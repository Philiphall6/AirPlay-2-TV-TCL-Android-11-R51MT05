namespace AirPlay.Models
{
    public sealed class NowPlayingInfo
    {
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Album { get; set; } = string.Empty;
        public byte[] Artwork { get; set; }
        public long ProgressStart { get; set; }
        public long ProgressCurrent { get; set; }
        public long ProgressEnd { get; set; }
        public bool IsPlaying { get; set; }
        public string ActiveRemote { get; set; } = string.Empty;
        public string DacpId { get; set; } = string.Empty;

        public NowPlayingInfo Clone() => new NowPlayingInfo
        {
            Title = Title,
            Artist = Artist,
            Album = Album,
            Artwork = Artwork == null ? null : (byte[])Artwork.Clone(),
            ProgressStart = ProgressStart,
            ProgressCurrent = ProgressCurrent,
            ProgressEnd = ProgressEnd,
            IsPlaying = IsPlaying,
            ActiveRemote = ActiveRemote,
            DacpId = DacpId
        };
    }
}
