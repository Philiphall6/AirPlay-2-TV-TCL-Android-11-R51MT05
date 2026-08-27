using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AirPlay.Models;
using AirPlay.Services.Implementations;
using AirPlay.Utils;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

namespace AirPlay.Listeners
{
    public class MirroringListener : BaseTcpListener
    {
        public const string AIR_PLAY_STREAM_KEY = "AirPlayStreamKey";
        public const string AIR_PLAY_STREAM_IV = "AirPlayStreamIV";

        private readonly IRtspReceiver _receiver;
        private readonly string _sessionId;
        private readonly IBufferedCipher _aesCtrDecrypt;
        private readonly OmgHax _omgHax = new OmgHax();

        private byte[] _og = new byte[16];
        private int _nextDecryptCount;
        private long _videoFrameCount;

        public MirroringListener(IRtspReceiver receiver, string sessionId, ushort port) : base(port, true)
        {
            _receiver = receiver;
            _sessionId = sessionId;

            _aesCtrDecrypt = CipherUtilities.GetCipher("AES/CTR/NoPadding");
        }

        public override async Task OnRawReceivedAsync(TcpClient client, NetworkStream stream, CancellationToken cancellationToken)
        {
            // Get session by active-remove header value
            var session = await SessionManager.Current.GetSessionAsync(_sessionId);

            // If we have not decripted session AesKey
            if (session.DecryptedAesKey == null)
            {
                byte[] decryptedAesKey = new byte[16];
                _omgHax.DecryptAesKey(session.KeyMsg, session.AesKey, decryptedAesKey);
                session.DecryptedAesKey = decryptedAesKey;
            }

            InitAesCtrCipher(session.DecryptedAesKey, session.EcdhShared, session.StreamConnectionId);

            var headerBuffer = new byte[128];
            while (!cancellationToken.IsCancellationRequested)
            {
                var readStart = 0;
                if (!await ReadExactlyAsync(stream, headerBuffer, readStart, 4, cancellationToken).ConfigureAwait(false))
                {
                    break;
                }

                if ((headerBuffer[0] == 80 && headerBuffer[1] == 79 && headerBuffer[2] == 83 && headerBuffer[3] == 84) ||
                    (headerBuffer[0] == 71 && headerBuffer[1] == 69 && headerBuffer[2] == 84))
                {
                    _receiver.OnDiagnostic("Requête texte inattendue sur le canal miroir");
                    break;
                }

                readStart = 4;
                if (!await ReadExactlyAsync(stream, headerBuffer, readStart, 128 - readStart, cancellationToken).ConfigureAwait(false))
                {
                    break;
                }

                var header = new MirroringHeader(headerBuffer);
                if (header.PayloadSize < 0 || header.PayloadSize > 16 * 1024 * 1024)
                {
                    _receiver.OnDiagnostic($"Taille miroir invalide: {header.PayloadSize}");
                    break;
                }

                var payload = new byte[header.PayloadSize];
                if (!await ReadExactlyAsync(stream, payload, 0, payload.Length, cancellationToken).ConfigureAwait(false))
                {
                    break;
                }

                try
                {
                    if (header.PayloadType == 0)
                    {
                        session.Pts = header.PayloadPts;
                        if (session.SpsPps != null && session.WidthSource.HasValue && session.HeightSource.HasValue)
                        {
                            DecryptVideoData(payload, out byte[] output);
                            ProcessVideo(output, session.SpsPps, header.PayloadPts,
                                session.WidthSource.Value, session.HeightSource.Value);
                        }
                    }
                    else if (header.PayloadType == 1)
                    {
                        ProcessSpsPps(payload, out byte[] spsPps);
                        session.SpsPps = spsPps;
                        session.WidthSource = header.WidthSource;
                        session.HeightSource = header.HeightSource;
                        _receiver.OnDiagnostic(
                            $"Miroir SPS/PPS: {header.WidthSource}x{header.HeightSource}, {spsPps?.Length ?? 0} octets");
                    }
                }
                catch (Exception e)
                {
                    _receiver.OnDiagnostic($"Erreur paquet miroir: {e.GetType().Name}: {e.Message}");
                }

                await SessionManager.Current.CreateOrUpdateSessionAsync(_sessionId, session);
                headerBuffer = new byte[128];
            }

            Console.WriteLine($"Closing mirroring connection..");
        }

