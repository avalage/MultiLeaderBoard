using System;
using System.Net;
using BeatLeader.Models;
using BeatLeader.Models.Replay;
using BeatLeader.Utils;

namespace BeatLeader.WebRequests {
    public static class UploadReplayRequest {
        [Obsolete("Compatibility shim for external mods. Use BeatLeader.APIV2.UploadReplayRequest instead.")]
        public static ScoreUploadResponse? Result => APIV2.UploadReplayRequest.Result;

        [Obsolete("Compatibility shim for external mods. Use BeatLeader.APIV2.UploadReplayRequest instead.")]
        public static RequestState RequestState => APIV2.UploadReplayRequest.RequestState;

        [Obsolete("Compatibility shim for external mods. Use BeatLeader.APIV2.UploadReplayRequest instead.")]
        public static HttpStatusCode RequestStatusCode => APIV2.UploadReplayRequest.RequestStatusCode;

        [Obsolete("Compatibility shim for external mods. Use BeatLeader.APIV2.UploadReplayRequest instead.")]
        public static string? FailReason => WebRequestFailReasonFormatter.FormatOrNull(APIV2.UploadReplayRequest.FailReason);

        [Obsolete("Compatibility shim for external mods. Use BeatLeader.APIV2.UploadReplayRequest instead.")]
        public static float DownloadProgress => APIV2.UploadReplayRequest.DownloadProgress;

        [Obsolete("Compatibility shim for external mods. Use BeatLeader.APIV2.UploadReplayRequest instead.")]
        public static float UploadProgress => APIV2.UploadReplayRequest.UploadProgress;

        [Obsolete("Compatibility shim for external mods. Use BeatLeader.APIV2.UploadReplayRequest instead.")]
        public static float OverallProgress => APIV2.UploadReplayRequest.OverallProgress;

        [Obsolete("Compatibility shim for external mods. Use BeatLeader.APIV2.UploadReplayRequest instead.")]
        public static event WebRequestStateChangedDelegate<IWebRequest<ScoreUploadResponse>>? StateChangedEvent {
            add => APIV2.UploadReplayRequest.StateChangedEvent += value;
            remove => APIV2.UploadReplayRequest.StateChangedEvent -= value;
        }

        [Obsolete("Compatibility shim for external mods. Use BeatLeader.APIV2.UploadReplayRequest instead.")]
        public static event WebRequestProgressChangedDelegate<IWebRequest<ScoreUploadResponse>>? ProgressChangedEvent {
            add => APIV2.UploadReplayRequest.ProgressChangedEvent += value;
            remove => APIV2.UploadReplayRequest.ProgressChangedEvent -= value;
        }

        [Obsolete("Compatibility shim for external mods. Use BeatLeader.APIV2.UploadReplayRequest instead.")]
        public static void Cancel() {
            APIV2.UploadReplayRequest.Cancel();
        }

        [Obsolete("Compatibility shim for external mods. Use BeatLeader.APIV2.UploadReplayRequest instead.")]
        public static void Send(Replay replay, PlayEndData data) {
            APIV2.UploadReplayRequest.Send(replay, data);
        }
    }
}
