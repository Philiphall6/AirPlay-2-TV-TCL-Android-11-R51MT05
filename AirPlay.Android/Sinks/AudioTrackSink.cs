using System;
using AirPlay.Models;
using Android.Media;

namespace AirPlay.Android.Sinks;

internal sealed class AudioTrackSink : IDisposable
{
    private const int SampleRate = 44100;
    private readonly object _gate = new();
    private AudioTrack? _track;
    private bool _paused;

    public void Write(PcmData pcm)
    {
        if (pcm.Data == null || pcm.Length <= 0)
        {
            return;
        }

        lock (_gate)
        {
            if (_paused)
            {
                return;
            }
            EnsureStarted();
            var length = Math.Min(pcm.Length, pcm.Data.Length);
            _track!.Write(pcm.Data, 0, length);
        }
    }

    public void SetPaused(bool paused)
    {
        lock (_gate)
        {
            _paused = paused;
            if (_track == null)
            {
                return;
            }
            if (paused)
            {
                _track.Pause();
                _track.Flush();
            }
            else
            {
                _track.Play();
            }
        }
    }

    public void SetVolume(decimal volume)
    {
        lock (_gate)
        {
            if (_track == null)
            {
                return;
            }

            var linear = (float)Math.Clamp((double)((volume + 30m) / 30m), 0d, 1d);
            _track.SetVolume(linear);
        }
    }

    private void EnsureStarted()
    {
        if (_track != null)
        {
            return;
        }

        var minimum = AudioTrack.GetMinBufferSize(SampleRate, ChannelOut.Stereo, Encoding.Pcm16bit);
        const int bytesPerFrame = 2 * sizeof(short);
        var bufferSize = Math.Max(minimum, SampleRate / 2);
        bufferSize = ((bufferSize + bytesPerFrame - 1) / bytesPerFrame) * bytesPerFrame;
        using var attributes = new AudioAttributes.Builder()
            .SetUsage(AudioUsageKind.Media)
            .SetContentType(AudioContentType.Music)
            .Build() ?? throw new InvalidOperationException("Unable to create Android audio attributes.");
        using var format = new AudioFormat.Builder()
            .SetEncoding(Encoding.Pcm16bit)
            .SetSampleRate(SampleRate)
            .SetChannelMask(ChannelOut.Stereo)
            .Build() ?? throw new InvalidOperationException("Unable to create Android PCM format.");
        _track = new AudioTrack.Builder()
            .SetAudioAttributes(attributes)
            .SetAudioFormat(format)
            .SetBufferSizeInBytes(bufferSize)
            .SetTransferMode(AudioTrackMode.Stream)
            .Build() ?? throw new InvalidOperationException("Unable to create Android AudioTrack.");
        _track.Play();
        global::Android.Util.Log.Info("TclAirPlay", $"AudioTrack actif: 44100 Hz stéréo PCM16, buffer={bufferSize}");
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_track == null)
            {
                return;
            }

            try
            {
                _track.Stop();
            }
            catch (Java.Lang.IllegalStateException)
            {
            }
            _track.Release();
            _track.Dispose();
            _track = null;
        }
    }
}
