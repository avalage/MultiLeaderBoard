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

        private const string ConfigPath = "UserData\\MultiLeaderboard.json";
        private const string LegacyConfigPath = "UserData\\BeatLeader.json";

        public static void Initialize() {
            if (TryLoad(ConfigPath, out var instance)) {
                Instance = instance!;
                return;
            }

            if (!File.Exists(ConfigPath) && TryLoad(LegacyConfigPath, out instance)) {
                Instance = instance!;
                Plugin.Log.Info("Migrated BeatLeader config to MultiLeaderboard config.");
                Save();
                return;
            }

            Instance = new();
        }

        private static bool TryLoad(string path, out ConfigFileData? instance) {
            instance = null;
            if (!File.Exists(path)) return false;

            var text = File.ReadAllText(path);

            try {
                instance = JsonConvert.DeserializeObject<ConfigFileData>(text);
                if (instance == null) throw new Exception("A deserialized instance was null");

                Plugin.Log.Debug($"Config initialized from {path}");
                return true;
            } catch (Exception ex) {
                Plugin.Log.Error($"Failed to load config from {path}:\n{ex}");
                return false;
            }
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

                var directory = Path.GetDirectoryName(ConfigPath);
                if (!string.IsNullOrEmpty(directory)) {
                    Directory.CreateDirectory(directory);
                }

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
        public bool ScoreSubmissionsAutoDisabledByConflict = ConfigDefaults.ScoreSubmissionsAutoDisabledByConflict;
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
