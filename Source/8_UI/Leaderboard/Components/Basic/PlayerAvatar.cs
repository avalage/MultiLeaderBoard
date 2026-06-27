using BeatLeader.Models;
using BeatLeader.Themes;
using BeatSaberMarkupLanguage.Attributes;
using HMUI;
using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

namespace BeatLeader.Components {
    internal class PlayerAvatar : ReeUIComponentV2 {
        #region Material

        private static readonly int AvatarTexturePropertyId = Shader.PropertyToID("_AvatarTexture");
        private static readonly int FadeValuePropertyId = Shader.PropertyToID("_FadeValue");
        private static readonly int HueShiftPropertyId = Shader.PropertyToID("_HueShift");
        private static readonly int SaturationPropertyId = Shader.PropertyToID("_Saturation");
        private static readonly int ScalePropertyId = Shader.PropertyToID("_Scale");

        private Texture? _texture;
        private float _fadeValue;
        private float _hueShift;
        private float _saturation;

        private Material? _baseMaterial;
        private Material? _materialInstance;
        private bool _materialSet;
        private IPlayerProfileSettings? _profileSettings;
        private static readonly HashSet<string> LoggedMaterialSelections = new();

        private void SelectMaterial(IPlayerProfileSettings? profileSettings) {
            ThemesUtils.GetAvatarParams(profileSettings, _useSmallMaterialVersion, out var baseMaterial, out _hueShift, out _saturation);
            LogMaterialSelection(profileSettings, baseMaterial);

            if (!_materialSet || baseMaterial != _baseMaterial) {
                _baseMaterial = baseMaterial;

                if (_materialSet) Destroy(_materialInstance);
                _materialInstance = Instantiate(baseMaterial);
                _image.material = _materialInstance;
                _materialSet = true;
                var scale = _materialInstance.HasProperty(ScalePropertyId) ? _materialInstance.GetFloat(ScalePropertyId) : 1.0f;
                _image.transform.localScale = new Vector3(scale, scale, scale);
            }

            UpdateMaterialProperties();
        }

        private void UpdateMaterialProperties() {
            if (!_materialSet) return;
            if (_materialInstance!.HasProperty(AvatarTexturePropertyId)) {
                _materialInstance.SetTexture(AvatarTexturePropertyId, _texture);
            }

            if (_materialInstance.HasProperty(FadeValuePropertyId)) {
                _materialInstance.SetFloat(FadeValuePropertyId, _fadeValue);
            }

            if (_materialInstance.HasProperty(HueShiftPropertyId)) {
                _materialInstance.SetFloat(HueShiftPropertyId, _hueShift);
            }

            if (_materialInstance.HasProperty(SaturationPropertyId)) {
                _materialInstance.SetFloat(SaturationPropertyId, _saturation);
            }
        }

        private void LogMaterialSelection(IPlayerProfileSettings? profileSettings, Material baseMaterial) {
            var profileKey = profileSettings == null
                ? "profile=null"
                : $"type={profileSettings.ThemeType},tier={profileSettings.ThemeTier},hue={profileSettings.EffectHue},sat={profileSettings.EffectSaturation}";
            var rawEffectName = profileSettings is ProfileSettings profileSettingsModel
                ? profileSettingsModel.effectName ?? "<null>"
                : "<unknown>";
            var materialName = baseMaterial != null ? baseMaterial.name : "null";
            var shaderName = baseMaterial != null && baseMaterial.shader != null ? baseMaterial.shader.name : "null";
            var isDefaultMaterial = baseMaterial == BundleLoader.DefaultAvatarMaterial;
            var key = $"{profileKey}|effect={rawEffectName}|small={_useSmallMaterialVersion}|material={materialName}|shader={shaderName}|default={isDefaultMaterial}";
            if (!LoggedMaterialSelections.Add(key)) {
                return;
            }

            Plugin.Log.Info(
                $"[PlayerAvatar] Material selected; {profileKey}; small={_useSmallMaterialVersion}; " +
                $"effectName='{rawEffectName}'; material={materialName}; shader={shaderName}; isDefault={isDefaultMaterial}; " +
                $"hasAvatarTexture={baseMaterial != null && baseMaterial.HasProperty(AvatarTexturePropertyId)}; " +
                $"hasFade={baseMaterial != null && baseMaterial.HasProperty(FadeValuePropertyId)}; " +
                $"hasHue={baseMaterial != null && baseMaterial.HasProperty(HueShiftPropertyId)}; " +
                $"hasSaturation={baseMaterial != null && baseMaterial.HasProperty(SaturationPropertyId)}; " +
                $"hasScale={baseMaterial != null && baseMaterial.HasProperty(ScalePropertyId)}"
            );

            if (profileSettings != null &&
                profileSettings.ThemeType != ThemeType.Unknown &&
                profileSettings.ThemeTier != ThemeTier.Unknown &&
                isDefaultMaterial) {
                Plugin.Log.Info(
                    $"[PlayerAvatar] Original BeatLeader avatar frame material was not resolved; " +
                    $"runtime fallback frame is disabled. type={profileSettings.ThemeType}, " +
                    $"tier={profileSettings.ThemeTier}, small={_useSmallMaterialVersion}, effectName='{rawEffectName}'."
                );
            }
        }