        private static async Task<bool> ReadExactlyAsync(
            NetworkStream stream,
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            while (count > 0)
            {
                var read = await stream.ReadAsync(
                    buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
                if (read <= 0)
                {
                    return false;
                }
                offset += read;
                count -= read;
            }
            return true;
        }

        private void DecryptVideoData(byte[] videoData, out byte[] output)
        {
            if (_nextDecryptCount > 0)
            {
                for (int i = 0; i < _nextDecryptCount; i++)
                {
                    videoData[i] = (byte)(videoData[i] ^ _og[(16 - _nextDecryptCount) + i]);
                }
            }

            int encryptlen = ((videoData.Length - _nextDecryptCount) / 16) * 16;
            _aesCtrDecrypt.ProcessBytes(videoData, _nextDecryptCount, encryptlen, videoData, _nextDecryptCount);
            Array.Copy(videoData, _nextDecryptCount, videoData, _nextDecryptCount, encryptlen);

            int restlen = (videoData.Length - _nextDecryptCount) % 16;
            int reststart = videoData.Length - restlen;
            _nextDecryptCount = 0;
            if (restlen > 0)
            {
                Array.Fill(_og, (byte)0);
                Array.Copy(videoData, reststart, _og, 0, restlen);
                _aesCtrDecrypt.ProcessBytes(_og, 0, 16, _og, 0);
                Array.Copy(_og, 0, videoData, reststart, restlen);
                _nextDecryptCount = 16 - restlen;
            }

            output = new byte[videoData.Length];
            Array.Copy(videoData, 0, output, 0, videoData.Length);

            // Release video data
            videoData = null;
        }

        private void InitAesCtrCipher(byte[] aesKey, byte[] ecdhShared, string streamConnectionId)
        {
            byte[] eaesKey = Utilities.Hash(aesKey, ecdhShared);

            byte[] skey = Encoding.UTF8.GetBytes($"{AIR_PLAY_STREAM_KEY}{streamConnectionId}");
            byte[] hash1 = Utilities.Hash(skey, Utilities.CopyOfRange(eaesKey, 0, 16));

            byte[] siv = Encoding.UTF8.GetBytes($"{AIR_PLAY_STREAM_IV}{streamConnectionId}");
            byte[] hash2 = Utilities.Hash(siv, Utilities.CopyOfRange(eaesKey, 0, 16));

            byte[] decryptAesKey = new byte[16];
            byte[] decryptAesIV = new byte[16];
            Array.Copy(hash1, 0, decryptAesKey, 0, 16);
            Array.Copy(hash2, 0, decryptAesIV, 0, 16);

            var keyParameter = ParameterUtilities.CreateKeyParameter("AES", decryptAesKey);
            var cipherParameters = new ParametersWithIV(keyParameter, decryptAesIV, 0, decryptAesIV.Length);

            _aesCtrDecrypt.Init(false, cipherParameters);
        }

        private void ProcessVideo(byte[] payload, byte[] spsPps, long pts, int widthSource, int heightSource)
        {
            if (payload == null || payload.Length < 5 || spsPps == null || spsPps.Length == 0)
            {
                return;
            }

            var offset = 0;
            var firstFrameType = 0;
            var containsIdr = false;
            var naluCount = 0;
            while (offset + 4 <= payload.Length)
            {
                var naluLength =
                    ((payload[offset] & 0xff) << 24) |
                    ((payload[offset + 1] & 0xff) << 16) |
                    ((payload[offset + 2] & 0xff) << 8) |
                    (payload[offset + 3] & 0xff);
                if (naluLength <= 0 || offset + 4 + naluLength > payload.Length)
                {
                    _receiver.OnDiagnostic(
                        $"NAL miroir invalide: offset={offset}, taille={naluLength}, paquet={payload.Length}");
                    return;
                }

                var frameType = payload[offset + 4] & 0x1f;
                if (naluCount == 0)
                {
                    firstFrameType = frameType;
                }
                containsIdr |= frameType == 5;
                naluCount++;

                payload[offset] = 0;
                payload[offset + 1] = 0;
                payload[offset + 2] = 0;
                payload[offset + 3] = 1;
                offset += 4 + naluLength;
            }

            if (offset != payload.Length)
            {
                _receiver.OnDiagnostic($"Fin NAL miroir invalide: {offset}/{payload.Length}");
                return;
            }

            var h264Data = new H264Data { FrameType = containsIdr ? 5 : firstFrameType };
            if (containsIdr)
            {
                var payloadOut = new byte[payload.Length + spsPps.Length];
                Array.Copy(spsPps, 0, payloadOut, 0, spsPps.Length);
                Array.Copy(payload, 0, payloadOut, spsPps.Length, payload.Length);
                h264Data.Data = payloadOut;
                h264Data.Length = payloadOut.Length;
            }
            else
            {
                h264Data.Data = payload;
                h264Data.Length = payload.Length;
            }

            h264Data.Pts = pts;
            h264Data.Width = widthSource;
            h264Data.Height = heightSource;
            _receiver.OnData(h264Data);

            _videoFrameCount++;
            if (containsIdr || _videoFrameCount % 120 == 0)
            {
                _receiver.OnDiagnostic(
                    $"H264 miroir: trame={_videoFrameCount}, NAL={naluCount}, type={h264Data.FrameType}, " +
                    $"taille={h264Data.Length}, pts={pts}");
            }
        }

        private void ProcessSpsPps(byte[] payload, out byte[] spsPps)
        {
            spsPps = null;
            if (payload == null || payload.Length < 11)
            {
                _receiver.OnDiagnostic("Configuration H264 miroir trop courte");
                return;
            }
            var h264 = new H264Codec();

            h264.Version = payload[0];
            h264.ProfileHigh = payload[1];
            h264.Compatibility = payload[2];
            h264.Level = payload[3];
            h264.Reserved6AndNal = payload[4];
            h264.Reserved3AndSps = payload[5];
            h264.LengthOfSps = (short)(((payload[6] & 255) << 8) + (payload[7] & 255));

            if (h264.LengthOfSps <= 0 || 8 + h264.LengthOfSps + 3 > payload.Length)
            {
                _receiver.OnDiagnostic($"Longueur SPS miroir invalide: {h264.LengthOfSps}/{payload.Length}");
                return;
            }

            var sequence = new byte[h264.LengthOfSps];
            Array.Copy(payload, 8, sequence, 0, h264.LengthOfSps);
            h264.SequenceParameterSet = sequence;
            h264.NumberOfPps = payload[h264.LengthOfSps + 8];
            h264.LengthOfPps = (short)(
                ((payload[h264.LengthOfSps + 9] & 255) << 8) |
                (payload[h264.LengthOfSps + 10] & 255));

            if (h264.LengthOfPps <= 0 ||
                h264.LengthOfSps + 11 + h264.LengthOfPps > payload.Length)
            {
                _receiver.OnDiagnostic($"Longueur PPS miroir invalide: {h264.LengthOfPps}/{payload.Length}");
                return;
            }

            var picture = new byte[h264.LengthOfPps];
            Array.Copy(payload, h264.LengthOfSps + 11, picture, 0, h264.LengthOfPps);
            h264.PictureParameterSet = picture;

            if (h264.LengthOfSps + h264.LengthOfPps < 102400)
            {
                var spsPpsLen = h264.LengthOfSps + h264.LengthOfPps + 8;
                spsPps = new byte[spsPpsLen];

                spsPps[0] = 0;
                spsPps[1] = 0;
                spsPps[2] = 0;
                spsPps[3] = 1;

                Array.Copy(h264.SequenceParameterSet, 0, spsPps, 4, h264.LengthOfSps);

                spsPps[h264.LengthOfSps + 4] = 0;
                spsPps[h264.LengthOfSps + 5] = 0;
                spsPps[h264.LengthOfSps + 6] = 0;
                spsPps[h264.LengthOfSps + 7] = 1;

                Array.Copy(h264.PictureParameterSet, 0, spsPps, h264.LengthOfSps + 8, h264.LengthOfPps);
            }
            else
            {
                spsPps = null;
            }
        }
    }
}
