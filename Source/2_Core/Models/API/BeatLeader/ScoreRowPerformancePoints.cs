namespace BeatLeader.Models {
    public readonly struct ScoreRowPerformancePoints {
        public readonly float BeatLeaderValue;
        public readonly bool HasBeatLeaderValue;
        public readonly float ScoreSaberValue;
        public readonly bool HasScoreSaberValue;
        public readonly float AccSaberValue;
        public readonly bool HasAccSaberValue;

        public ScoreRowPerformancePoints(
            float beatLeaderValue,
            bool hasBeatLeaderValue,
            float scoreSaberValue,
            bool hasScoreSaberValue,
            float accSaberValue = 0.0f,
            bool hasAccSaberValue = false
        ) {
            BeatLeaderValue = beatLeaderValue;
            HasBeatLeaderValue = hasBeatLeaderValue;
            ScoreSaberValue = scoreSaberValue;
            HasScoreSaberValue = hasScoreSaberValue;
            AccSaberValue = accSaberValue;
            HasAccSaberValue = hasAccSaberValue;
        }
    }
}
