using System;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using AirPlay.Android.Platform;
using AirPlay.Android.Sinks;
using AirPlay.Models.Configs;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Media.TV;
using Android.Net.Wifi;
using Android.OS;
using Microsoft.Extensions.Options;

namespace AirPlay.Android;

[Service(
    Name = "com.philphall.tclairplayreceiver.AirPlayForegroundService",
    Exported = false,
    ForegroundServiceType = ForegroundService.TypeMediaPlayback)]
public sealed class AirPlayForegroundService : Service
{
    private const string ChannelId = "tcl_airplay_receiver";
    private const int NotificationId = 2102;
    private CancellationTokenSource? _cancellation;
    private IAirPlayReceiver? _receiver;
    private AudioTrackSink? _audio;
    private H264SurfaceSink? _video;
    private WifiManager.MulticastLock? _multicastLock;
    private Task? _startTask;
    private long _lastTvInputLaunchMs;
    private long _lastGeometryBroadcastMs;
    private int _lastVideoWidth;
    private int _lastVideoHeight;
    private long _surfaceReadySinceMs;
    private bool _tvInputWasOpened;

    public override void OnCreate()
    {
        base.OnCreate();
        CreateNotificationChannel();
        StartForeground(NotificationId, BuildNotification("Initialisation"));
        AcquireMulticastLock();

        _cancellation = new CancellationTokenSource();
        _audio = new AudioTrackSink();
        _video = new H264SurfaceSink();
        _startTask = Task.Run(
            () => StartReceiverAsync(_cancellation.Token),
            _cancellation.Token);
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId) =>
        StartCommandResult.Sticky;

    public override global::Android.OS.IBinder? OnBind(Intent? intent) => null;

    private async Task StartReceiverAsync(CancellationToken cancellationToken)
    {
        try
        {
            var nativeDir = ApplicationInfo?.NativeLibraryDir ?? string.Empty;
            var aac = Path.Combine(nativeDir, "libfdk-aac.so");
            var alac = Path.Combine(nativeDir, "libalac.so");
            if (!File.Exists(aac) || !File.Exists(alac))
            {
                ReceiverStatus.Publish("Codecs ARMv7 manquants : libfdk-aac.so / libalac.so");
            }

            var deviceMac = FindMacAddress();
            var baseName = ReceiverNameSettings.GetBaseName(this);
            var audioName = ReceiverNameSettings.AudioName(baseName);
            var videoName = ReceiverNameSettings.VideoName(baseName);
            var receiverConfig = Options.Create(new AirPlayReceiverConfig
            {
                Instance = baseName,
                AudioInstance = audioName,
                VideoInstance = videoName,
                AirTunesPort = 5000,
                AirPlayPort = 7000,
                AirPlayDataPort = 7100,
                DeviceMacAddress = deviceMac,
                AudioDeviceMacAddress = DeriveAudioMacAddress(deviceMac)
            });
            var codecConfig = Options.Create(new CodecLibrariesConfig
            {
                AACLibPath = aac,
                ALACLibPath = alac
            });
            var dumpConfig = Options.Create(new DumpConfig
            {
                Path = FilesDir?.AbsolutePath ?? CacheDir?.AbsolutePath ?? "/data/local/tmp"
            });

            _receiver = new AirPlayReceiver(receiverConfig, codecConfig, dumpConfig);
            _receiver.OnPCMDataReceived += (_, pcm) => _audio?.Write(pcm);
            _receiver.OnH264DataReceived += (_, frame) =>
            {
                EnsureTclTvInputVisible();
                NotifyTclVideoGeometry(frame.Width, frame.Height);
                _video?.Write(frame);
            };
            _receiver.OnSetVolumeReceived += (_, volume) => _audio?.SetVolume(volume);
            _receiver.OnDiagnosticReceived += (_, message) =>
                global::Android.Util.Log.Info("TclAirPlayProtocol", message);

            await _receiver.StartListeners(cancellationToken).ConfigureAwait(false);
            await _receiver.StartMdnsAsync().ConfigureAwait(false);
            ReceiverStatus.Publish($"Actif : {audioName} · {videoName}");
            UpdateNotification($"{baseName} actif");
            NotifyLegacyTclReady();
        }
        catch (Exception exception)
        {
            ReceiverStatus.Publish($"Erreur : {exception.Message}");
            UpdateNotification("Erreur de démarrage");
            global::Android.Util.Log.Error("TclAirPlay", exception.ToString());
        }
    }

    private string FindMacAddress()
    {
        try
        {
            var candidate = NetworkInterface.GetAllNetworkInterfaces()
                .Where(item => item.OperationalStatus == OperationalStatus.Up)
                .Select(item => item.GetPhysicalAddress().GetAddressBytes())
                .FirstOrDefault(bytes => bytes.Length == 6 && bytes.Any(value => value != 0));
            if (candidate != null)
            {
                return string.Join(":", candidate.Select(value => value.ToString("X2")));
            }
        }
        catch (Exception exception)
        {
            global::Android.Util.Log.Warn("TclAirPlay", $"MAC indisponible: {exception.Message}");
        }

        return "02:51:03:00:00:01";
    }

    private static string DeriveAudioMacAddress(string deviceMac)
    {
        var bytes = deviceMac.Split(':').Select(value => Convert.ToByte(value, 16)).ToArray();
        if (bytes.Length != 6)
        {
            return "02:51:03:00:00:02";
        }

        bytes[0] = (byte)(bytes[0] | 0x02);
        bytes[5] = (byte)(bytes[5] ^ 0x01);
        return string.Join(":", bytes.Select(value => value.ToString("X2")));
    }

