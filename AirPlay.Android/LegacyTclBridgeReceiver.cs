using Android.App;
using Android.Content;

namespace AirPlay.Android;

[BroadcastReceiver(
    Name = "com.philphall.tclairplayreceiver.LegacyTclBridgeReceiver",
    Enabled = true,
    Exported = true,
    Permission = BroadcastPermission)]
[IntentFilter(new[]
{
    StartDaemonAction,
    LaunchDaemonAction,
    RestartAction,
    DestroyAction,
    StopAction
})]
public sealed class LegacyTclBridgeReceiver : BroadcastReceiver
{
    public const string BroadcastPermission = "com.mediatek.permission.AirPlay.BroadCast";
    public const string StartDaemonAction = "Start.daemon";
    public const string LaunchDaemonAction = "Intent.Launch.AirPlayDaemon";
    public const string RestartAction = "Intent.airplay.restart";
    public const string DestroyAction = "AirPlay.TIS.Destroy";
    public const string StopAction = "airplay.tis.stop.action";

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null || string.IsNullOrEmpty(intent?.Action))
        {
            return;
        }

        var serviceIntent = new Intent(context, typeof(AirPlayForegroundService));
        switch (intent.Action)
        {
            case DestroyAction:
            case StopAction:
                context.StopService(serviceIntent);
                break;
            case StartDaemonAction:
            case LaunchDaemonAction:
            case RestartAction:
                context.StartForegroundService(serviceIntent);
                break;
        }
    }
}