        #endregion

        #region Initialize / Dispose / Setup

        private const int Width = 200;
        private const int Height = 200;

        private RenderTexture _bufferTexture = null!;
        private bool _useSmallMaterialVersion;

        protected override void OnInitialize() {
            _bufferTexture = new RenderTexture(Width, Height, 0, RenderTextureFormat.Default, 10);
            _bufferTexture.Create();
        }

        protected override void OnDispose() {
            _bufferTexture.Release();
        }

        public void Setup(bool useSmallMaterialVersion) {
            if (_useSmallMaterialVersion == useSmallMaterialVersion) return;
            _useSmallMaterialVersion = useSmallMaterialVersion;
            if (_materialSet) {
                SelectMaterial(_profileSettings);
            }
        }

        #endregion

        #region Events

        private void OnEnable() {
            UpdateAvatar();
        }

        private void OnDisable() {
            StopAllCoroutines();
        }

        #endregion

        #region SetAvatar

        private string? _url = "";
        private string? _profileSettingsKey = "";
        private CancellationTokenSource? tokenSource = null;

        public void SetLoading() {
            if (_url == null) return;
            _url = null;
            ShowSpinner();
        }
        
        public void SetAvatar(IPlayer? player) {
            SetAvatar(player?.AvatarUrl, player?.ProfileSettings);
        }

        public void SetAvatar(string? url, IPlayerProfileSettings? profileSettings) {
            _profileSettings = profileSettings;
            var profileSettingsKey = GetProfileSettingsKey(profileSettings);
            if (_url == url && _profileSettingsKey == profileSettingsKey) return;
            var urlChanged = _url != url;
            _url = url;
            _profileSettingsKey = profileSettingsKey;
            SelectMaterial(_profileSettings);
            if (urlChanged) {
                UpdateAvatar();
            } else {
                UpdateMaterialProperties();
            }
        }

        private static string? GetProfileSettingsKey(IPlayerProfileSettings? profileSettings) {
            return profileSettings == null
                ? null
                : $"{profileSettings.ThemeType}|{profileSettings.ThemeTier}|{profileSettings.EffectHue}|{profileSettings.EffectSaturation}";
        }

        private void UpdateAvatar() {
            if (!gameObject.activeInHierarchy) return;
            if (_url == null || _url.Length == 0) {
                ShowTexture(BundleLoader.DefaultAvatar.texture);
                return;
            }

            ShowSpinner();
            StopAllCoroutines();

            tokenSource?.Cancel();
            tokenSource = new CancellationTokenSource();
            StartCoroutine(LoadImage());
        }

        private IEnumerator LoadImage() {
            var loadTask = AvatarStorage.GetPlayerAvatarCoroutine(_url, false, OnAvatarLoadSuccess, OnAvatarLoadFailed, tokenSource.Token);
            yield return loadTask;
        }

        private void OnAvatarLoadSuccess(AvatarImage avatarImage) {
            ShowTexture(_bufferTexture);
            StartCoroutine(avatarImage.PlaybackCoroutine(_bufferTexture));
        }

        private void OnAvatarLoadFailed(string reason) {
            ShowTexture(BundleLoader.DefaultAvatar.texture);
        }

        #endregion

        #region Image

        [UIComponent("image-component"), UsedImplicitly]
        private ImageView _image = null!;

        public void SetAlpha(float value) {
            _image.color = new Color(1, 1, 1, value);
        }

        private void ShowSpinner() {
            _fadeValue = 0.0f;
            UpdateMaterialProperties();
        }

        private void ShowTexture(Texture texture) {
            _fadeValue = 1.0f;
            _texture = texture;
            UpdateMaterialProperties();
        }

        #endregion
    }
}
