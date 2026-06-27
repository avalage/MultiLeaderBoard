using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using BeatLeader.ViewControllers;
using HMUI;
using JetBrains.Annotations;
using LeaderboardCore.Managers;
using LeaderboardCore.Models;
using UnityEngine;
using Zenject;

namespace BeatLeader {
    [UsedImplicitly]
    internal class BeatLeaderCustomLeaderboard : CustomLeaderboard, IInitializable, IDisposable {
        #region Inject

        private readonly CustomLeaderboardManager _customLeaderboardManager;
        private readonly LeaderboardPanel _leaderboardPanel;
        private readonly LeaderboardView _leaderboardView;

        public BeatLeaderCustomLeaderboard(
            CustomLeaderboardManager customLeaderboardManager,
            LeaderboardPanel panelViewController,
            LeaderboardView leaderboardViewController
        ) {
            _customLeaderboardManager = customLeaderboardManager;
            _leaderboardPanel = panelViewController;
            _leaderboardView = leaderboardViewController;
        }

        #endregion

        #region CustomLeaderboard Implementation

        protected override ViewController panelViewController => _leaderboardPanel;
        protected override ViewController leaderboardViewController => _leaderboardView;

        public override bool ShowForLevel(BeatmapKey? selectedLevel) {
            return true;
        }
        protected override string leaderboardId => "MultiLeaderboard";

        #endregion

        #region Initialize & Dispose   (Register/UnRegister)

        public void Initialize() {
            _customLeaderboardManager.Register(this);
            LogRegisteredLeaderboards(_customLeaderboardManager, "initialize");
            LeaderboardCoreDiagnosticsRunner.Schedule(_customLeaderboardManager);
        }

        public void Dispose() {
            _customLeaderboardManager.Unregister(this);
        }

        #endregion

        #region Diagnostics

        private static void LogRegisteredLeaderboards(CustomLeaderboardManager customLeaderboardManager, string phase) {
            try {
                var field = typeof(CustomLeaderboardManager).GetField("customLeaderboardsById", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field?.GetValue(customLeaderboardManager) is not IDictionary leaderboardsById) {
                    Plugin.Log.Info($"[LeaderboardCore] Registered MultiLeaderboard ({phase}); unable to inspect registered leaderboard ids.");
                    LogManagerShape(customLeaderboardManager, phase);
                    return;
                }

                var ids = leaderboardsById.Keys.Cast<object?>().Select(key => key?.ToString() ?? "<null>").ToArray();
                Plugin.Log.Info($"[LeaderboardCore] Registered MultiLeaderboard ({phase}); ids=[{string.Join(", ", ids)}]");
            } catch (Exception ex) {
                Plugin.Log.Warn($"[LeaderboardCore] Registered MultiLeaderboard ({phase}); failed to inspect leaderboard ids: {ex.Message}");
                LogManagerShape(customLeaderboardManager, phase);
            }
        }

        private static void LogManagerShape(CustomLeaderboardManager customLeaderboardManager, string phase) {
            try {
                var fields = typeof(CustomLeaderboardManager)
                    .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Select(field => {
                        var value = field.GetValue(customLeaderboardManager);
                        if (value is IDictionary dictionary) {
                            var keys = dictionary.Keys.Cast<object?>().Select(key => key?.ToString() ?? "<null>").ToArray();
                            return $"{field.Name}:{value.GetType().Name}[{string.Join(", ", keys)}]";
                        }

                        return $"{field.Name}:{value?.GetType().Name ?? "<null>"}";
                    });

                Plugin.Log.Info($"[LeaderboardCore] Manager shape ({phase}); fields={string.Join("; ", fields)}");
            } catch (Exception ex) {
                Plugin.Log.Warn($"[LeaderboardCore] Failed to inspect manager shape ({phase}): {ex.Message}");
            }
        }

        private sealed class LeaderboardCoreDiagnosticsRunner : MonoBehaviour {
            private CustomLeaderboardManager _customLeaderboardManager = null!;

            public static void Schedule(CustomLeaderboardManager customLeaderboardManager) {
                var gameObject = new GameObject("MultiLeaderboardLeaderboardCoreDiagnostics");
                DontDestroyOnLoad(gameObject);
                gameObject.AddComponent<LeaderboardCoreDiagnosticsRunner>()._customLeaderboardManager = customLeaderboardManager;
            }

            private IEnumerator Start() {
                yield return new WaitForSecondsRealtime(1.0f);
                LogRegisteredLeaderboards(_customLeaderboardManager, "after 1s");
                yield return new WaitForSecondsRealtime(4.0f);
                LogRegisteredLeaderboards(_customLeaderboardManager, "after 5s");
                Destroy(gameObject);
            }
        }

        #endregion
    }
}
