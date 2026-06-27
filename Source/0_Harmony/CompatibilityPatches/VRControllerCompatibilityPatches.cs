using System;
using HarmonyLib;
using UnityEngine;

namespace BeatLeader {
    internal static class VRControllerCompatibilityPatches {
        private static readonly Harmony Harmony = new("MultiLeaderboard.VRControllerCompatibility");
        private static bool _applied;
        private static bool _thumbstickExceptionLogged;
        private static bool _controllerFocusExceptionLogged;

        public static void ApplyRuntimePatches() {
            if (_applied) return;

            var getter = AccessTools.PropertyGetter(typeof(VRController), "thumbstick");
            if (getter == null) {
                Plugin.Log.Warn("[VRControllerCompat] Could not find VRController.thumbstick getter.");
                return;
            }

            var finalizer = new HarmonyMethod(typeof(VRControllerCompatibilityPatches).GetMethod(
                nameof(SuppressThumbstickNullReference),
                AccessTools.all));
            Harmony.Patch(getter, finalizer: finalizer);

            var updateActiveState = AccessTools.Method(
                typeof(DeactivateVRControllersOnFocusCapture),
                "UpdateVRControllerActiveState");
            if (updateActiveState == null) {
                Plugin.Log.Warn("[VRControllerCompat] Could not find DeactivateVRControllersOnFocusCapture.UpdateVRControllerActiveState.");
            } else {
                var updateFinalizer = new HarmonyMethod(typeof(VRControllerCompatibilityPatches).GetMethod(
                    nameof(SuppressControllerFocusNullReference),
                    AccessTools.all));
                Harmony.Patch(updateActiveState, finalizer: updateFinalizer);
            }

            _applied = true;
            Plugin.Log.Info("[VRControllerCompat] Installed VR controller NullReference guards.");
        }

        private static Exception? SuppressThumbstickNullReference(Exception? __exception, ref Vector2 __result) {
            if (__exception == null) {
                return null;
            }

            if (__exception is NullReferenceException) {
                __result = Vector2.zero;
                if (!_thumbstickExceptionLogged) {
                    Plugin.Log.Warn("[VRControllerCompat] Suppressed VRController.thumbstick NullReferenceException; returning Vector2.zero.");
                    _thumbstickExceptionLogged = true;
                }

                return null;
            }

            return __exception;
        }

        private static Exception? SuppressControllerFocusNullReference(Exception? __exception) {
            if (__exception == null) {
                return null;
            }

            if (__exception is NullReferenceException) {
                if (!_controllerFocusExceptionLogged) {
                    Plugin.Log.Warn("[VRControllerCompat] Suppressed DeactivateVRControllersOnFocusCapture NullReferenceException.");
                    _controllerFocusExceptionLogged = true;
                }

                return null;
            }

            return __exception;
        }
    }
}
