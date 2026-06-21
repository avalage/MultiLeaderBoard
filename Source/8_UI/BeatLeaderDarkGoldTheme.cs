using BeatLeader;
using HMUI;
using Reactive.Components;
using Reactive.BeatSaber;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BeatLeader.UI {
    internal static class BeatLeaderDarkGoldTheme {
        private const string LeaderboardHeaderResource = Plugin.ResourcesPath + ".Images.leaderboard_header_dark_gold.png";
        private const string SelectedScoreRowResource = Plugin.ResourcesPath + ".Images.selected_score_row_gold.png";
        private static readonly int FillAspectPropertyId = Shader.PropertyToID("_FillAspect");
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
        private static readonly int BackgroundColorPropertyId = Shader.PropertyToID("_BackgroundColor");
        private static Sprite? _leaderboardHeaderSprite;
        private static Sprite? _selectedScoreRowSprite;
        private static Sprite? _solidPanelSprite;

        private const string BackgroundDarkHex = "#050507";
        private const string PanelDarkHex = "#0B0C10";
        private const string PanelDark2Hex = "#12131A";
        private const string BorderGoldHex = "#FFD21A";
        private const string AccentYellowHex = "#FFE04D";
        private const string AccentGoldHex = "#FFB000";
        private const string AccentAmberHex = "#FF8C00";
        private const string TextPrimaryHex = "#F5F5F5";
        private const string TextSecondaryHex = "#A8A8A8";
        private const string TextMutedHex = "#6F6F6F";
        private const string SuccessGreenHex = "#28E875";
        private const string AccSaberGreenHex = "#159947";
        private const string WarningOrangeHex = "#FF7A1A";
        private const string ErrorRedHex = "#FF3B30";

        private const string LegacyWhiteHex = "#FFFFFF";
        private const string LegacyFadedHex = "#888888";
        private const string LegacyMutedHex = "#555555";
        private const string LegacyGoodHex = "#88FF88";
        private const string LegacyBadHex = "#FF8888";
        private const string LegacyPpHex = "#B856FF";

        public static bool Enabled => !ConfigFileData.IsInitialized || ConfigFileData.Instance.DarkGoldThemeEnabled;

        public static readonly Color BackgroundDark = Html(BackgroundDarkHex);
        public static readonly Color PanelDark = Html(PanelDarkHex);
        public static readonly Color PanelDark2 = Html(PanelDark2Hex);
        public static readonly Color BorderGold = Html(BorderGoldHex);
        public static readonly Color AccentYellow = Html(AccentYellowHex);
        public static readonly Color AccentGold = Html(AccentGoldHex);
        public static readonly Color AccentAmber = Html(AccentAmberHex);
        public static readonly Color TextPrimary = Html(TextPrimaryHex);
        public static readonly Color TextSecondary = Html(TextSecondaryHex);
        public static readonly Color TextMuted = Html(TextMutedHex);
        public static readonly Color SuccessGreen = Html(SuccessGreenHex);
        public static readonly Color AccSaberGreen = Html(AccSaberGreenHex);
        public static readonly Color WarningOrange = Html(WarningOrangeHex);
        public static readonly Color ErrorRed = Html(ErrorRedHex);

        public static readonly Color LegacySelectedBlue = new(0.0f, 0.4f, 1.0f, 1.0f);
        public static readonly Color LegacyFadedIcon = new(0.8f, 0.8f, 0.8f, 0.2f);
        public static readonly Color LegacyFadedIconHover = new(0.5f, 0.5f, 0.5f, 0.2f);
        public static readonly Color LegacyInactiveIcon = new(0.2f, 0.2f, 0.2f, 1.0f);

        public static Color PanelTransparent => WithAlpha(PanelDark, 0.96f);
        public static Color PanelTransparent2 => WithAlpha(PanelDark2, 0.96f);
        public static Color SelectedIconColor => Enabled ? AccentYellow : LegacySelectedBlue;
        public static Color FadedIconColor => Enabled ? WithAlpha(TextSecondary, 0.34f) : LegacyFadedIcon;
        public static Color FadedIconHoverColor => Enabled ? WithAlpha(AccentGold, 0.55f) : LegacyFadedIconHover;
        public static Color InactiveIconColor => Enabled ? WithAlpha(TextMuted, 0.35f) : LegacyInactiveIcon;
        public static Color ActivePaginationColor => Enabled ? AccentYellow : Color.white;
        public static Color HoveredPaginationColor => Enabled ? AccentGold : new Color(0.5f, 0.5f, 0.5f);
        public static Color PrimaryTextColor => Enabled ? TextPrimary : Color.white;
        public static Color HighlightTextColor => Enabled ? AccentYellow : Color.cyan;
        public static Color ScorePanelColor => Enabled ? PanelTransparent : Color.white;
        public static Color BottomPanelColor => Enabled ? WithAlpha(PanelDark2, 0.86f) : Color.white;
        public static Color DividerColor => Enabled ? WithAlpha(BorderGold, 0.28f) : Color.white;
        public static Color MiniProfileBackgroundColor => Enabled ? WithAlpha(PanelDark, 0.86f) : new Color(0.0f, 0.0f, 0.0f, 0.8f);
        public static Color QualificationNeutralColor => Enabled ? WithAlpha(TextMuted, 0.5f) : new Color(0.5f, 0.5f, 0.5f, 0.5f);
        public static Color QualificationFailedColor => Enabled ? WithAlpha(ErrorRed, 0.8f) : new Color(1.0f, 0.0f, 0.0f, 0.8f);
        public static Color QualificationOnHoldColor => Enabled ? WithAlpha(AccentAmber, 0.8f) : new Color(1.0f, 1.0f, 0.0f, 0.8f);
        public static Color QualificationCheckedColor => Enabled ? WithAlpha(SuccessGreen, 0.8f) : new Color(0.0f, 1.0f, 0.0f, 0.8f);
        public static Color AccuracyGridGoodColor => Enabled ? AccentYellow : new Color(0f, 0.2f, 1.0f, 1.0f);
        public static Color AccuracyGridBadColor => Enabled ? WithAlpha(PanelDark2, 0.1f) : new Color(0.0f, 0.1f, 0.3f, 0.1f);
        public static Color AccuracyGridHoverColor => Enabled ? WithAlpha(AccentAmber, 0.65f) : new Color(1.0f, 0.2f, 0.5f, 0.8f);
        public static Color AccuracyGridEmptyColor => Enabled ? WithAlpha(PanelDark, 0.0f) : new Color(0.1f, 0.1f, 0.1f, 0.0f);
        public static Color VotingLoadingTint => Enabled ? WithAlpha(AccentAmber, 0.45f) : new Color(0.2f, 0.2f, 0.2f, 0.0f);
        public static Color VotingLockedTint => Enabled ? WithAlpha(TextMuted, 0.28f) : new Color(0.2f, 0.2f, 0.2f, 0.0f);
        public static Color VotingUnlockedTint => Enabled ? WithAlpha(AccentYellow, 0.95f) : new Color(1f, 1f, 1f, 0.7f);
        public static Color VotingDoneTint => Enabled ? WithAlpha(SuccessGreen, 0.55f) : new Color(0.2f, 0.8f, 0.2f, 0.4f);
        public static Color VotingIdleColor => Enabled ? TextMuted : Color.black;
        public static Color VotingHoverColor => Enabled ? AccentAmber : Color.red;
        public static Color DifficultyPanelColor => Enabled ? WithAlpha(PanelDark, 0.96f) : Color.white;
        public static Color ScoreTableBackgroundColor => Enabled ? WithAlpha(BackgroundDark, 0.92f) : Color.clear;
        public static Color CurrentPlayerScoreBackgroundColor => Enabled ? WithAlpha(Color.Lerp(PanelDark2, AccentAmber, 0.38f), 0.88f) : new Color(0.7f, 0f, 0.7f, 0.3f);
        public static Color LogoTint => Enabled ? Color.white : Color.white;
        public static Color LogoGlowTint => Enabled ? new Color(1.0f, 0.95f, 0.58f, 0.34f) : Color.clear;
        public static Color HeaderPanelColor0 => Enabled ? WithAlpha(PanelDark2, 2.35f) : LegacySelectedBlue;
        public static Color HeaderPanelColor1 => Enabled ? WithAlpha(PanelDark, 2.35f) : LegacySelectedBlue;
        public static Color ExperienceBarColor => Enabled ? WithAlpha(AccentYellow, 0.95f) : Color.white;
        public static Color ExperienceBarBackgroundColor => Enabled ? WithAlpha(PanelDark, 0.96f) : Color.white;
        public static Color ExperienceBarProgressColor => Enabled ? WithAlpha(AccentYellow, 0.98f) : Color.white;
        public static Color ExperienceBarSessionColor => Enabled ? WithAlpha(BorderGold, 1.0f) : Color.white;
        public static Color ExperienceBarGlowColor => Enabled ? WithAlpha(AccentYellow, 0.18f) : Color.clear;
        public static Color AccuracyNeutralColor => Enabled ? TextSecondary : new Color(0.93f, 1f, 0.62f);

        public static string TextPrimaryHtml => Enabled ? TextPrimaryHex : LegacyWhiteHex;
        public static string TextSecondaryHtml => Enabled ? TextSecondaryHex : LegacyFadedHex;
        public static string TextMutedHtml => Enabled ? TextMutedHex : LegacyMutedHex;
        public static string AccentYellowHtml => Enabled ? AccentYellowHex : LegacyPpHex;
        public static string AccentGoldHtml => Enabled ? AccentGoldHex : LegacyPpHex;
        public static string BeatLeaderPpHtml => LegacyPpHex;
        public static string ScoreSaberPpHtml => Enabled ? AccentYellowHex : "#6772E5";
        public static string AccSaberApHtml => Enabled ? AccSaberGreenHex : LegacyGoodHex;
        public static string SuccessGreenHtml => Enabled ? SuccessGreenHex : LegacyGoodHex;
        public static string WarningOrangeHtml => Enabled ? WarningOrangeHex : LegacyBadHex;
        public static string ErrorRedHtml => Enabled ? ErrorRedHex : LegacyBadHex;

        public static Sprite LeaderboardHeaderSprite => _leaderboardHeaderSprite ??= LoadEmbeddedSprite(LeaderboardHeaderResource, "BeatLeaderDarkGoldHeaderSprite");
        public static Sprite SelectedScoreRowSprite => _selectedScoreRowSprite ??= LoadEmbeddedSprite(SelectedScoreRowResource, "BeatLeaderDarkGoldSelectedScoreRowSprite");
        public static Sprite SolidPanelSprite => _solidPanelSprite ??= CreateSolidPanelSprite();

        public static ReadOnlyColorSet GlowingButtonColorSet => new() {
            ActiveColor = Enabled ? AccentYellow : BeatSaberStyle.PrimaryButtonColor,
            HoveredColor = Enabled ? AccentGold : BeatSaberStyle.PrimaryButtonColor,
            Color = Enabled ? WithAlpha(TextSecondary, 0.22f) : WithAlpha(Color.white * 0.8f, 0.2f)
        };

        public static Color RowBackground(bool selected, float alpha, float opacity = 1.0f) {
            var color = selected
                ? (Enabled ? WithAlpha(AccentGold, 0.50f) : new Color(0.7f, 0f, 0.7f, 0.3f))
                : (Enabled ? WithAlpha(PanelDark2, 0.18f) : new Color(0.07f, 0f, 0.14f, 0.05f));
            if (!selected) color.a *= Mathf.Clamp01(opacity);
            color.a *= alpha;
            return color;
        }

        public static Color WithAlpha(Color color, float alpha) {
            color.a = alpha;
            return color;
        }

        public static void ApplyPanel(ImageView? image, Color color) {
            if (!Enabled || image == null) return;
            image.color = color;
        }

        public static Material? ApplyReplayStylePanel(ImageView? image, Color accentColor, Color backgroundColor, float fillAspect = 1.0f) {
            if (!Enabled || image == null || BundleLoader.OpponentBackgroundMaterial == null) return null;

            var material = Object.Instantiate(BundleLoader.OpponentBackgroundMaterial);
            material.SetFloat(FillAspectPropertyId, fillAspect);
            material.SetColor(ColorPropertyId, accentColor);
            material.SetColor(BackgroundColorPropertyId, backgroundColor);

            image.material = material;
            image.color = Color.white;
            return material;
        }

        public static void ApplyGraphicColor(Graphic? graphic, Color color) {
            if (!Enabled || graphic == null) return;
            graphic.color = color;
        }

        public static void ApplyTextColor(TextMeshProUGUI? text, Color color) {
            if (!Enabled || text == null) return;
            text.color = color;
        }

        public static void ApplyGoldMaterial(Material? material, Color color) {
            if (!Enabled || material == null) return;

            SetColorIfPresent(material, "_Color", color);
            SetColorIfPresent(material, "_Tint", color);
            SetColorIfPresent(material, "_TintColor", color);
            SetColorIfPresent(material, "_BaseColor", color);
            SetColorIfPresent(material, "_MainColor", color);
            SetColorIfPresent(material, "_BackgroundColor", PanelDark);
            SetColorIfPresent(material, "_ProgressColor", AccentGold);
            SetColorIfPresent(material, "_SessionColor", AccentYellow);
            SetColorIfPresent(material, "_GradientColor", color);
            SetColorIfPresent(material, "_GradientColor1", AccentYellow);
            SetColorIfPresent(material, "_GradientColor2", AccentGold);
            SetColorIfPresent(material, "_Color0", PanelDark2);
            SetColorIfPresent(material, "_Color1", AccentYellow);
            SetColorIfPresent(material, "_Color2", AccentGold);
            SetColorIfPresent(material, "_Color3", AccentAmber);
            SetColorIfPresent(material, "_GlowColor", WithAlpha(AccentYellow, Mathf.Max(color.a, 0.55f)));
            SetColorIfPresent(material, "_OutlineColor", BorderGold);
            SetColorIfPresent(material, "_EmissionColor", WithAlpha(AccentGold, Mathf.Max(color.a, 0.45f)));
        }

        private static void SetColorIfPresent(Material material, string propertyName, Color color) {
            var propertyId = Shader.PropertyToID(propertyName);
            if (material.HasProperty(propertyId)) material.SetColor(propertyId, color);
        }

        private static Color Html(string html) {
            return ColorUtility.TryParseHtmlString(html, out var color) ? color : Color.white;
        }

        private static Sprite LoadEmbeddedSprite(string resourcePath, string spriteName) {
            using var stream = ResourcesUtils.GetEmbeddedResourceStream(resourcePath);
            if (stream == null) {
                throw new System.InvalidOperationException($"Embedded resource not found: {resourcePath}");
            }

            using var memoryStream = new System.IO.MemoryStream();
            stream.CopyTo(memoryStream);

            var texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
            if (!texture.LoadImage(memoryStream.ToArray())) {
                throw new System.InvalidOperationException($"Unity failed to decode PNG resource: {resourcePath}");
            }

            texture.name = spriteName + "Texture";
            texture.wrapMode = TextureWrapMode.Clamp;

            var sprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100.0f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(12.0f, 12.0f, 12.0f, 12.0f)
            );
            sprite.name = spriteName;
            return sprite;
        }

        private static Sprite CreateSolidPanelSprite() {
            var texture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            texture.name = "BeatLeaderDarkGoldSolidPanelTexture";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);

            var sprite = Sprite.Create(
                texture,
                new Rect(0, 0, 1, 1),
                new Vector2(0.5f, 0.5f),
                100.0f,
                0,
                SpriteMeshType.FullRect
            );
            sprite.name = "BeatLeaderDarkGoldSolidPanelSprite";
            return sprite;
        }
    }
}
