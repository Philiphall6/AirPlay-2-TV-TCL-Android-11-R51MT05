using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AirPlay.Models;

namespace AirPlay
{
    public interface IRtspReceiver
    {
        void OnSetVolume(decimal volume);
        void OnData(H264Data data);
        void OnPCMData(PcmData data);
        void OnMetadata(IDictionary<string, object> metadata);
        void OnArtwork(byte[] artwork);
        void OnProgress(long start, long current, long end);
        void OnPlaybackState(bool isPlaying);
        void OnRemoteControl(string activeRemote, string dacpId);
        void OnDiagnostic(string message);
    }
}
