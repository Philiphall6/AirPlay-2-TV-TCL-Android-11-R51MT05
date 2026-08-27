using System;
using System.Text;
using Android.AccessibilityServices;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Views.Accessibility;

namespace AirPlay.Android;

[Service(
    Name = "com.philphall.tclairplayreceiver.TclAirPlayTileAccessibilityService",
    Label = "Pont tuile AirPlay TCL",
    Permission = "android.permission.BIND_ACCESSIBILITY_SERVICE",
    Exported = true)]
[IntentFilter(new[] { "android.accessibilityservice.AccessibilityService" })]
[MetaData("android.accessibilityservice", Resource = "@xml/tcl_airplay_accessibility_service")]
public sealed class TclAirPlayTileAccessibilityService : AccessibilityService
{
    private const string LegacyAirPlayPackage = "com.tcl.airplay2";
    private const string TclSuspensionPackage = "com.tcl.suspension";
    private readonly Handler _handler = new(Looper.MainLooper!);
    private long _lastLaunchMs;
    private long _lastLegacyDismissMs;

    public override void OnAccessibilityEvent(AccessibilityEvent? accessibilityEvent)
    {
        if (accessibilityEvent == null)
        {
            return;
        }

        var packageName = accessibilityEvent.PackageName?.ToString() ?? string.Empty;
        var description = new StringBuilder();
        foreach (var item in accessibilityEvent.Text)
        {
            description.Append(' ').Append(item);
        }
        AppendNodeText(accessibilityEvent.Source, description, 0);
        var isAirPlayClick = accessibilityEvent.EventType == EventTypes.ViewClicked &&
            (description.ToString().Contains("AirPlay", StringComparison.OrdinalIgnoreCase) ||
             IsTclAirPlayTile(accessibilityEvent, packageName));
        var isLegacyWindow = string.Equals(packageName, LegacyAirPlayPackage,
            StringComparison.Ordinal) &&
            (accessibilityEvent.EventType == EventTypes.WindowStateChanged ||
             accessibilityEvent.EventType == EventTypes.NotificationStateChanged);
        if (!isAirPlayClick && !isLegacyWindow)
        {
            return;
        }

        var now = SystemClock.ElapsedRealtime();
        if (isLegacyWindow)
        {
            if (now - _lastLegacyDismissMs < 250)
            {
                return;
            }
            _lastLegacyDismissMs = now;
            global::Android.Util.Log.Info(
                "TclAirPlay",
                $"Ancien écran TCL fermé ({accessibilityEvent.EventType})");
            PerformGlobalAction(GlobalAction.Back);
            _handler.PostDelayed(LaunchReceiver, 30);
            _handler.PostDelayed(LaunchReceiver, 260);
            return;
        }

        if (now - _lastLaunchMs < 1800)
        {
            return;
        }
        _lastLaunchMs = now;
        global::Android.Util.Log.Info(
            "TclAirPlay",
            $"Entrée système AirPlay interceptée ({accessibilityEvent.EventType}, {packageName})");

        LaunchReceiver();
        _handler.PostDelayed(LaunchReceiver, 140);
    }

    public override void OnInterrupt()
    {
    }

    protected override bool OnKeyEvent(KeyEvent? keyEvent)
    {
        if (keyEvent?.Action == KeyEventActions.Down &&
            (keyEvent.KeyCode == Keycode.DpadCenter || keyEvent.KeyCode == Keycode.Enter ||
             keyEvent.KeyCode == Keycode.NumpadEnter) &&
            IsAirPlayFocused())
        {
            global::Android.Util.Log.Info("TclAirPlay", "Touche OK AirPlay interceptée avant TCL");
            LaunchReceiver();
            _handler.PostDelayed(LaunchReceiver, 140);
            return true;
        }
        return base.OnKeyEvent(keyEvent);
    }

    private void LaunchReceiver()
    {
        var intent = new Intent(this, typeof(MainActivity));
        intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop | ActivityFlags.SingleTop);
        StartActivity(intent);
    }

    private bool IsTclAirPlayTile(AccessibilityEvent accessibilityEvent, string packageName)
    {
        if (!string.Equals(packageName, TclSuspensionPackage, StringComparison.Ordinal))
        {
            return false;
        }

        var root = RootInActiveWindow;
        var labels = root?.FindAccessibilityNodeInfosByText("AirPlay");
        if (labels != null)
        {
            foreach (var label in labels)
            {
                AccessibilityNodeInfo? current = label;
                for (var depth = 0; current != null && depth < 4; depth++)
                {
                    if (current.Focused || current.Selected)
                    {
                        return true;
                    }
                    current = current.Parent;
                }
            }
        }

        if (accessibilityEvent.Source == null)
        {
            return false;
        }

        var sourceBounds = new Rect();
        accessibilityEvent.Source.GetBoundsInScreen(sourceBounds);
        var metrics = Resources?.DisplayMetrics;
        if (metrics == null || metrics.WidthPixels <= 0 || metrics.HeightPixels <= 0)
        {
            return false;
        }

        var centerX = sourceBounds.CenterX() / (double)metrics.WidthPixels;
        var centerY = sourceBounds.CenterY() / (double)metrics.HeightPixels;
        return centerX >= 0.12 && centerX <= 0.23 && centerY >= 0.72;
    }

    private bool IsAirPlayFocused()
    {
        var root = RootInActiveWindow;
        if (root == null || !string.Equals(root.PackageName?.ToString(), TclSuspensionPackage,
                StringComparison.Ordinal))
        {
            return false;
        }

        var labels = root.FindAccessibilityNodeInfosByText("AirPlay");
        foreach (var label in labels)
        {
            AccessibilityNodeInfo? current = label;
            for (var depth = 0; current != null && depth < 4; depth++)
            {
                if (current.Focused || current.Selected)
                {
                    return true;
                }
                current = current.Parent;
            }
        }
        return false;
    }

    private static void AppendNodeText(AccessibilityNodeInfo? node, StringBuilder output, int depth)
    {
        if (node == null || depth > 4)
        {
            return;
        }
        output.Append(' ').Append(node.Text).Append(' ').Append(node.ContentDescription);
        for (var index = 0; index < node.ChildCount; index++)
        {
            AppendNodeText(node.GetChild(index), output, depth + 1);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _handler.RemoveCallbacksAndMessages(null);
            _handler.Dispose();
        }
        base.Dispose(disposing);
    }
}
