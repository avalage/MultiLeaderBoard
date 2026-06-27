using IPA.Loader;

namespace BeatLeader {
    internal static class GuildSaberCompatibilityPatches {
        private const string GuildSaberPluginId = "GuildSaber";

        private static bool _applied;

        public static void ApplyRuntimePatches() {
            if (_applied) return;

            var guildSaberMetadata = PluginManager.GetPluginFromId(GuildSaberPluginId);
            if (guildSaberMetadata == null) {
                Plugin.Log.Debug("[GuildSaberCompat] GuildSaber assembly is not loaded yet.");
            } else {
                Plugin.Log.Info("[GuildSaberCompat] GuildSaber detected; keeping its configured API environment.");
            }

            _applied = true;
        }
    }
}
