using System;
using System.Collections.Generic;
using System.Text;

namespace AirPlay.Models.Configs
{
    public class AirPlayReceiverConfig
    {
        public string Instance { get; set; }
        public string AudioInstance { get; set; }
        public string VideoInstance { get; set; }
        public ushort AirTunesPort { get; set; }
        public ushort AirPlayPort { get; set; }
        public ushort AirPlayDataPort { get; set; }
        public string DeviceMacAddress { get; set; }
        public string AudioDeviceMacAddress { get; set; }
    }
}
