namespace BeatLeader.Models
{
    public enum RankedStarsDisplayMode
    {
        Both,
        ScoreSaberOnly,
        BeatLeaderOnly,
        AccSaberOnly,
        All
    }

    public class LeaderboardDisplaySettings
    {
        public bool ClanCaptureDisplay { get; set; }
        public RankedStarsDisplayMode RankedStarsDisplayMode { get; set; } = RankedStarsDisplayMode.Both;
    }
}
