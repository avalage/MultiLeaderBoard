using System;
using BeatLeader.Models;

namespace BeatLeader {
    internal static class PluginConfig {
        private static void Save() {
            if (ConfigFileData.IsInitialized) ConfigFileData.Save();
        }

        #region Enabled

        public static bool Enabled {
            get => ConfigFileData.Instance.Enabled;
            set {
                if (ConfigFileData.Instance.Enabled == value) return;
                ConfigFileData.Instance.Enabled = value;
            }
        }

        #endregion

        #region Noticeboard

        public static bool NoticeboardEnabled {
            get => ConfigFileData.Instance.NoticeboardEnabled;
            set {
                if (ConfigFileData.Instance.NoticeboardEnabled == value) return;
                ConfigFileData.Instance.NoticeboardEnabled = value;
            }
        }

        #endregion

        #region MainServer

        public static BeatLeaderServer MainServer {
            get => ConfigFileData.Instance.MainServer;
            set {
                if (ConfigFileData.Instance.MainServer.Equals(value)) return;
                ConfigFileData.Instance.MainServer = value;
            }
        }

        #endregion

        #region ScoresContext

        public static event Action<int> ScoresContextChangedEvent;

        public static int ScoresContext {
            get => ConfigFileData.Instance.ScoresContext;
            set {
                if (ConfigFileData.Instance.ScoresContext.Equals(value)) return;
                ConfigFileData.Instance.ScoresContext = value;
                ScoresContextChangedEvent?.Invoke(value);
            }
        }

        public static event Action ScoresContextListChangedEvent;

        public static void NotifyScoresContextListWasChanged() {
            ScoresContextListChangedEvent?.Invoke();
        }

        #endregion

        #region LeaderboardTableMask

        public static event Action<ScoreRowCellType> LeaderboardTableMaskChangedEvent;

        public static ScoreRowCellType LeaderboardTableMask {
            get => ConfigFileData.Instance.LeaderboardTableMask;
            set {
                if (ConfigFileData.Instance.LeaderboardTableMask.Equals(value)) return;
                ConfigFileData.Instance.LeaderboardTableMask = value;
                Save();
                LeaderboardTableMaskChangedEvent?.Invoke(value);
            }
        }

        #endregion

        #region LeaderboardDisplaySettings

        public static event Action<LeaderboardDisplaySettings> LeaderboardDisplaySettingsChangedEvent;

        public static LeaderboardDisplaySettings LeaderboardDisplaySettings
        {
            get => ConfigFileData.Instance.LeaderboardDisplaySettings;
            set
            {
                ConfigFileData.Instance.LeaderboardDisplaySettings = value;
                Save();
                LeaderboardDisplaySettingsChangedEvent?.Invoke(value);
            }
        }

        public static RankedStarsDisplayMode RankedStarsDisplayMode
        {
            get => LeaderboardDisplaySettings.RankedStarsDisplayMode;
            set
            {
                var settings = LeaderboardDisplaySettings;
                if (settings.RankedStarsDisplayMode == value) return;

                LeaderboardDisplaySettings = new LeaderboardDisplaySettings
                {
                    ClanCaptureDisplay = settings.ClanCaptureDisplay,
                    RankedStarsDisplayMode = value,
                    AccSaberProfileStatsDisplay = settings.AccSaberProfileStatsDisplay
                };
            }
        }

        public static bool AccSaberProfileStatsDisplay
        {
            get => LeaderboardDisplaySettings.AccSaberProfileStatsDisplay;
            set
            {
                var settings = LeaderboardDisplaySettings;
                if (settings.AccSaberProfileStatsDisplay == value) return;

                LeaderboardDisplaySettings = new LeaderboardDisplaySettings
                {
                    ClanCaptureDisplay = settings.ClanCaptureDisplay,
                    RankedStarsDisplayMode = settings.RankedStarsDisplayMode,
                    AccSaberProfileStatsDisplay = value
                };
            }
        }

        #endregion

        #region DarkGoldTheme

        public static bool DarkGoldThemeEnabled {
            get => ConfigFileData.Instance.DarkGoldThemeEnabled;
            set {
                if (ConfigFileData.Instance.DarkGoldThemeEnabled == value) return;
                ConfigFileData.Instance.DarkGoldThemeEnabled = value;
            }
        }

        #endregion

        #region LeaderboardOtherScoreBackgroundOpacity

        public static event Action<float> LeaderboardOtherScoreBackgroundOpacityChangedEvent;

        public static float LeaderboardOtherScoreBackgroundOpacity {
            get => ConfigFileData.Instance.LeaderboardOtherScoreBackgroundOpacity;
            set {
                value = Math.Max(0.0f, Math.Min(1.0f, value));
                if (Math.Abs(ConfigFileData.Instance.LeaderboardOtherScoreBackgroundOpacity - value) < 0.001f) return;
                ConfigFileData.Instance.LeaderboardOtherScoreBackgroundOpacity = value;
                Save();
                LeaderboardOtherScoreBackgroundOpacityChangedEvent?.Invoke(value);
            }
        }

        #endregion

        #region ScoreSubmission

        public static event Action ScoreSubmissionSettingsChangedEvent;

        public static bool ScoreSubmissionsEnabled {
            get => ConfigFileData.Instance.ScoreSubmissionsEnabled;
            set {
                if (ConfigFileData.Instance.ScoreSubmissionsEnabled == value) return;
                ConfigFileData.Instance.ScoreSubmissionsEnabled = value;
                Save();
                ScoreSubmissionSettingsChangedEvent?.Invoke();
            }
        }

        public static bool ScoreSubmissionsAutoDisabledByConflict {
            get => ConfigFileData.Instance.ScoreSubmissionsAutoDisabledByConflict;
            set {
                if (ConfigFileData.Instance.ScoreSubmissionsAutoDisabledByConflict == value) return;
                ConfigFileData.Instance.ScoreSubmissionsAutoDisabledByConflict = value;
                Save();
            }
        }

        public static bool BeatLeaderScoreSubmissionEnabled {
            get => ConfigFileData.Instance.BeatLeaderScoreSubmissionEnabled;
            set {
                if (ConfigFileData.Instance.BeatLeaderScoreSubmissionEnabled == value) return;
                ConfigFileData.Instance.BeatLeaderScoreSubmissionEnabled = value;
                Save();
                ScoreSubmissionSettingsChangedEvent?.Invoke();
            }
        }

        public static bool ScoreSaberScoreSubmissionEnabled {
            get => ConfigFileData.Instance.ScoreSaberScoreSubmissionEnabled;
            set {
                if (ConfigFileData.Instance.ScoreSaberScoreSubmissionEnabled == value) return;
                ConfigFileData.Instance.ScoreSaberScoreSubmissionEnabled = value;
                Save();
                ScoreSubmissionSettingsChangedEvent?.Invoke();
            }
        }

        #endregion

        #region ReplayerSettings

        public static event Action<ReplayerSettings> ReplayerSettingsChangedEvent;

        public static ReplayerSettings ReplayerSettings {
            get => ConfigFileData.Instance.ReplayerSettings;
            set {
                ConfigFileData.Instance.ReplayerSettings = value;
                ReplayerSettingsChangedEvent?.Invoke(value);
            }
        }

        public static void NotifyReplayerSettingsChanged() {
            ReplayerSettingsChangedEvent?.Invoke(ReplayerSettings);
        }

        #endregion

        #region Language

        public static BLLanguage SelectedLanguage {
            get => ConfigFileData.Instance.SelectedLanguage;
            set => ConfigFileData.Instance.SelectedLanguage = value;
        }

        #endregion
    }
}
