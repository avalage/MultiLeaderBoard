using System;
using System.Net;
using BeatLeader.Models;
using BeatLeader.Models.Replay;
using BeatLeader.Utils;
using BeatLeader.WebRequests;

namespace BeatLeader.API {
    public class UploadReplayRequest : PersistentSingletonWebRequestBase<UploadReplayRequest, ScoreUploadResponse, JsonResponseParser<ScoreUploadResponse>> {
        private static bool _subscribedToApiV2;

        [Obsolete("Compatibility shim for external mods. Use BeatLeader.APIV2.UploadReplayRequest instead.")]
        public static new ScoreUploadResponse? Result => APIV2.UploadReplayRequest.Result;

        [Obsolete("Compatibility shim for external mods. Use BeatLeader.APIV2.UploadReplayRequest instead.")]
        public static new WebRequests.RequestState RequestState => APIV2.UploadReplayRequest.RequestState;

        [Obsolete("Compatibility shim for external mods. Use BeatLeader.APIV2.UploadReplayRequest instead.")]
        public static new HttpStatusCode RequestStatusCode => APIV2.UploadReplayRequest.RequestStatusCode;

        [Obsolete("Compatibility shim for external mods. Use BeatLeader.APIV2.UploadReplayRequest instead.")]
        public static new string? FailReason => WebRequestFailReasonFormatter.FormatOrNull(APIV2.UploadReplayRequest.FailReason);

        [Obsolete("Compatibility shim for external mods. Use BeatLeader.APIV2.UploadReplayRequest instead.")]
        public static new float DownloadProgress => APIV2.UploadReplayRequest.DownloadProgress;

        [Obsolete("Compatibility shim for external mods. Use BeatLeader.APIV2.UploadReplayRequest instead.")]
        public static new float UploadProgress => APIV2.UploadReplayRequest.UploadProgress;

        [Obsolete("Compatibility shim for external mods. Use BeatLeader.APIV2.UploadReplayRequest instead.")]
        public static new float OverallProgress => APIV2.UploadReplayRequest.OverallProgress;

        [Obsolete("Compatibility shim for external mods. Use BeatLeader.APIV2.UploadReplayRequest instead.")]
        public static new event WebRequestStateChangedDelegate<IWebRequest<ScoreUploadResponse>>? StateChangedEvent {
            add {
                EnsureSubscribedToApiV2();
                PersistentSingletonWebRequestBase<UploadReplayRequest, ScoreUploadResponse, JsonResponseParser<ScoreUploadResponse>>
                    .StateChangedEvent += value;
            }
            remove {
                PersistentSingletonWebRequestBase<UploadReplayRequest, ScoreUploadResponse, JsonResponseParser<ScoreUploadResponse>>
                    .StateChangedEvent -= value;
            }
        }

        [Obsolete("Compatibility shim for external mods. Use BeatLeader.APIV2.UploadReplayRequest instead.")]
        public static new event WebRequestProgressChangedDelegate<IWebRequest<ScoreUploadResponse>>? ProgressChangedEvent {
            add {
                EnsureSubscribedToApiV2();
                PersistentSingletonWebRequestBase<UploadReplayRequest, ScoreUploadResponse, JsonResponseParser<ScoreUploadResponse>>
                    .ProgressChangedEvent += value;
            }
            remove {
                PersistentSingletonWebRequestBase<UploadReplayRequest, ScoreUploadResponse, JsonResponseParser<ScoreUploadResponse>>
                    .ProgressChangedEvent -= value;
            }
        }

        [Obsolete("Compatibility shim for external mods. Use BeatLeader.APIV2.UploadReplayRequest instead.")]
        public static void InitializeCompatibility() {
            EnsureSubscribedToApiV2();
        }

        [Obsolete("Compatibility shim for external mods. Use BeatLeader.APIV2.UploadReplayRequest instead.")]
        public static new void Cancel() {
            APIV2.UploadReplayRequest.Cancel();
        }

        [Obsolete("Compatibility shim for external mods. Use BeatLeader.APIV2.UploadReplayRequest instead.")]
        public static void Send(Replay replay, PlayEndData data) {
            APIV2.UploadReplayRequest.Send(replay, data);
        }

        private static void EnsureSubscribedToApiV2() {
            if (_subscribedToApiV2) return;

            APIV2.UploadReplayRequest.StateChangedEvent += OnApiV2UploadStateChanged;
            APIV2.UploadReplayRequest.ProgressChangedEvent += OnApiV2UploadProgressChanged;
            _subscribedToApiV2 = true;
        }

        private static void OnApiV2UploadStateChanged(
            IWebRequest<ScoreUploadResponse> instance,
            WebRequests.RequestState state,
            string? failReason) {
            PersistentSingletonWebRequestBase<UploadReplayRequest, ScoreUploadResponse, JsonResponseParser<ScoreUploadResponse>>
                .Instance_StateChangedEvent(instance, state, WebRequestFailReasonFormatter.FormatOrNull(failReason));
        }

        private static void OnApiV2UploadProgressChanged(
            IWebRequest<ScoreUploadResponse> instance,
            float downloadProgress,
            float uploadProgress,
            float overallProgress) {
            PersistentSingletonWebRequestBase<UploadReplayRequest, ScoreUploadResponse, JsonResponseParser<ScoreUploadResponse>>
                .Instance_ProgressChangedEvent(instance, downloadProgress, uploadProgress, overallProgress);
        }
    }
}
