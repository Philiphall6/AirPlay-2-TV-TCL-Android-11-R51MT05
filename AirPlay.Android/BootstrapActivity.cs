using Android.App;
using Android.Content;
using Android.OS;

namespace AirPlay.Android;

[Activity(
    Name = "com.philphall.tclairplayreceiver.BootstrapActivity",
    Theme = "@android:style/Theme.NoDisplay",
    ExcludeFromRecents = true,
    NoHistory = true,
    Exported = true)]
public sealed class BootstrapActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        StartForegroundService(new Intent(this, typeof(AirPlayForegroundService)));
        Finish();
    }
}
