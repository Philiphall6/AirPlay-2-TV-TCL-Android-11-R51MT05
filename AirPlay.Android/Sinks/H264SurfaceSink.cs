using System;
using AirPlay.Android.Platform;
using AirPlay.Models;
using Android.Media;
using Android.Views;

namespace AirPlay.Android.Sinks;

internal sealed class H264SurfaceSink : IDisposable
{
    private readonly object _gate = new();
    private MediaCodec? _codec;
    private Surface? _surface;
    private int _width;
    private int _height;
    private long _fallbackPtsUs;
    private long _lastPtsUs;
    private long _inputFrames;
    private long _outputFrames;
    private long _unavailableInputs;
    private H264Data? _latestIdr;

    public H264SurfaceSink()
    {
        _surface = ReceiverSurfaceRegistry.Current;
        ReceiverSurfaceRegistry.Changed += OnSurfaceChanged;
    }

    public void Write(H264Data frame)
    {
        if (frame.Data == null || frame.Length <= 0)
        {
            return;
        }

        lock (_gate)
        {
            if (frame.FrameType == 5)
            {
                // The first IDR includes SPS/PPS, but often arrives before the
                // TCL TvView Surface. Retain it for a clean decoder startup.
                _latestIdr = frame;
            }

            if (_surface == null || !_surface.IsValid)
            {
                return;
            }

            if (_codec == null || _width != frame.Width || _height != frame.Height)
            {
                var startupFrame = frame.FrameType == 5
                    ? frame
                    : _latestIdr is H264Data cached &&
                      cached.Width == frame.Width && cached.Height == frame.Height
                        ? cached
                        : (H264Data?)null;
                if (startupFrame == null)
                {
                    return;
                }

                Configure(startupFrame.Value);
                if (_codec != null && frame.FrameType != 5)
                {
                    QueueInput(startupFrame.Value);
                    DrainOutput();
                }
            }

            if (_codec == null)
            {
                return;
            }

            QueueInput(frame);
            DrainOutput();
        }
    }

    private void QueueInput(H264Data frame)
    {
        if (_codec == null)
        {
            return;
        }

        // Realtek's decoder frequently needs a few milliseconds to recycle an
        // input buffer. A zero timeout dropped most frames on the R51MT05.
        var inputIndex = _codec.DequeueInputBuffer(10000);
        if (inputIndex >= 0)
        {
            var input = _codec.GetInputBuffer(inputIndex);
            if (input != null)
            {
                input.Clear();
                var length = Math.Min(frame.Length, frame.Data.Length);
                if (length > input.Remaining())
                {
                    global::Android.Util.Log.Warn(
                        "TclAirPlay",
                        $"Trame H264 trop grande: {length} > buffer {input.Remaining()}");
                    return;
                }
                input.Put(frame.Data, 0, length);
                var ptsUs = frame.Pts > 0 ? frame.Pts : (_fallbackPtsUs += 33333);
                if (ptsUs <= _lastPtsUs)
                {
                    ptsUs = _lastPtsUs + 1;
                }
                _lastPtsUs = ptsUs;
                _codec.QueueInputBuffer(inputIndex, 0, length, ptsUs, MediaCodecBufferFlags.None);
                _inputFrames++;
                if (frame.FrameType == 5 || _inputFrames % 120 == 0)
                {
                    global::Android.Util.Log.Info(
                        "TclAirPlay",
                        $"Codec H264 entrée={_inputFrames}, sortie={_outputFrames}, " +
                        $"type={frame.FrameType}, taille={length}, pts={ptsUs}");
                }
            }
        }
        else
        {
            _unavailableInputs++;
            if (_unavailableInputs % 120 == 0)
            {
                global::Android.Util.Log.Warn(
                    "TclAirPlay", $"Codec H264 sans buffer d'entrée: {_unavailableInputs}");
            }
            if (_unavailableInputs >= 240)
            {
                global::Android.Util.Log.Warn(
                    "TclAirPlay", "Codec H264 bloqué, réinitialisation sur la dernière IDR");
                ReleaseCodec();
            }
        }
    }

