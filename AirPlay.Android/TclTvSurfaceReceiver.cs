using AirPlay.Android.Platform;
using Android.App;
using Android.Content;
using Android.Views;

namespace AirPlay.Android;

[BroadcastReceiver(
    Name = "com.philphall.tclairplayreceiver.TclTvSurfaceReceiver",
    Enabled = true,
    Exported = true,
    Permission = LegacyTclBridgeReceiver.BroadcastPermission)]
[IntentFilter(new[] { SurfaceAction })]
public sealed class TclTvSurfaceReceiver : BroadcastReceiver
{
    public const string SurfaceAction = "com.philphall.tclairplayreceiver.TCL_TV_SURFACE";
    public const string SurfaceExtra = "surface";

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent?.Action != SurfaceAction)
        {
            return;
        }

#pragma warning disable CS0618
        var surface = intent.GetParcelableExtra(SurfaceExtra) as Surface;
#pragma warning restore CS0618
        ReceiverSurfaceRegistry.Set(surface);
        global::Android.Util.Log.Info(
            "TclAirPlay",
            surface?.IsValid == true ? "Surface TVInput TCL connectée" : "Surface TVInput TCL libérée");
    }
}
