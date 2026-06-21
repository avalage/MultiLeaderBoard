using System;
using System.IO;
using IPA.Loader;

namespace BeatLeader.Utils {
    internal static class ScoreSubmissionManager {
        private const string BeatLeaderPluginId = "BeatLeader";
        private const string ScoreSaberPluginId = "ScoreSaber";
        private const string OriginalBeatLeaderFileName = "BeatLeader.dll";

        public static bool OriginalBeatLeaderInstalled { get; private set; }
        public static bool OriginalScoreSaberInstalled { get; private set; }

        public static bool ScoreSaberDirectUploadAvailable => false;

        public static bool BeatLeaderUploadEnabled =>
            PluginConfig.ScoreSubmissionsEnabled &&
            PluginConfig.BeatLeaderScoreSubmissionEnabled &&
            !OriginalBeatLeaderInstalled;

        public static bool ScoreSaberUploadEnabled =>
            PluginConfig.ScoreSubmissionsEnabled &&
            PluginConfig.ScoreSaberScoreSubmissionEnabled &&
            !OriginalScoreSaberInstalled &&
            ScoreSaberDirectUploadAvailable;

        public static void RefreshInstalledMods() {
            OriginalScoreSaberInstalled = PluginManager.GetPluginFromId(ScoreSaberPluginId) != null;
            OriginalBeatLeaderInstalled = HasOriginalBeatLeaderInstall();

            Plugin.Log.Info(
                $"[ScoreSubmission] Original mods: BeatLeader={OriginalBeatLeaderInstalled}, ScoreSaber={OriginalScoreSaberInstalled}. " +
                $"Effective upload: BeatLeader={BeatLeaderUploadEnabled}, ScoreSaber={ScoreSaberUploadEnabled}");
        }

        public static string FormatStatus() {
            var beatLeaderStatus = OriginalBeatLeaderInstalled ? "original mod installed" :
                PluginConfig.BeatLeaderScoreSubmissionEnabled ? "enabled" : "disabled";
            var scoreSaberStatus = OriginalScoreSaberInstalled ? "original mod installed" :
                ScoreSaberDirectUploadAvailable
                    ? (PluginConfig.ScoreSaberScoreSubmissionEnabled ? "enabled" : "disabled")
                    : "direct upload unavailable";

            return $"BL: {beatLeaderStatus}; SS: {scoreSaberStatus}";
        }

        private static bool HasOriginalBeatLeaderInstall() {
            var currentPath = NormalizePath(Plugin.Metadata?.File?.FullName);

            foreach (var plugin in PluginManager.EnabledPlugins) {
                if (!string.Equals(plugin.Id, BeatLeaderPluginId, StringComparison.OrdinalIgnoreCase)) continue;
                if (IsCurrentPlugin(plugin, currentPath)) continue;
                return true;
            }

            var pluginsDirectory = Plugin.Metadata?.File?.Directory;
            if (pluginsDirectory == null) return false;

            var originalFile = Path.Combine(pluginsDirectory.FullName, OriginalBeatLeaderFileName);
            if (!File.Exists(originalFile)) return false;

            return !string.Equals(NormalizePath(originalFile), currentPath, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCurrentPlugin(PluginMetadata metadata, string? currentPath) {
            if (ReferenceEquals(metadata, Plugin.Metadata)) return true;
            if (metadata.Assembly == typeof(Plugin).Assembly) return true;

            var metadataPath = NormalizePath(metadata.File?.FullName);
            return !string.IsNullOrEmpty(currentPath) &&
                !string.IsNullOrEmpty(metadataPath) &&
                string.Equals(metadataPath, currentPath, StringComparison.OrdinalIgnoreCase);
        }

        private static string? NormalizePath(string? path) {
            if (string.IsNullOrEmpty(path)) return path;

            try {
                return Path.GetFullPath(path);
            } catch {
                return path;
            }
        }
    }
}