    private void Configure(H264Data startupFrame)
    {
        ReleaseCodec();
        var width = startupFrame.Width;
        var height = startupFrame.Height;
        if (_surface == null || !_surface.IsValid || width <= 0 || height <= 0)
        {
            return;
        }

        _width = width;
        _height = height;
        _lastPtsUs = 0;
        _inputFrames = 0;
        _outputFrames = 0;
        _unavailableInputs = 0;
        var format = MediaFormat.CreateVideoFormat(MediaFormat.MimetypeVideoAvc, width, height);
        var (sps, pps) = ExtractCodecSpecificData(startupFrame.Data, startupFrame.Length);
        if (sps != null && pps != null)
        {
            format.SetByteBuffer("csd-0", Java.Nio.ByteBuffer.Wrap(sps));
            format.SetByteBuffer("csd-1", Java.Nio.ByteBuffer.Wrap(pps));
            global::Android.Util.Log.Info(
                "TclAirPlay", $"Codec H264 CSD: SPS={sps.Length}, PPS={pps.Length}");
        }
        else
        {
            global::Android.Util.Log.Warn("TclAirPlay", "Codec H264 sans SPS/PPS dans l'IDR");
        }
        _codec = MediaCodec.CreateDecoderByType(MediaFormat.MimetypeVideoAvc);
        _codec.Configure(format, _surface, null, MediaCodecConfigFlags.None);
        _codec.Start();
        ReceiverStatus.Publish($"Décodage H.264 {width}x{height}");
    }

    private static (byte[]? Sps, byte[]? Pps) ExtractCodecSpecificData(byte[] data, int requestedLength)
    {
        var length = Math.Min(requestedLength, data.Length);
        byte[]? sps = null;
        byte[]? pps = null;
        var offset = FindStartCode(data, 0, length);
        while (offset >= 0 && offset + 4 < length)
        {
            var next = FindStartCode(data, offset + 4, length);
            var end = next >= 0 ? next : length;
            var type = data[offset + 4] & 0x1f;
            if ((type == 7 && sps == null) || (type == 8 && pps == null))
            {
                var nalu = new byte[end - offset];
                Array.Copy(data, offset, nalu, 0, nalu.Length);
                if (type == 7)
                {
                    sps = nalu;
                }
                else
                {
                    pps = nalu;
                }
            }
            if (sps != null && pps != null)
            {
                break;
            }
            offset = next;
        }
        return (sps, pps);
    }

    private static int FindStartCode(byte[] data, int start, int length)
    {
        for (var index = start; index + 3 < length; index++)
        {
            if (data[index] == 0 && data[index + 1] == 0 &&
                data[index + 2] == 0 && data[index + 3] == 1)
            {
                return index;
            }
        }
        return -1;
    }

    private void DrainOutput()
    {
        if (_codec == null)
        {
            return;
        }

        var info = new MediaCodec.BufferInfo();
        while (true)
        {
            var outputIndex = _codec.DequeueOutputBuffer(info, 0);
            if (outputIndex >= 0)
            {
                _codec.ReleaseOutputBuffer(outputIndex, true);
                _outputFrames++;
                if (_outputFrames == 1 || _outputFrames % 120 == 0)
                {
                    global::Android.Util.Log.Info(
                        "TclAirPlay", $"Codec H264 image rendue: {_outputFrames}");
                }
                continue;
            }
            if (outputIndex == (int)MediaCodecInfoState.OutputFormatChanged)
            {
                global::Android.Util.Log.Info(
                    "TclAirPlay", $"Codec H264 format sortie: {_codec.OutputFormat}");
                continue;
            }
            break;
        }
    }

    private void OnSurfaceChanged(object? sender, Surface? surface)
    {
        lock (_gate)
        {
            _surface = surface;
            ReleaseCodec();
        }
    }

    private void ReleaseCodec()
    {
        if (_codec == null)
        {
            return;
        }

        try
        {
            _codec.Stop();
        }
        catch (Java.Lang.IllegalStateException)
        {
        }
        _codec.Release();
        _codec.Dispose();
        _codec = null;
    }

    public void Dispose()
    {
        ReceiverSurfaceRegistry.Changed -= OnSurfaceChanged;
        lock (_gate)
        {
            ReleaseCodec();
        }
    }
}
