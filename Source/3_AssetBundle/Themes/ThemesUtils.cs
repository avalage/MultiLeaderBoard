using BeatLeader.Models;
using System.Collections.Generic;
using UnityEngine;

namespace BeatLeader.Themes {
    internal static class ThemesUtils {
        private static bool _loggedMissingThemesCollection;
        private static readonly HashSet<string> LoggedMissingThemeMaterials = new();
        private static readonly HashSet<string> LoggedUnknownThemes = new();
        private static readonly HashSet<string> LoggedParsedEffectNames = new();

        #region GetAvatarParams

        public static void GetAvatarParams(
            IPlayerProfileSettings? profileSettings, bool useSmallMaterialVersion,
            out Material baseMaterial, out float hueShift, out float saturation
        ) {
            if (profileSettings == null) {
                hueShift = 0.0f;
                saturation = 1.0f;
                baseMaterial = BundleLoader.DefaultAvatarMaterial;
                return;
            }

            hueShift = (profileSettings.EffectHue / 360.0f) * (Mathf.PI * 2);
            saturation = profileSettings.EffectSaturation;
            baseMaterial = GetAvatarMaterial(profileSettings.ThemeType, profileSettings.ThemeTier, useSmallMaterialVersion);
        }

        #endregion

        #region GetAvatarMaterial

        public static Material GetAvatarMaterial(ThemeType themeType, ThemeTier themeTier, bool smallVersion) {
            if (themeType is ThemeType.Unknown || themeTier is ThemeTier.Unknown) {
                LogUnknownTheme(themeType, themeTier);
                return BundleLoader.DefaultAvatarMaterial;
            }

            if (BundleLoader.ThemesCollection == null) {
                if (BundleLoader.TryGetAvatarThemeMaterial(themeType, themeTier, smallVersion, out var fallbackMaterial)) {
                    LogMaterialFallback(themeType, themeTier, smallVersion, fallbackMaterial.name);
                    return fallbackMaterial;
                }

                if (!_loggedMissingThemesCollection) {
                    _loggedMissingThemesCollection = true;
                    Plugin.Log.Info("[AvatarTheme] ThemesCollection is missing; avatar frame materials cannot be applied.");
                }

                return BundleLoader.DefaultAvatarMaterial;
            }

            if (!BundleLoader.ThemesCollection.TryGetThemeMaterials(themeType, out var themeMaterials) || themeMaterials == null) {
                if (BundleLoader.TryGetAvatarThemeMaterial(themeType, themeTier, smallVersion, out var fallbackMaterial)) {
                    LogMaterialFallback(themeType, themeTier, smallVersion, fallbackMaterial.name);
                    return fallbackMaterial;
                }

                LogMissingThemeMaterial(themeType, themeTier, smallVersion, "theme materials collection");
                return BundleLoader.DefaultAvatarMaterial;
            }

            if (!themeMaterials.TryGetAvatarMaterial(themeTier, smallVersion, out var material) || material == null) {
                if (BundleLoader.TryGetAvatarThemeMaterial(themeType, themeTier, smallVersion, out var fallbackMaterial)) {
                    LogMaterialFallback(themeType, themeTier, smallVersion, fallbackMaterial.name);
                    return fallbackMaterial;
                }

                LogMissingThemeMaterial(themeType, themeTier, smallVersion, "avatar material");
                return BundleLoader.DefaultAvatarMaterial;
            }

            return material;
        }

        #endregion

        #region ParseEffectName

        public static void ParseEffectName(string effectName, out ThemeType themeType, out ThemeTier themeTier) {
            if (string.IsNullOrWhiteSpace(effectName)) {
                themeType = ThemeType.Unknown;
                themeTier = ThemeTier.Unknown;
                return;
            }

            var normalizedName = NormalizeToken(effectName);

            themeType = normalizedName switch {
                var value when value.Contains("booster") => ThemeType.Booster,
                var value when value.Contains("thesun") || value.Contains("sun") => ThemeType.TheSun,
                var value when value.Contains("themoon") || value.Contains("moon") => ThemeType.TheMoon,
                var value when value.Contains("thestar") || value.Contains("star") => ThemeType.TheStar,
                var value when value.Contains("sparks") => ThemeType.Sparks,
                var value when value.Contains("special") => ThemeType.Special,
                _ => ThemeType.Unknown
            };

            themeTier = normalizedName switch {
                var value when value.Contains("tier1") || value.EndsWith("1") => ThemeTier.Tier1,
                var value when value.Contains("tier2") || value.EndsWith("2") => ThemeTier.Tier2,
                var value when value.Contains("tier3") || value.EndsWith("3") => ThemeTier.Tier3,
                _ => ThemeTier.Unknown
            };

            LogParsedEffectName(effectName, themeType, themeTier);
        }

        private static string NormalizeToken(string value) {
            return value
                .Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .Replace(" ", string.Empty)
                .ToLowerInvariant();
        }

        private static void LogUnknownTheme(ThemeType themeType, ThemeTier themeTier) {
            var key = $"{themeType}|{themeTier}";
            if (!LoggedUnknownThemes.Add(key)) {
                return;
            }

            Plugin.Log.Info($"[AvatarTheme] Unknown avatar theme values: type={themeType}, tier={themeTier}.");
        }

        private static void LogParsedEffectName(string effectName, ThemeType themeType, ThemeTier themeTier) {
            var key = $"{effectName}|{themeType}|{themeTier}";
            if (!LoggedParsedEffectNames.Add(key)) {
                return;
            }

            Plugin.Log.Info($"[AvatarTheme] Parsed effectName='{effectName}' -> type={themeType}, tier={themeTier}.");
        }

        private static void LogMissingThemeMaterial(ThemeType themeType, ThemeTier themeTier, bool smallVersion, string part) {
            var key = $"{themeType}|{themeTier}|{smallVersion}|{part}";
            if (!LoggedMissingThemeMaterials.Add(key)) {
                return;
            }

            Plugin.Log.Info($"[AvatarTheme] Missing {part} for type={themeType}, tier={themeTier}, small={smallVersion}.");
        }

        private static void LogMaterialFallback(ThemeType themeType, ThemeTier themeTier, bool smallVersion, string materialName) {
            var key = $"{themeType}|{themeTier}|{smallVersion}|fallback|{materialName}";
            if (!LoggedMissingThemeMaterials.Add(key)) {
                return;
            }

            Plugin.Log.Info($"[AvatarTheme] Using fallback material '{materialName}' for type={themeType}, tier={themeTier}, small={smallVersion}.");
        }

        #endregion
    }
}
