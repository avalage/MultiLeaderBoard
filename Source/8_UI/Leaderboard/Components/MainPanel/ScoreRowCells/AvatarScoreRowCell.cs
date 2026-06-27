using BeatLeader.Models;
using BeatSaberMarkupLanguage.Attributes;
using JetBrains.Annotations;
using System.Collections.Generic;

namespace BeatLeader.Components {
    internal class AvatarScoreRowCell : AbstractScoreRowCell {
        #region Components

        private const float Size = 4.0f;
        private const int MaxLoggedAvatarRows = 250;
        private static readonly HashSet<string> LoggedAvatarRows = new();

        [UIValue("player-avatar"), UsedImplicitly]
        private PlayerAvatar _playerAvatar;

        private void Awake() {
            _playerAvatar = Instantiate<PlayerAvatar>(transform);
            _playerAvatar.Setup(true);
        }

        #endregion

        #region Implementation

        public struct Data {
            public readonly string? url;
            public readonly ProfileSettings? profileSettings;
            public readonly string? playerId;
            public readonly string? playerName;
            public readonly int rank;

            public Data(string? url, ProfileSettings? profileSettings, string? playerId = null, string? playerName = null, int rank = -1) {
                this.url = url;
                this.profileSettings = profileSettings;
                this.playerId = playerId;
                this.playerName = playerName;
                this.rank = rank;
            }
        }

        public override void SetValue(object? value) {
            if (value is Data data) {
                LogAvatarRowData(data);
                _playerAvatar.SetAvatar(data.url, data.profileSettings);
                isEmpty = false;
            } else {
                _playerAvatar.SetAvatar(null, null);
                isEmpty = true;
            }
        }

        private static void LogAvatarRowData(Data data) {
            if (LoggedAvatarRows.Count >= MaxLoggedAvatarRows) {
                return;
            }

            var frameDescription = data.profileSettings == null
                ? "profileSettings=<null>"
                : data.profileSettings.FrameDebugDescription;
            var key = $"{data.rank}|{data.playerId}|{data.playerName}|{data.url}|{frameDescription}";
            if (!LoggedAvatarRows.Add(key)) {
                return;
            }

            var avatarState = string.IsNullOrEmpty(data.url) ? "<empty>" : $"present,length={data.url!.Length}";
            Plugin.Log.Info(
                $"[AvatarFrame] Row avatar data; rank={data.rank}; player='{data.playerName ?? "<null>"}'; " +
                $"id={data.playerId ?? "<null>"}; avatar={avatarState}; {frameDescription}."
            );
        }

        public override void SetAlpha(float value) {
            _playerAvatar.SetAlpha(value);
        }

        protected override float CalculatePreferredWidth() {
            return Size;
        }

        #endregion
    }
}
