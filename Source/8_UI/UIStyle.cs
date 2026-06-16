using Reactive.BeatSaber;
using Reactive.Components;
using UnityEngine;

namespace BeatLeader.UI {
    internal static class UIStyle {
        public static float Skew => BeatSaberStyle.Skew;
        public static Color SecondaryTextColor => BeatSaberStyle.SecondaryTextColor;

        public static SimpleColorSet InputColorSet => BeatSaberStyle.InputColorSet;
        public static SimpleColorSet ControlColorSet => BeatSaberStyle.ControlColorSet;
        public static SimpleColorSet ControlButtonColorSet => BeatSaberStyle.ControlButtonColorSet;

        public static ReadOnlyColorSet GlowingButtonColorSet => BeatLeaderDarkGoldTheme.GlowingButtonColorSet;
    }
}
