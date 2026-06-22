using System;
using System.Linq;
using System.Reflection;
using BeatLeader.Utils;
using HarmonyLib;
using IPA.Loader;

namespace BeatLeader {
    internal static class GuildSaberCompatibilityPatches {
        private const string GuildSaberPluginId = "GuildSaber";
        private const string NalulunaLevelDetailPluginId = "NalulunaLevelDetail";

        private static readonly Harmony GuildSaberHarmony = new("BeatLeader.GuildSaberCompatibility");
        private static bool _applied;
        private static bool _nalulunaHoverHintSkipLogged;

        public static void ApplyRuntimePatches() {
            if (_applied) return;

            var patchCount = 0;
            var guildSaberAssembly = GetPluginAssembly(GuildSaberPluginId, "GuildSaber.Mod", "GuildSaberMod");
            if (guildSaberAssembly == null) {
                Plugin.Log.Debug("[GuildSaberCompat] GuildSaber assembly is not loaded yet.");
            } else {
                Plugin.Log.Info("[GuildSaberCompat] GuildSaber detected; keeping its configured API environment.");
            }

            patchCount += PatchNalulunaLevelDetailHoverHintCrash();

            _applied = patchCount > 0;
            if (_applied) {
                Plugin.Log.Info($"[GuildSaberCompat] Applied {patchCount} compatibility patch(es).");
            }
        }

        private static int PatchNalulunaLevelDetailHoverHintCrash() {
            var assembly = GetPluginAssembly(NalulunaLevelDetailPluginId, "NalulunaLevelDetail");
            if (assembly == null) return 0;

            MethodInfo? method = null;
            try {
                method = assembly.GetTypes()
                    .SelectMany(type => type.GetMethods(ReflectionUtils.UniversalFlags))
                    .FirstOrDefault(candidate =>
                        candidate.Name == "LLZv7Am0Ui63K5FyNP8lOYK_RVwjnKJYZN$idfesrVi1" &&
                        candidate.GetParameters().Length == 1 &&
                        candidate.GetParameters()[0].ParameterType == typeof(StandardLevelDetailView));
            } catch (Exception ex) {
                Plugin.Log.Debug($"[GuildSaberCompat] Failed to scan NalulunaLevelDetail methods: {ex.Message}");
            }

            if (method == null) return 0;

            var prefix = new HarmonyMethod(
                typeof(GuildSaberCompatibilityPatches).GetMethod(nameof(SkipNalulunaHoverHintPrefix), ReflectionUtils.StaticFlags));
            GuildSaberHarmony.Patch(method, prefix);
            return 1;
        }

        private static Assembly? GetPluginAssembly(string pluginId, params string[] assemblyNames) {
            var metadata = PluginManager.GetPluginFromId(pluginId);
            if (metadata?.Assembly != null) return metadata.Assembly;

            return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly => {
                var name = assembly.GetName().Name;
                return assemblyNames.Any(expected => string.Equals(name, expected, StringComparison.OrdinalIgnoreCase));
            });
        }

        private static bool SkipNalulunaHoverHintPrefix() {
            if (!_nalulunaHoverHintSkipLogged) {
                Plugin.Log.Info("[GuildSaberCompat] Disabled NalulunaLevelDetail hover-hint hook for BS 1.40.8 compatibility.");
                _nalulunaHoverHintSkipLogged = true;
            }

            return false;
        }
    }
}
