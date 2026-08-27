using Android.App;
using Android.Content;

namespace AirPlay.Android;

[BroadcastReceiver(
    Name = "com.philphall.tclairplayreceiver.BootReceiver",
    Enabled = false,
    Exported = true)]
[IntentFilter(new[] { Intent.ActionBootCompleted })]
public sealed class BootReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null || intent?.Action != Intent.ActionBootCompleted)
        {
            return;
        }

        context.StartForegroundService(new Intent(context, typeof(AirPlayForegroundService)));
    }
}
