using System;
using AirPlay.Models;

namespace AirPlay.Android.Platform;

internal static class NowPlayingStatus
{
    private static readonly object Gate = new();
    private static NowPlayingInfo _current = new();

    public static event EventHandler<NowPlayingInfo>? Changed;

    public static NowPlayingInfo Current
    {
        get
        {
            lock (Gate)
            {
                return _current.Clone();
            }
        }
    }

    public static void Publish(NowPlayingInfo value)
    {
        lock (Gate)
        {
            _current = value.Clone();
        }
        Changed?.Invoke(null, value.Clone());
    }
}
