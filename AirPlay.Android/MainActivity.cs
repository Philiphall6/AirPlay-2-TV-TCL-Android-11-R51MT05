using AirPlay.Android.Platform;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace AirPlay.Android;

[Activity(
    Label = "TCL AirPlay Receiver",
    MainLauncher = true,
    Exported = true,
    ScreenOrientation = ScreenOrientation.Landscape,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
public sealed class MainActivity : Activity, ISurfaceHolderCallback
{
    private TextView? _status;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        var root = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical
        };
        root.SetPadding(24, 24, 24, 24);
        root.SetBackgroundColor(Color.Black);

        _status = new TextView(this)
        {
            Text = ReceiverStatus.Current,
            TextSize = 18f
        };
        _status.SetTextColor(Color.White);

        var controls = new LinearLayout(this)
        {
            Orientation = Orientation.Horizontal
        };
        var start = new Button(this) { Text = "Démarrer AirPlay" };
        var stop = new Button(this) { Text = "Arrêter" };
        start.Click += (_, _) => StartReceiver();
        stop.Click += (_, _) => StopService(new Intent(this, typeof(AirPlayForegroundService)));
        controls.AddView(start);
        controls.AddView(stop);

        var surface = new SurfaceView(this);
        surface.Holder?.AddCallback(this);

        root.AddView(_status);
        root.AddView(controls);
        root.AddView(surface, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            0,
            1f));
        SetContentView(root);

        ReceiverStatus.Changed += OnStatusChanged;
    }

    private void StartReceiver()
    {
        var intent = new Intent(this, typeof(AirPlayForegroundService));
        StartForegroundService(intent);
    }

    private void OnStatusChanged(object? sender, string value)
    {
        RunOnUiThread(() =>
        {
            if (_status != null)
            {
                _status.Text = value;
            }
        });
    }

    protected override void OnDestroy()
    {
        ReceiverStatus.Changed -= OnStatusChanged;
        ReceiverSurfaceRegistry.Set(null);
        base.OnDestroy();
    }

    public void SurfaceCreated(ISurfaceHolder holder) => ReceiverSurfaceRegistry.Set(holder.Surface);

    public void SurfaceChanged(ISurfaceHolder holder, Format format, int width, int height) =>
        ReceiverSurfaceRegistry.Set(holder.Surface);

    public void SurfaceDestroyed(ISurfaceHolder holder) => ReceiverSurfaceRegistry.Set(null);
}
