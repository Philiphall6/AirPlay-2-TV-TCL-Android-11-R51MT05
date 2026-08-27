using System;

namespace AirPlay.Android.Platform;

internal static class ReceiverStatus
{
    private static string _current = "Arrêté";

    public static event EventHandler<string>? Changed;

    public static string Current => _current;

    public static void Publish(string value)
    {
        _current = value;
        Changed?.Invoke(null, value);
        global::Android.Util.Log.Info("TclAirPlay", value);
    }
}
