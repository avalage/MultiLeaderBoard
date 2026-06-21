using BeatLeader.Models;
using BeatLeader.Utils;
using UnityEngine;
using UnityEngine.XR;
using Zenject;

namespace BeatLeader.Replayer.Binding {
    internal class ReplayerStickControls : MonoBehaviour {
        private const float StickDeadzone = 0.45f;
        private const float SpeedTickSeconds = 0.05f;
        private const float SpeedChangePerSecond = 0.85f;
        private const float MinSpeedMultiplier = 0.1f;
        private const float MaxSpeedMultiplier = 2.0f;
        private const float SeekTickSeconds = 0.05f;
        private const float MaxSeekSecondsPerSecond = 14.0f;
        private const float MaxElapsedSeconds = 0.15f;

        [Inject] private readonly IBeatmapTimeController _timeController = null!;

        private InputDevice _leftDevice;
        private InputDevice _rightDevice;
        private float _lastSpeedChangeTime;
        private float _lastSeekTime;
        private bool _leftStickClickWasPressed;

        private void Awake() {
            RefreshDevices();
            _lastSpeedChangeTime = Time.unscaledTime;
            _lastSeekTime = Time.unscaledTime;
        }

        private void Update() {
            if (InputUtils.UsesFPFC) return;

            RefreshDevicesIfNeeded();
            HandleLeftStickSpeed();
            HandleLeftStickClick();
            HandleRightStickSeek();
        }

        private void RefreshDevicesIfNeeded() {
            if (!_leftDevice.isValid || !_rightDevice.isValid) {
                RefreshDevices();
            }
        }

        private void RefreshDevices() {
            _leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            _rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        }

        private void HandleLeftStickSpeed() {
            if (!TryGetVerticalAxis(_leftDevice, out var axis)) return;

            var now = Time.unscaledTime;
            var magnitude = Mathf.Abs(axis);
            if (magnitude < StickDeadzone) {
                _lastSpeedChangeTime = now;
                return;
            }

            var elapsed = Mathf.Min(now - _lastSpeedChangeTime, MaxElapsedSeconds);
            if (elapsed < SpeedTickSeconds) return;

            var normalizedMagnitude = SmoothStickMagnitude(magnitude);
            var direction = axis > 0f ? 1f : -1f;
            var speed = Mathf.Clamp(
                _timeController.SongSpeedMultiplier + SpeedChangePerSecond * normalizedMagnitude * elapsed * direction,
                MinSpeedMultiplier,
                MaxSpeedMultiplier);

            _timeController.SetSpeedMultiplier(speed);
            _lastSpeedChangeTime = now;
        }

        private void HandleLeftStickClick() {
            if (!_leftDevice.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out var pressed)) return;

            if (pressed && !_leftStickClickWasPressed) {
                var initialSpeed = Mathf.Clamp(
                    _timeController.SongStartSpeedMultiplier,
                    MinSpeedMultiplier,
                    MaxSpeedMultiplier);
                _timeController.SetSpeedMultiplier(initialSpeed);
            }

            _leftStickClickWasPressed = pressed;
        }

        private void HandleRightStickSeek() {
            if (!TryGetVerticalAxis(_rightDevice, out var seekAxis)) return;

            var seekMagnitude = Mathf.Abs(seekAxis);
            var now = Time.unscaledTime;

            if (seekMagnitude < StickDeadzone) {
                _lastSeekTime = now;
                return;
            }

            var elapsed = Mathf.Min(now - _lastSeekTime, MaxElapsedSeconds);
            if (elapsed < SeekTickSeconds) return;

            var normalizedMagnitude = SmoothStickMagnitude(seekMagnitude);
            var direction = seekAxis > 0f ? 1f : -1f;
            var seekSeconds = MaxSeekSecondsPerSecond * normalizedMagnitude * elapsed * direction;
            var targetTime = _timeController.SongTime + seekSeconds;

            _timeController.Rewind(targetTime);
            _lastSeekTime = now;
        }

        private static float SmoothStickMagnitude(float magnitude) {
            var normalized = Mathf.InverseLerp(StickDeadzone, 1.0f, magnitude);
            return Mathf.SmoothStep(0.0f, 1.0f, normalized);
        }

        private static bool TryGetVerticalAxis(InputDevice device, out float value) {
            value = 0.0f;
            if (!device.TryGetFeatureValue(CommonUsages.primary2DAxis, out var axis)) return false;

            value = axis.y;
            return true;
        }
    }
}
