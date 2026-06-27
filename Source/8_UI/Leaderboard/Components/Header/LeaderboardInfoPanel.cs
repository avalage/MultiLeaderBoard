using System;
using System.Collections;
using System.Collections.Generic;
using BeatLeader.DataManager;
using BeatLeader.Manager;
using BeatLeader.Models;
using BeatLeader.UI;
using BeatLeader.UI.Hub;
using BeatLeader.UI.Reactive.Components;
using BeatLeader.UIPatches;
using BeatLeader.Utils;
using ModestTree;
using Reactive;
using Reactive.BeatSaber.Components;
using Reactive.Components;
using Reactive.Yoga;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace BeatLeader.Components {
    internal class LeaderboardInfoPanel : ReactiveComponent {
        #region Construct

        private ReeWrapperV2<QualificationCheckbox> _criteriaCheckbox = null!;
        private ReeWrapperV2<QualificationCheckbox> _approvalCheckbox = null!;
        private CaptorClan _captorClan = null!;
        private MapStatus _mapStatus = null!;
        private RankedStarsModeButton _rankedStarsModeButton = null!;

        private MapTypePanel _mapTypePanel1 = null!;
        private MapTypePanel _mapTypePanel2 = null!;
        private MapTypePanel _mapTypePanel3 = null!;
        private List<MapTypePanel>? _mapTypePanels = null;
        private YogaModifier _rankedStarsPanelModifier = null!;
        private const float RankedStarsPanelBothOffset = 8.0f;
        private const float RankedStarsPanelSingleSourceOffset = 10.0f;
        private const float RankedStarsPanelTripleAllClearance = 5.0f;

        private DownloadScoresModal _downloadModal = null!;
        private Label _replaysLabel = null!;
        private BsButton _proceedButton = null!;

        private ImageButton _menuButton = null!;
        private PushContainer _container = null!;

        private static ImageButton CreateHeaderButton(Sprite sprite, Action callback) {
            return new ImageButton {
                Image = {
                    Sprite = sprite,
                    Material = BundleLoader.UIAdditiveGlowMaterial
                },
                Colors = BeatLeaderDarkGoldTheme.GlowingButtonColorSet,
                OnClick = callback
            }.AsFlexItem(size: 4f);
        }

        protected override GameObject Construct() {
            _downloadModal = new DownloadScoresModal {
                DownloadingFinishedCallback = OnScoreDownloadingFinished
            }.WithAlphaAnimation(() => Canvas!.gameObject).WithJumpAnimation();

            return new PushContainer {
                OpenedView = new Layout {
                    Children = {
                        new Layout {
                            Children = {
                                CreateHeaderButton(
                                    BundleLoader.Sprites.homeIcon,
                                    LeaderboardEvents.NotifyMenuButtonWasPressed
                                ).Bind(ref _menuButton),
                            }
                        }.AsFlexItem(flexGrow: 1f).AsFlexGroup(justifyContent: Justify.FlexStart),

                        new Layout {
                            Children = {
                                new ReeWrapperV2<RankedStarsModeButton>().BindRee(ref _rankedStarsModeButton),

                                new ReeWrapperV2<MapStatus>().BindRee(ref _mapStatus),

                                new ReeWrapperV2<MapTypePanel>().BindRee(ref _mapTypePanel1),
                                new ReeWrapperV2<MapTypePanel>().BindRee(ref _mapTypePanel2),
                                new ReeWrapperV2<MapTypePanel>().BindRee(ref _mapTypePanel3),

                                new ReeWrapperV2<CaptorClan>().BindRee(ref _captorClan),

                                new ReeWrapperV2<QualificationCheckbox>().Bind(ref _criteriaCheckbox),

                                new ReeWrapperV2<QualificationCheckbox>().Bind(ref _approvalCheckbox),
                            }
                        }.With(
                            x => {
                                var group = x.Content.AddComponent<HorizontalLayoutGroup>();
                                group.childForceExpandWidth = false;
                                group.childForceExpandHeight = false;
                                group.childControlHeight = false;
                                group.childAlignment = TextAnchor.MiddleCenter;
                                group.spacing = 1f;

                                var fitter = x.Content.AddComponent<ContentSizeFitter>();
                                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                            }
                        ).AsFlexItem(modifier: out _rankedStarsPanelModifier),

                        new Layout {
                            Children = {
                                CreateHeaderButton(
                                    BundleLoader.BattleRoyaleIcon,
                                    () => {
                                        if (LeaderboardState.leaderboardType != LeaderboardType.SongDiffPlayerScores) return;
                                        SetBattleRoyaleEnabled(true);
                                    }),

                                CreateHeaderButton(
                                    BundleLoader.ProfileIcon,
                                    () => {
                                        if (_websiteLink == null) return;
                                        EnvironmentUtils.OpenBrowserPage(_websiteLink);
                                    }
                                ),

                                CreateHeaderButton(
                                    BundleLoader.SettingsIcon,
                                    LeaderboardEvents.NotifyLeaderboardSettingsButtonWasPressed
                                )
                            }
                        }.AsFlexItem(flexGrow: 1f).AsFlexGroup(justifyContent: Justify.FlexEnd, gap: 1f)
                    }
                }.AsFlexGroup(
                    alignItems: Align.Center,
                    padding: new() { left = 1f, right = 2f },
                    gap: 1f
                ).WithRectExpand(),

                ClosedView = new Layout {
                    Children = {
                        new BsButton {
                            Text = "Cancel",
                            ShowUnderline = false,
                            OnClick = () => SetBattleRoyaleEnabled(false)
                        }.AsFlexItem(),

                        new Label {
                            FontSize = 5f
                        }.AsFlexItem().Bind(ref _replaysLabel),

                        new BsButton {
                            Text = "Proceed",
                            ShowUnderline = false,
                            OnClick = () => {
                                _downloadModal.SetData(_selectedScores);
                                ModalSystem.PresentModal(_downloadModal, Canvas!.transform);
                            }
                        }.AsFlexItem().Bind(ref _proceedButton),
                    }
                }.AsFlexGroup(
                    alignItems: Align.Center,
                    padding: new() { left = 1f, right = 2f },
                    justifyContent: Justify.SpaceBetween
                ).WithRectExpand(),

                Opened = true,
                Color = Color.clear
            }.Bind(ref _container).Use();
        }

        #endregion

        #region Init/Dispose

        private ReplayerViewNavigatorWrapper? _replayerNavigator;

        public void Setup(ReplayerViewNavigatorWrapper navigator) {
            _replayerNavigator = navigator;
        }

        protected override void OnInitialize() {
            _rankedStarsModeButton.OnClick = CycleRankedStarsDisplayMode;
            RefreshRankedStarsModeButton();

            LeaderboardsCache.CacheWasChangedEvent += OnCacheWasChanged;
            PluginConfig.LeaderboardDisplaySettingsChangedEvent += OnLeaderboardDisplaySettingsChanged;
            EnvironmentManagerPatch.EnvironmentTypeChangedEvent += OnMenuEnvironmentChanged;
            LeaderboardEvents.ScoreInfoButtonWasPressed += OnScoreClicked;

            LeaderboardState.AddSelectedBeatmapListener(OnSelectedBeatmapWasChanged);
        }

        protected override void OnDestroy() {
            CancelScoreSaberInfoRequest();
            CancelAccSaberInfoRequest();

            LeaderboardsCache.CacheWasChangedEvent -= OnCacheWasChanged;
            EnvironmentManagerPatch.EnvironmentTypeChangedEvent -= OnMenuEnvironmentChanged;
            PluginConfig.LeaderboardDisplaySettingsChangedEvent -= OnLeaderboardDisplaySettingsChanged;
            LeaderboardEvents.ScoreInfoButtonWasPressed -= OnScoreClicked;

            LeaderboardState.RemoveSelectedBeatmapListener(OnSelectedBeatmapWasChanged);
        }

        #endregion

        #region Battle Royale

        private readonly HashSet<Score> _selectedScores = new();
        private bool _battleRoyaleEnabled;

        private void SetBattleRoyaleEnabled(bool enabled) {
            _battleRoyaleEnabled = enabled;
            _container.Opened = !enabled;
            LeaderboardEvents.NotifyBattleRoyaleEnabled(enabled);

            if (enabled) {
                _selectedScores.Clear();
                RefreshBattleRoyaleUI();
            }
        }

        private void RefreshBattleRoyaleUI() {
            _replaysLabel.Text = $"{_selectedScores.Count} OPPONENTS";
            _proceedButton.Interactable = _selectedScores.Count > 1;
        }

        private void OnScoreClicked(Score score) {
            if (!_battleRoyaleEnabled) {
                return;
            }

            if (_selectedScores.Contains(score)) {
                _selectedScores.Remove(score);
            } else {
                _selectedScores.Add(score);
            }

            RefreshBattleRoyaleUI();
        }

        private void OnScoreDownloadingFinished() {
            var level = new BeatmapLevelWithKey(
                LeaderboardState.SelectedBeatmapLevel,
                LeaderboardState.SelectedBeatmapKey
            );

            _replayerNavigator?.NavigateToBattleRoyale(level, _downloadModal.Headers, false, true);
        }

        #endregion

        #region Events

        private void OnLeaderboardDisplaySettingsChanged(LeaderboardDisplaySettings settings) {
            _displayCaptorClan = settings.ClanCaptureDisplay;
            RefreshRankedStarsModeButton();
            UpdateVisuals();
        }

        private void OnMenuEnvironmentChanged(MenuEnvironmentManager.MenuEnvironmentType type) {
            UpdateVisuals();
        }

        private void OnSelectedBeatmapWasChanged(bool selectedAny, LeaderboardKey leaderboardKey, BeatmapKey key, BeatmapLevel level) {
            _selectedScores.Clear();
            RefreshBattleRoyaleUI();
            SetBeatmap(key);
        }

        private void OnCacheWasChanged() {
            SetBeatmap(LeaderboardState.SelectedBeatmapKey);
        }

        #endregion

        #region SetBeatmap

        private RankedStatus _rankedStatus;
        private int _mapType;
        private DiffInfo _difficultyInfo;
        private ScoreSaberLeaderboardInfo _scoreSaberLeaderboardInfo = ScoreSaberLeaderboardInfo.Unknown;
        private Coroutine? _scoreSaberInfoCoroutine;
        private MonoBehaviour? _scoreSaberInfoCoroutineRunner;
        private int _scoreSaberInfoRequestId;
        private UnityWebRequest? _activeScoreSaberInfoRequest;
        private AccSaberDifficultyInfo _accSaberDifficultyInfo = AccSaberDifficultyInfo.Unknown;
        private Coroutine? _accSaberInfoCoroutine;
        private MonoBehaviour? _accSaberInfoCoroutineRunner;
        private int _accSaberInfoRequestId;
        private UnityWebRequest? _activeAccSaberInfoRequest;
        private bool _displayCaptorClan = PluginConfig.LeaderboardDisplaySettings.ClanCaptureDisplay;
        private string? _websiteLink;
        private static CoroutineRunner? _coroutineRunner;

        private void SetBeatmap(BeatmapKey beatmap) {
            CancelScoreSaberInfoRequest();
            CancelAccSaberInfoRequest();
            _scoreSaberLeaderboardInfo = ScoreSaberLeaderboardInfo.Unknown;
            _accSaberDifficultyInfo = AccSaberDifficultyInfo.Unknown;

            if (!beatmap.IsValid()) {
                _rankedStatus = RankedStatus.Unknown;
                _mapType = 0;
                _difficultyInfo = default;
                _websiteLink = null;
                UpdateVisuals();
                return;
            }

            var key = LeaderboardKey.FromBeatmap(beatmap);
            RequestScoreSaberInfo(key);
            RequestAccSaberInfo(key);

            if (!LeaderboardsCache.TryGetLeaderboardInfo(key, out var data)) {
                _rankedStatus = RankedStatus.Unknown;
                _mapType = 0;
                _difficultyInfo = default;
                _websiteLink = null;
                UpdateVisuals();
                return;
            }

            _difficultyInfo = data.DifficultyInfo;
            _rankedStatus = FormatUtils.GetRankedStatus(data.DifficultyInfo);
            _mapType = data.DifficultyInfo.type;
            _websiteLink = BLConstants.LeaderboardPage(data.LeaderboardId);
            if (_rankedStatus is RankedStatus.Ranked) {
                _captorClan.SetValues(data);
            }

            UpdateCheckboxes(data.QualificationInfo);
            UpdateVisuals();
        }

        #endregion

        #region ScoreSaberInfo

        private void RequestScoreSaberInfo(LeaderboardKey leaderboardKey) {
            if (!ScoreSaberLeaderboardInfoProvider.CanRequest(leaderboardKey)) {
                return;
            }

            if (ScoreSaberLeaderboardInfoProvider.TryGetCached(leaderboardKey, out var cachedInfo)) {
                _scoreSaberLeaderboardInfo = cachedInfo;
                return;
            }

            _scoreSaberInfoCoroutine = StartManagedCoroutine(
                LoadScoreSaberInfo(_scoreSaberInfoRequestId, leaderboardKey),
                ref _scoreSaberInfoCoroutineRunner
            );
        }

        private IEnumerator LoadScoreSaberInfo(int requestId, LeaderboardKey leaderboardKey) {
            var url = ScoreSaberLeaderboardInfoProvider.BuildInfoUrl(leaderboardKey);
            using var request = UnityWebRequest.Get(url);
            _activeScoreSaberInfoRequest = request;
            request.timeout = ScoreSaberLeaderboardInfoProvider.TimeoutSeconds;
            request.SetRequestHeader("User-Agent", Plugin.UserAgent);
            yield return request.SendWebRequest();
            ClearActiveScoreSaberInfoRequest(request);

            if (requestId != _scoreSaberInfoRequestId) {
                yield break;
            }

            _scoreSaberInfoCoroutine = null;
            _scoreSaberInfoCoroutineRunner = null;

            if (request.isNetworkError || request.isHttpError) {
                Plugin.Log.Debug($"[ScoreSaberHeader] Failed to load leaderboard info for {leaderboardKey.Hash}/{leaderboardKey.Mode}/{leaderboardKey.Diff}: {request.responseCode} {request.error}");
                _scoreSaberLeaderboardInfo = ScoreSaberLeaderboardInfo.Unknown;
                UpdateVisuals();
                yield break;
            }

            if (!ScoreSaberLeaderboardInfoProvider.TryRead(request.downloadHandler.text, out var info)) {
                Plugin.Log.Info($"[ScoreSaberHeader] Could not parse leaderboard info for {leaderboardKey.Hash}/{leaderboardKey.Mode}/{leaderboardKey.Diff}");
                _scoreSaberLeaderboardInfo = ScoreSaberLeaderboardInfo.Unknown;
                UpdateVisuals();
                yield break;
            }

            _scoreSaberLeaderboardInfo = info;
            ScoreSaberLeaderboardInfoProvider.SaveToCache(leaderboardKey, info);
            UpdateVisuals();
        }

        private void CancelScoreSaberInfoRequest() {
            _scoreSaberInfoRequestId++;
            StopManagedCoroutine(ref _scoreSaberInfoCoroutine, ref _scoreSaberInfoCoroutineRunner);

            if (_activeScoreSaberInfoRequest != null) {
                _activeScoreSaberInfoRequest.Abort();
                _activeScoreSaberInfoRequest.Dispose();
                _activeScoreSaberInfoRequest = null;
            }
        }

        private void ClearActiveScoreSaberInfoRequest(UnityWebRequest request) {
            if (ReferenceEquals(_activeScoreSaberInfoRequest, request)) {
                _activeScoreSaberInfoRequest = null;
            }
        }

        #endregion

        #region AccSaberInfo

        private void RequestAccSaberInfo(LeaderboardKey leaderboardKey) {
            if (!AccSaberApiProvider.CanRequestScore(leaderboardKey)) {
                return;
            }

            if (AccSaberApiProvider.TryGetCachedDifficulty(leaderboardKey, out var cachedInfo)) {
                _accSaberDifficultyInfo = cachedInfo;
                return;
            }

            _accSaberInfoCoroutine = StartManagedCoroutine(
                LoadAccSaberInfo(_accSaberInfoRequestId, leaderboardKey),
                ref _accSaberInfoCoroutineRunner
            );
        }

        private IEnumerator LoadAccSaberInfo(int requestId, LeaderboardKey leaderboardKey) {
            var url = AccSaberApiProvider.BuildDifficultyInfoUrl(leaderboardKey);
            using var request = UnityWebRequest.Get(url);
            _activeAccSaberInfoRequest = request;
            request.timeout = AccSaberApiProvider.TimeoutSeconds;
            request.SetRequestHeader("User-Agent", Plugin.UserAgent);
            yield return request.SendWebRequest();
            ClearActiveAccSaberInfoRequest(request);

            if (requestId != _accSaberInfoRequestId) {
                yield break;
            }

            _accSaberInfoCoroutine = null;
            _accSaberInfoCoroutineRunner = null;

            if (request.isNetworkError || request.isHttpError) {
                Plugin.Log.Debug($"[AccSaberHeader] Failed to load difficulty info for {leaderboardKey.Hash}/{leaderboardKey.Diff}: {request.responseCode} {request.error}");
                _accSaberDifficultyInfo = AccSaberDifficultyInfo.Unknown;
                UpdateVisuals();
                yield break;
            }

            if (!AccSaberApiProvider.TryReadDifficulty(request.downloadHandler.text, leaderboardKey, out var info)) {
                Plugin.Log.Debug($"[AccSaberHeader] Could not parse difficulty info for {leaderboardKey.Hash}/{leaderboardKey.Diff}");
                _accSaberDifficultyInfo = AccSaberDifficultyInfo.Unknown;
                UpdateVisuals();
                yield break;
            }

            _accSaberDifficultyInfo = info;
            AccSaberApiProvider.SaveDifficultyToCache(leaderboardKey, info);
            UpdateVisuals();
        }

        private void CancelAccSaberInfoRequest() {
            _accSaberInfoRequestId++;
            StopManagedCoroutine(ref _accSaberInfoCoroutine, ref _accSaberInfoCoroutineRunner);

            if (_activeAccSaberInfoRequest != null) {
                _activeAccSaberInfoRequest.Abort();
                _activeAccSaberInfoRequest.Dispose();
                _activeAccSaberInfoRequest = null;
            }
        }

        private void ClearActiveAccSaberInfoRequest(UnityWebRequest request) {
            if (ReferenceEquals(_activeAccSaberInfoRequest, request)) {
                _activeAccSaberInfoRequest = null;
            }
        }

        #endregion

        #region UpdateCheckboxes

        private void UpdateCheckboxes(QualificationInfo qualificationInfo) {
            string criteriaPostfix;

            if (qualificationInfo.criteriaCommentary == null || qualificationInfo.criteriaCommentary.IsEmpty()) {
                criteriaPostfix = "";
            } else {
                criteriaPostfix = $"<size=80%>\n\n{qualificationInfo.criteriaCommentary}";
            }

            var criteriaCheckbox = _criteriaCheckbox.ReeComponent;
            var approvalCheckbox = _approvalCheckbox.ReeComponent;

            switch (qualificationInfo.criteriaMet) {
                case 1:
                    criteriaCheckbox.SetState(QualificationCheckbox.State.Checked);
                    criteriaCheckbox.HoverHint = $"Criteria passed{criteriaPostfix}";
                    break;
                case 2:
                    criteriaCheckbox.SetState(QualificationCheckbox.State.Failed);
                    criteriaCheckbox.HoverHint = $"Criteria failed{criteriaPostfix}";
                    break;
                case 3:
                    criteriaCheckbox.SetState(QualificationCheckbox.State.OnHold);
                    criteriaCheckbox.HoverHint = $"Criteria on hold{criteriaPostfix}";
                    break;
                default:
                    criteriaCheckbox.SetState(QualificationCheckbox.State.Neutral);
                    criteriaCheckbox.HoverHint = $"Awaiting criteria check{criteriaPostfix}";
                    break;
            }

            if (qualificationInfo.approved) {
                approvalCheckbox.SetState(QualificationCheckbox.State.Checked);
                approvalCheckbox.HoverHint = "Qualified!";
            } else {
                approvalCheckbox.SetState(QualificationCheckbox.State.Neutral);
                approvalCheckbox.HoverHint = "Awaiting RT approval";
            }
        }

        #endregion

        #region UpdateVisuals

        private void UpdateVisuals() {
            RefreshRankedStarsModeButton();
            UpdateRankedStarsPanelClearance();
            _mapStatus.SetActive(_rankedStatus is not RankedStatus.Unknown || _scoreSaberLeaderboardInfo.IsKnown || _accSaberDifficultyInfo.IsKnown);
            _mapStatus.SetValues(
                _rankedStatus,
                _difficultyInfo,
                _scoreSaberLeaderboardInfo,
                _accSaberDifficultyInfo,
                PluginConfig.RankedStarsDisplayMode
            );
            
            UpdateMapTypes();
            _captorClan.SetActive(_displayCaptorClan && _rankedStatus is RankedStatus.Ranked);

            var qualificationActive = _rankedStatus is RankedStatus.Nominated or RankedStatus.Qualified or RankedStatus.Unrankable;
            _criteriaCheckbox.Enabled = qualificationActive;
            _approvalCheckbox.Enabled = qualificationActive;

            _menuButton.Enabled = EnvironmentManagerPatch.EnvironmentType is not MenuEnvironmentManager.MenuEnvironmentType.Lobby;
        }

        private void UpdateRankedStarsPanelClearance() {
            if (_rankedStarsPanelModifier == null) {
                return;
            }

            if (PluginConfig.RankedStarsDisplayMode is RankedStarsDisplayMode.All) {
                var rankedSourcesCount = 0;
                if (_scoreSaberLeaderboardInfo.IsRanked) rankedSourcesCount++;
                if (_rankedStatus is RankedStatus.Ranked) rankedSourcesCount++;
                if (_accSaberDifficultyInfo.IsKnown) rankedSourcesCount++;

                _rankedStarsPanelModifier.Margin = rankedSourcesCount switch {
                    3 => new() { right = RankedStarsPanelTripleAllClearance },
                    2 => new() { left = RankedStarsPanelBothOffset, right = 0.0f },
                    1 => new() { left = RankedStarsPanelSingleSourceOffset, right = 0.0f },
                    _ => new() { left = RankedStarsPanelSingleSourceOffset, right = 0.0f }
                };
                return;
            }

            _rankedStarsPanelModifier.Margin =
                PluginConfig.RankedStarsDisplayMode is RankedStarsDisplayMode.Both
                    ? new() { left = RankedStarsPanelBothOffset, right = 0.0f }
                    : new() { left = RankedStarsPanelSingleSourceOffset, right = 0.0f };
        }

        private void UpdateMapTypes() {
            if (_mapTypePanels == null) {
                _mapTypePanels = new List<MapTypePanel> { _mapTypePanel1, _mapTypePanel2, _mapTypePanel3 };
            }
            
            int typeIndex = 0;
            int knownTypes = MapTypesManager.MapsTypes?.Count ?? 0;
            foreach (var mapTypePanel in _mapTypePanels) {
                for (; typeIndex < knownTypes; typeIndex++) {
                    var typeDescriptiopn = MapTypesManager.MapsTypes![typeIndex];
                    if ((_mapType & typeDescriptiopn.Id) == typeDescriptiopn.Id) {
                        mapTypePanel.SetActive(true);
                        mapTypePanel.SetValues(typeDescriptiopn);
                        typeIndex++;
                        break;
                    }
                }

                if (typeIndex == knownTypes) {
                    mapTypePanel.SetActive(false);
                }
            }
        }

        #endregion

        #region Coroutine Runner

        private static Coroutine StartManagedCoroutine(IEnumerator coroutine, ref MonoBehaviour? runner) {
            runner = GetCoroutineRunner();
            return runner.StartCoroutine(coroutine);
        }

        private static void StopManagedCoroutine(ref Coroutine? coroutine, ref MonoBehaviour? runner) {
            if (coroutine != null && runner != null) {
                runner.StopCoroutine(coroutine);
            }

            coroutine = null;
            runner = null;
        }

        private static MonoBehaviour GetCoroutineRunner() {
            if (_coroutineRunner != null) {
                return _coroutineRunner;
            }

            var gameObject = new GameObject("BeatLeaderLeaderboardInfoRunner");
            UnityEngine.Object.DontDestroyOnLoad(gameObject);
            _coroutineRunner = gameObject.AddComponent<CoroutineRunner>();
            return _coroutineRunner;
        }

        private sealed class CoroutineRunner : MonoBehaviour {
        }

        #endregion

        #region Utils

        private void CycleRankedStarsDisplayMode() {
            PluginConfig.RankedStarsDisplayMode = PluginConfig.RankedStarsDisplayMode switch {
                RankedStarsDisplayMode.ScoreSaberOnly => RankedStarsDisplayMode.BeatLeaderOnly,
                RankedStarsDisplayMode.BeatLeaderOnly => RankedStarsDisplayMode.AccSaberOnly,
                RankedStarsDisplayMode.AccSaberOnly => RankedStarsDisplayMode.Both,
                RankedStarsDisplayMode.Both => RankedStarsDisplayMode.All,
                _ => RankedStarsDisplayMode.ScoreSaberOnly
            };
        }

        private void RefreshRankedStarsModeButton() {
            if (_rankedStarsModeButton == null) {
                return;
            }

            _rankedStarsModeButton.Text = RankedStarsModeButtonText(PluginConfig.RankedStarsDisplayMode);
        }

        private static string RankedStarsModeButtonText(RankedStarsDisplayMode mode) {
            return mode switch {
                RankedStarsDisplayMode.ScoreSaberOnly => $"<color={BeatLeaderDarkGoldTheme.ScoreSaberPpHtml}>SS</color>",
                RankedStarsDisplayMode.BeatLeaderOnly => $"<color={BeatLeaderDarkGoldTheme.BeatLeaderPpHtml}>BL</color>",
                RankedStarsDisplayMode.AccSaberOnly => $"<color={BeatLeaderDarkGoldTheme.AccSaberApHtml}>Acc</color>",
                RankedStarsDisplayMode.All => $"<color={BeatLeaderDarkGoldTheme.ScoreSaberPpHtml}>A</color><color={BeatLeaderDarkGoldTheme.BeatLeaderPpHtml}>l</color><color={BeatLeaderDarkGoldTheme.AccSaberApHtml}>l</color>",
                _ => $"<color={BeatLeaderDarkGoldTheme.ScoreSaberPpHtml}>SS</color><color={BeatLeaderDarkGoldTheme.TextMutedHtml}>+</color><color={BeatLeaderDarkGoldTheme.BeatLeaderPpHtml}>BL</color>"
            };
        }

        private static bool ExMachinaVisibleToRole(PlayerRole playerRole) {
            return playerRole.IsAnyAdmin() || playerRole.IsAnyRT() || playerRole.IsAnySupporter();
        }

        private static bool RtToolsVisibleToRole(PlayerRole playerRole) {
            return playerRole.IsAnyAdmin() || playerRole.IsAnyRT();
        }

        #endregion
    }
}
