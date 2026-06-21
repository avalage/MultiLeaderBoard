using System;
using System.IO;
using System.Runtime.CompilerServices;
using BeatLeader.Models;
using IPA.Config.Stores;
using IPA.Config.Stores.Attributes;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Version = Hive.Versioning.Version;

namespace BeatLeader {
    internal class ConfigFileData {
        #region Serialization

        private const string ConfigPath = "UserData\\BeatLeader.json";

        public static void Initialize() {
            if (File.Exists(ConfigPath)) {
                var text = File.ReadAllText(ConfigPath);

                try {
                    var instance = JsonConvert.DeserializeObject<ConfigFileData>(text);

                    Instance = instance ?? throw new Exception("A deserialized instance was null");

                    Plugin.Log.Debug("Config initialized");
                    return;
                } catch (Exception ex) {
                    Plugin.Log.Error($"Failed to load config (default will be used):\n{ex}");
                }
            }

            Instance = new();
        }

        public static void Save() {
            try {
                var text = JsonConvert.SerializeObject(
                    Instance,
                    Formatting.Indented,
                    new JsonSerializerSettings {
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                        Converters = {
                            new StringEnumConverter()
                        }
                    }
                );

                File.WriteAllText(ConfigPath, text);
                Plugin.Log.Debug("Config saved");
            } catch (Exception ex) {
                Plugin.Log.Error($"Failed to save configuration:\n{ex}");
            }
        }

        private static ConfigFileData? _instance;

        public static ConfigFileData Instance {
            get => _instance!;
            private set => _instance = value;
        }

        public static bool IsInitialized => _instance != null;

        #endregion

        #region Config
        
        public string LastSessionModVersion = Version.Zero.ToString();
        
        public bool Enabled = ConfigDefaults.Enabled;
        public bool NoticeboardEnabled = true;
        public bool MenuButtonEnabled = ConfigDefaults.MenuButtonEnabled;
        
        // Accessibility
        public BLLanguage SelectedLanguage = ConfigDefaults.SelectedLanguage;
        public BeatLeaderServer MainServer = ConfigDefaults.MainServer;
        
        // Leaderboard
        public LeaderboardDisplaySettings LeaderboardDisplaySettings = ConfigDefaults.LeaderboardDisplaySettings;
        public ScoreRowCellType LeaderboardTableMask = ConfigDefaults.LeaderboardTableMask;
        public int ScoresContext = ConfigDefaults.ScoresContext;
        public bool DarkGoldThemeEnabled = ConfigDefaults.DarkGoldThemeEnabled;
        public float LeaderboardOtherScoreBackgroundOpacity = ConfigDefaults.LeaderboardOtherScoreBackgroundOpacity;
        public bool ExperienceBarEnabled = true;
        public bool ScoreSubmissionsEnabled = ConfigDefaults.ScoreSubmissionsEnabled;
        public bool BeatLeaderScoreSubmissionEnabled = ConfigDefaults.BeatLeaderScoreSubmissionEnabled;
        public bool ScoreSaberScoreSubmissionEnabled = ConfigDefaults.ScoreSaberScoreSubmissionEnabled;

        // Hub
        public BeatLeaderHubTheme HubTheme = ConfigDefaults.HubTheme;
        
        // Replayer
        public ReplayerSettings ReplayerSettings = ConfigDefaults.ReplayerSettings;
        
        // Replays
        public bool SaveLocalReplays = ConfigDefaults.SaveLocalReplays;
        public bool OverrideOldReplays = ConfigDefaults.OverrideOldReplays;
        public ReplaySaveOption ReplaySavingOptions = ConfigDefaults.ReplaySavingOptions;

        #endregion
    }
}
