using System;
using Android.Views;

namespace AirPlay.Android.Platform;

internal static class ReceiverSurfaceRegistry
{
    private static readonly object Gate = new();
    private static Surface? _surface;

    public static event EventHandler<Surface?>? Changed;

    public static Surface? Current
    {
        get
        {
            lock (Gate)
            {
                return _surface;
            }
        }
    }

    public static void Set(Surface? surface)
    {
        lock (Gate)
        {
            _surface = surface;
        }
        Changed?.Invoke(null, surface);
    }
}
