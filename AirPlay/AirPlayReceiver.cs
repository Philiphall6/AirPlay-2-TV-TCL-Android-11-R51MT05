using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AirPlay.Listeners;
using AirPlay.Models;
using AirPlay.Models.Configs;
using Makaretu.Dns;
using Microsoft.Extensions.Options;

namespace AirPlay
{
    public class AirPlayReceiver : IRtspReceiver, IAirPlayReceiver
    {
        public event EventHandler<decimal> OnSetVolumeReceived;
        public event EventHandler<H264Data> OnH264DataReceived;
        public event EventHandler<PcmData> OnPCMDataReceived;
        public event EventHandler<string> OnDiagnosticReceived;

        public const string AirPlayType = "_airplay._tcp";
        public const string AirTunesType = "_raop._tcp";
        private const string AudioFeatures = "0x483FDA00,0x0";
        private const string ReceiverPublicKey = "29fbb183a58b466e05b9ab667b3c429d18a6b785637333d3f0f3a34baa89f45e";

        private MulticastService _mdns = null;
        private AirTunesListener _airTunesListener = null;
        private AirTunesListener _airPlayListener = null;
        private readonly string _instance;
        private readonly string _audioInstance;
        private readonly string _videoInstance;
        private readonly ushort _airTunesPort;
        private readonly ushort _airPlayPort;
        private readonly ushort _airPlayDataPort;
        private readonly string _deviceId;
        private readonly string _audioDeviceId;

        public AirPlayReceiver(IOptions<AirPlayReceiverConfig> aprConfig, IOptions<CodecLibrariesConfig> codecConfig, IOptions<DumpConfig> dumpConfig)
        {
            _airTunesPort = aprConfig?.Value?.AirTunesPort ?? 5000;
            _airPlayPort = aprConfig?.Value?.AirPlayPort ?? 7000;
            _airPlayDataPort = aprConfig?.Value?.AirPlayDataPort ?? 7100;
            _deviceId = aprConfig?.Value?.DeviceMacAddress ?? "11:22:33:44:55:66";
            _instance = aprConfig?.Value?.Instance ?? throw new ArgumentNullException("apr.instance");
            _audioInstance = aprConfig?.Value?.AudioInstance ?? $"{_instance} Audio";
            _videoInstance = aprConfig?.Value?.VideoInstance ?? $"{_instance} Vidéo";
            _audioDeviceId = aprConfig?.Value?.AudioDeviceMacAddress ?? _deviceId;

            var clConfig = codecConfig?.Value ?? throw new ArgumentNullException(nameof(codecConfig));
            var dConfig = dumpConfig?.Value ?? throw new ArgumentNullException(nameof(dumpConfig));

            // SteeBono's original receiver advertises 7000 but only listens on 5000.
            // Keep its RTSP engine on both advertised control ports and reserve a
            // distinct port for the H.264 payload returned by the type 110 SETUP.
            _airTunesListener = new AirTunesListener(this, _airTunesPort, _airPlayDataPort, clConfig, dConfig);
            _airPlayListener = new AirTunesListener(this, _airPlayPort, _airPlayDataPort, clConfig, dConfig);
        }

        public async Task StartListeners(CancellationToken cancellationToken)
        {
            await _airTunesListener.StartAsync(cancellationToken).ConfigureAwait(false);
            await _airPlayListener.StartAsync(cancellationToken).ConfigureAwait(false);
        }

