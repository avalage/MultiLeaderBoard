namespace BeatLeader.Models {
    public readonly struct ScoreRowPerformancePoints {
        public readonly float BeatLeaderValue;
        public readonly bool HasBeatLeaderValue;
        public readonly float ScoreSaberValue;
        public readonly bool HasScoreSaberValue;

        public ScoreRowPerformancePoints(float beatLeaderValue, bool hasBeatLeaderValue, float scoreSaberValue, bool hasScoreSaberValue) {
            BeatLeaderValue = beatLeaderValue;
            HasBeatLeaderValue = hasBeatLeaderValue;
            ScoreSaberValue = scoreSaberValue;
            HasScoreSaberValue = hasScoreSaberValue;
        }
    }
}
