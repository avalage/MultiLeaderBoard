using System;
using System.Collections;
using BeatLeader.APIV2;
using BeatLeader.Manager;
using BeatLeader.Models;
using BeatLeader.UI;
using BeatLeader.Utils;
using BeatSaberMarkupLanguage.Attributes;
using JetBrains.Annotations;
using UnityEngine;

namespace BeatLeader.Components {
    internal class StatusBar : ReeUIComponentV2 {
        #region Initialize/Dispose

        protected override void OnInitialize() {
            LeaderboardEvents.StatusMessageEvent += OnStatusMessage;

            UserRequest.StateChangedEvent += OnProfileRequestStateChanged;
            VoteRequest.StateChangedEvent += OnVoteRequestStateChanged;
            UploadReplayRequest.StateChangedEvent += OnUploadRequestStateChanged;
        }

        protected override void OnDispose() {
            LeaderboardEvents.StatusMessageEvent -= OnStatusMessage;

            UserRequest.StateChangedEvent -= OnProfileRequestStateChanged;
            VoteRequest.StateChangedEvent -= OnVoteRequestStateChanged;
            UploadReplayRequest.StateChangedEvent -= OnUploadRequestStateChanged;
        }

        private void OnDisable() {
            StopAllCoroutines();
            MessageText = "";
        }

        #endregion

        #region Events

        private void OnVoteRequestStateChanged(WebRequests.IWebRequest<VoteStatus> instance, WebRequests.RequestState state, string? failReason) {
            switch (state) {
                case WebRequests.RequestState.Finished:
                    ShowGoodNews("Your vote has been accepted!");
                    break;
                case WebRequests.RequestState.Failed:
                    ShowBadNews($"Vote failed: {FormatFailReason(failReason)}");
                    break;
            }
        }

        private void OnProfileRequestStateChanged(WebRequests.IWebRequest<Player> instance, WebRequests.RequestState state, string? failReason) {
            switch (state) {
                case WebRequests.RequestState.Failed:
                    ShowBadNews($"Profile update failed: {FormatFailReason(failReason)}");
                    break;
            }
        }

        private void OnUploadRequestStateChanged(WebRequests.IWebRequest<ScoreUploadResponse> instance, WebRequests.RequestState state, string? failReason) {
            switch (state) {
                case WebRequests.RequestState.Finished:
                    if (instance.Result.Status == ScoreUploadStatus.Uploaded) {
                        ShowGoodNews("Score uploaded!");
                    }
                    break;
                case WebRequests.RequestState.Failed:
                    ShowBadNews($"Score upload failed: {FormatFailReason(failReason)}");
                    break;
            }
        }

        private void OnStatusMessage(string message, LeaderboardEvents.StatusMessageType type, float duration) {
            if (WebRequestFailReasonFormatter.IsTimeout(message)) {
                Plugin.Log.Debug($"Suppressed background status timeout message: {message}");
                return;
            }

            switch (type) {
                case LeaderboardEvents.StatusMessageType.Neutral:
                    ShowMessage(message, duration);
                    break;
                case LeaderboardEvents.StatusMessageType.Bad:
                    ShowBadNews(message, duration);
                    break;
                case LeaderboardEvents.StatusMessageType.Good:
                    ShowGoodNews(message, duration);
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        #endregion

        #region ShowMessage

        private const float DefaultDuration = 1.4f;
        private static string GoodNewsColor => BeatLeaderDarkGoldTheme.SuccessGreenHtml;
        private static string BadNewsColor => BeatLeaderDarkGoldTheme.WarningOrangeHtml;

        private void ShowGoodNews(string message, float duration = DefaultDuration) {
            ShowMessage($"<color={GoodNewsColor}>{message}", duration);
        }

        private void ShowBadNews(string message, float duration = DefaultDuration) {
            ShowMessage($"<color={BadNewsColor}>{message}", duration);
        }

        private static string FormatFailReason(string? failReason) {
            return WebRequestFailReasonFormatter.Format(failReason);
        }

        private void ShowMessage(string message, float duration = DefaultDuration) {
            if (!gameObject.activeInHierarchy) return;
            StopAllCoroutines();
            StartCoroutine(ShowMessageCoroutine(message, duration));
        }

        private IEnumerator ShowMessageCoroutine(string message, float duration) {
            MessageText = message;
            yield return new WaitForSeconds(duration);
            MessageText = "";
        }

        #endregion

        #region MessageText

        private string _messageText = "";

        [UIValue("message-text"), UsedImplicitly]
        public string MessageText {
            get => _messageText;
            set {
                if (_messageText.Equals(value)) return;
                _messageText = value;
                NotifyPropertyChanged();
            }
        }

        #endregion
    }
}