        public Task StartMdnsAsync()
        {
            if (string.IsNullOrWhiteSpace(_deviceId))
            {
                throw new ArgumentNullException(_deviceId);
            }

            var rDeviceId = new Regex("^(([0-9a-fA-F][0-9a-fA-F]):){5}([0-9a-fA-F][0-9a-fA-F])$");
            var mDeviceId = rDeviceId.Match(_deviceId);
            if (!mDeviceId.Success)
            {
                throw new ArgumentException("Device id must be a mac address", _deviceId);
            }

            var audioDeviceMatch = rDeviceId.Match(_audioDeviceId);
            if (!audioDeviceMatch.Success)
            {
                throw new ArgumentException("Audio device id must be a mac address", _audioDeviceId);
            }

            var deviceIdInstance = string.Join(string.Empty, audioDeviceMatch.Groups[2].Captures) + audioDeviceMatch.Groups[3].Value;

            _mdns = new MulticastService();
            var sd = new ServiceDiscovery(_mdns);

            foreach (var ip in MulticastService.GetIPAddresses())
            {
                Console.WriteLine($"IP address {ip}");
            }

            _mdns.NetworkInterfaceDiscovered += (s, e) =>
            {
                foreach (var nic in e.NetworkInterfaces)
                {
                    Console.WriteLine($"NIC '{nic.Name}'");
                }
            };

            // Internally 'ServiceProfile' create the SRV record
            var airTunes = new ServiceProfile($"{deviceIdInstance}@{_audioInstance}", AirTunesType, _airTunesPort);
            airTunes.AddProperty("ch", "2");
            airTunes.AddProperty("cn", "0,1,2,3");
            airTunes.AddProperty("et", "0,3,5");
            airTunes.AddProperty("md", "0,1,2");
            airTunes.AddProperty("sr", "44100");
            airTunes.AddProperty("ss", "16");
            airTunes.AddProperty("pw", "false");
            airTunes.AddProperty("sm", "false");
            airTunes.AddProperty("ek", "1");
            airTunes.AddProperty("da", "true");
            airTunes.AddProperty("sv", "false");
            airTunes.AddProperty("ft", AudioFeatures);
            airTunes.AddProperty("am", "AppleTV5,3");
            airTunes.AddProperty("pk", ReceiverPublicKey);
            airTunes.AddProperty("sf", "0x4");
            airTunes.AddProperty("tp", "UDP");
            airTunes.AddProperty("vn", "65537");
            airTunes.AddProperty("vs", "220.68");
            airTunes.AddProperty("vv", "2");

            /*
             * ch	2	audio channels: stereo
             * cn	0,1,2,3	audio codecs
             * et	0,3,5	supported encryption types
             * md	0,1,2	supported metadata types
             * pw	false	does the speaker require a password?
             * sr	44100	audio sample rate: 44100 Hz
             * ss	16	audio sample size: 16-bit
             */

            // Recent Apple senders discover audio targets through a matching
            // _airplay companion in addition to the legacy _raop profile.
            // Keep screen/video feature bits clear so it remains audio-only.
            var audioAirPlay = new ServiceProfile(_audioInstance, AirPlayType, _airTunesPort);
            audioAirPlay.AddProperty("deviceid", _audioDeviceId);
            audioAirPlay.AddProperty("features", AudioFeatures);
            audioAirPlay.AddProperty("flags", "0x4");
            audioAirPlay.AddProperty("model", "AppleTV5,3");
            audioAirPlay.AddProperty("pk", ReceiverPublicKey);
            audioAirPlay.AddProperty("pi", "aa072a95-0318-4ec3-b042-4992495877d4");
            audioAirPlay.AddProperty("srcvers", "220.68");
            audioAirPlay.AddProperty("vv", "2");

            // Internally 'ServiceProfile' create the SRV record
            var airPlay = new ServiceProfile(_videoInstance, AirPlayType, _airPlayPort);
            airPlay.AddProperty("deviceid", _deviceId);
            airPlay.AddProperty("features", "0x5A7FFFF7,0x1E"); // 0x4A7FFFF7
            airPlay.AddProperty("flags", "0x4");
            airPlay.AddProperty("model", "AppleTV5,3");
            airPlay.AddProperty("pk", ReceiverPublicKey);
            airPlay.AddProperty("pi", "aa072a95-0318-4ec3-b042-4992495877d3");
            airPlay.AddProperty("srcvers", "220.68");
            airPlay.AddProperty("vv", "2");

            sd.Advertise(airTunes);
            sd.Advertise(audioAirPlay);
            sd.Advertise(airPlay);

            _mdns.Start();

            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            if (_airTunesListener != null)
            {
                await _airTunesListener.StopAsync().ConfigureAwait(false);
            }

            if (_airPlayListener != null)
            {
                await _airPlayListener.StopAsync().ConfigureAwait(false);
            }

            if (_mdns != null)
            {
                _mdns.Stop();
                _mdns.Dispose();
                _mdns = null;
            }
        }

        public void OnSetVolume(decimal volume)
        {
            OnSetVolumeReceived?.Invoke(this, volume);
        }

        public void OnData(H264Data data)
        {
            OnH264DataReceived?.Invoke(this, data);
        }

        public void OnPCMData(PcmData data)
        {
            OnPCMDataReceived?.Invoke(this, data);
        }

        public void OnDiagnostic(string message)
        {
            OnDiagnosticReceived?.Invoke(this, message);
        }
    }
}