    private void AcquireMulticastLock()
    {
        var wifi = (WifiManager?)GetSystemService(WifiService);
        _multicastLock = wifi?.CreateMulticastLock("tcl-airplay-mdns");
        if (_multicastLock != null)
        {
            _multicastLock.SetReferenceCounted(false);
            _multicastLock.Acquire();
        }
    }

    private void CreateNotificationChannel()
    {
        var manager = (NotificationManager?)GetSystemService(NotificationService);
        manager?.CreateNotificationChannel(new NotificationChannel(
            ChannelId,
            "AirPlay 2 TV TCL",
            NotificationImportance.Low));
    }

    private Notification BuildNotification(string text)
    {
        var launchIntent = new Intent(this, typeof(MainActivity));
        var pending = PendingIntent.GetActivity(
            this,
            0,
            launchIntent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        return new Notification.Builder(this, ChannelId)
            .SetContentTitle("AirPlay 2 TV TCL")
            .SetContentText(text)
            .SetSmallIcon(global::Android.Resource.Drawable.IcMediaPlay)
            .SetContentIntent(pending)
            .SetOngoing(true)
            .Build();
    }

    private void UpdateNotification(string text)
    {
        var manager = (NotificationManager?)GetSystemService(NotificationService);
        manager?.Notify(NotificationId, BuildNotification(text));
    }

    private void NotifyLegacyTclReady()
    {
        foreach (var packageName in new[] { "com.tcl.airplay2", "com.mediatek.AirplayAPK" })
        {
            var ready = new Intent("AirPlay.appReady");
            ready.SetPackage(packageName);
            SendBroadcast(ready, LegacyTclBridgeReceiver.BroadcastPermission);

            var systemReady = new Intent("Intent.airplay.airplaysystemready");
            systemReady.SetPackage(packageName);
            SendBroadcast(systemReady, LegacyTclBridgeReceiver.BroadcastPermission);
        }
    }

    private void EnsureTclTvInputVisible()
    {
        var surface = ReceiverSurfaceRegistry.Current;
        if (surface?.IsValid == true)
        {
            _tvInputWasOpened = true;
            return;
        }
        if (_tvInputWasOpened)
        {
            return;
        }

        var now = SystemClock.ElapsedRealtime();
        if (now - _lastTvInputLaunchMs < 5000)
        {
            return;
        }
        _lastTvInputLaunchMs = now;

        try
        {
            // Let the privileged TCL launcher perform the source switch. Unlike
            // this foreground service, it is allowed by Android 11 to bring the
            // TV activity to the front. Start.LunaTest switches source before
            // consulting the removed proprietary daemon.
            var sourceSwitch = new Intent("Start.LunaTest");
            sourceSwitch.SetPackage("com.tcl.airplay2");
            sourceSwitch.AddFlags(ActivityFlags.ReceiverForeground);
            SendBroadcast(sourceSwitch, LegacyTclBridgeReceiver.BroadcastPermission);

            // TVActivity compares the encoded data string literally and only tunes
            // when the slash in the TV input id is kept as "%2F".  On this G03,
            // TvContract.BuildChannelUriForPassthroughInput() returns an unescaped
            // slash through the .NET Android binding, so build the vendor URI
            // explicitly.
            var channel = global::Android.Net.Uri.Parse(
                "content://android.media.tv/passthrough/" +
                "com.mediatek.AirplayAPK%2F.AirPlayTvInputService");
            var intent = new Intent(Intent.ActionView, channel);
            intent.SetClassName("com.mediatek.AirplayAPK", "com.mediatek.activity.TVActivity");
            intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
            StartActivity(intent);
            global::Android.Util.Log.Info("TclAirPlay", "Demande d'ouverture du TVInputService TCL");
        }
        catch (Exception exception)
        {
            global::Android.Util.Log.Warn("TclAirPlay", $"TVInput TCL indisponible: {exception.Message}");
        }
    }

    private void NotifyTclVideoGeometry(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var now = SystemClock.ElapsedRealtime();
        if (ReceiverSurfaceRegistry.Current?.IsValid != true)
        {
            _surfaceReadySinceMs = 0;
            return;
        }
        if (_surfaceReadySinceMs == 0)
        {
            _surfaceReadySinceMs = now;
            return;
        }
        if (now - _surfaceReadySinceMs < 3000)
        {
            return;
        }

        var changed = width != _lastVideoWidth || height != _lastVideoHeight;
        if (!changed && now - _lastGeometryBroadcastMs < 2000)
        {
            return;
        }

        _lastVideoWidth = width;
        _lastVideoHeight = height;
        _lastGeometryBroadcastMs = now;

        var geometry = new Intent("com.philphall.tclairplayreceiver.TCL_VIDEO_GEOMETRY");
        geometry.SetPackage("com.mediatek.AirplayAPK");
        geometry.PutExtra("width", width);
        geometry.PutExtra("height", height);
        SendBroadcast(geometry, LegacyTclBridgeReceiver.BroadcastPermission);
        global::Android.Util.Log.Info("TclAirPlay", $"Géométrie vidéo TCL: {width}x{height}");
    }

    public override void OnDestroy()
    {
        _cancellation?.Cancel();
        if (_receiver != null)
        {
            _ = _receiver.StopAsync();
        }
        _video?.Dispose();
        _audio?.Dispose();
        if (_multicastLock?.IsHeld == true)
        {
            _multicastLock.Release();
        }
        _multicastLock?.Dispose();
        _cancellation?.Dispose();
        ReceiverStatus.Publish("Arrêté");
        base.OnDestroy();
    }
}
