using System.Collections.Generic;
using System.Reflection;
using Cinemachine;
using UnityEngine;

namespace CameraUnlock
{
    /// <summary>
    /// A detached fly camera. Cinemachine's brain is switched off (the game does the same for the swan
    /// camera) and the main camera is moved directly; the character stays where it is and ignores input.
    /// </summary>
    internal sealed class FreeCamera
    {
        const float MaxPitch = 89f;
        const float SpeedStepPerNotch = 1.2f;
        const float MinSpeedScale = 0.05f;
        const float MaxSpeedScale = 20f;

        public static bool IsActive { get; private set; }

        Camera _camera;
        CinemachineBrain _brain;
        bool _brainWasEnabled;
        float _gameFov;

        Vector3 _position;
        Vector3 _targetPosition;
        float _yaw;
        float _targetYaw;
        float _pitch;
        float _targetPitch;
        float _fov;
        float _speedScale = 1f;

        static Plugin Settings => Plugin.Instance;

        public bool TryEnter(out string error)
        {
            error = null;
            if (GameAccess.LocalPlayer == null)
            {
                error = "join a world first";
                return false;
            }
            if (GameAccess.IsPlayerOnSwan())
            {
                error = "not available on the swan boat";
                return false;
            }
            Camera camera = Camera.main;
            CinemachineBrain brain = camera != null ? camera.GetComponent<CinemachineBrain>() : null;
            if (brain == null || !brain.enabled)
            {
                error = "the game camera is not available right now";
                return false;
            }

            _camera = camera;
            _brain = brain;
            _brainWasEnabled = brain.enabled;
            brain.enabled = false;

            Transform transform = camera.transform;
            _position = _targetPosition = transform.position;
            Vector3 euler = transform.rotation.eulerAngles;
            _yaw = _targetYaw = euler.y;
            _pitch = _targetPitch = Mathf.Clamp(Mathf.DeltaAngle(0f, euler.x), -MaxPitch, MaxPitch);
            _fov = _gameFov = camera.fieldOfView;
            IsActive = true;
            return true;
        }

        public void Exit()
        {
            if (!IsActive)
                return;
            IsActive = false;
            if (_camera != null)
                _camera.fieldOfView = _gameFov;
            if (_brain != null)
                _brain.enabled = _brainWasEnabled;
            _camera = null;
            _brain = null;
        }

        public void ResetFov()
        {
            _fov = _gameFov;
            Plugin.Toast($"Field of view: {_fov:0}° (default)");
        }

        /// <summary>Runs in LateUpdate. Returns false when the camera went away and free mode ended.</summary>
        public bool LateTick()
        {
            if (!IsActive)
                return true;
            if (_camera == null || _brain == null || GameAccess.LocalPlayer == null)
            {
                Exit();
                return false;
            }
            _brain.enabled = false;

            float deltaTime = Time.unscaledDeltaTime;
            if (!GameAccess.IsAnyTextFieldFocused())
                ReadInput(deltaTime);

            float smoothing = Settings.FreeCamSmoothing.Value;
            float follow = smoothing <= 0f ? 1f : 1f - Mathf.Pow(smoothing, deltaTime * 10f);
            _position = Vector3.Lerp(_position, _targetPosition, follow);
            _yaw = Mathf.Lerp(_yaw, _targetYaw, follow);
            _pitch = Mathf.Lerp(_pitch, _targetPitch, follow);

            _camera.transform.SetPositionAndRotation(_position, Quaternion.Euler(_pitch, _yaw, 0f));
            _camera.fieldOfView = _fov;
            return true;
        }

        void ReadInput(float deltaTime)
        {
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            // Look around while the right mouse button is held, like the game's own camera
            // (the game keeps locking the cursor for that button).
            if (Input.GetMouseButton(1))
            {
                float sensitivity = Settings.FreeCamLookSensitivity.Value;
                float invert = Settings.FreeCamInvertY.Value ? -1f : 1f;
                _targetYaw += Input.GetAxisRaw("Mouse X") * sensitivity;
                _targetPitch = Mathf.Clamp(_targetPitch - Input.GetAxisRaw("Mouse Y") * sensitivity * invert,
                    -MaxPitch, MaxPitch);
            }

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.001f && !GameAccess.IsPointerOverUi())
            {
                if (shift)
                {
                    _fov = Mathf.Clamp(_fov - scroll * Lens.StepPerNotch, Lens.MinFov, Lens.MaxFov);
                    Plugin.Toast($"Field of view: {_fov:0}°");
                }
                else
                {
                    _speedScale = Mathf.Clamp(_speedScale * Mathf.Pow(SpeedStepPerNotch, scroll), MinSpeedScale, MaxSpeedScale);
                    Plugin.Toast($"Camera speed: {Settings.FreeCamSpeed.Value * _speedScale:0.##} m/s");
                }
            }

            Vector3 planar = Vector3.zero;
            if (Input.GetKey(Settings.FreeCamForward.Value)) planar.z += 1f;
            if (Input.GetKey(Settings.FreeCamBack.Value)) planar.z -= 1f;
            if (Input.GetKey(Settings.FreeCamRight.Value)) planar.x += 1f;
            if (Input.GetKey(Settings.FreeCamLeft.Value)) planar.x -= 1f;
            float vertical = 0f;
            if (Input.GetKey(Settings.FreeCamUp.Value)) vertical += 1f;
            if (Input.GetKey(Settings.FreeCamDown.Value)) vertical -= 1f;
            if (planar.sqrMagnitude > 1f)
                planar.Normalize();

            float speed = Settings.FreeCamSpeed.Value * _speedScale;
            if (Input.GetKey(Settings.FreeCamFast.Value))
                speed *= Settings.FreeCamFastMultiplier.Value;
            if (Input.GetKey(Settings.FreeCamSlow.Value))
                speed *= Settings.FreeCamSlowMultiplier.Value;

            Vector3 move = Quaternion.Euler(_targetPitch, _targetYaw, 0f) * planar + Vector3.up * vertical;
            _targetPosition += move * (speed * deltaTime);
        }
    }

    /// <summary>
    /// Clears the game's per-frame input state while the free camera is active, the same way the game does
    /// while you type in the chat (InputManager.Update with TaskManager.IsCancelInputs). The right mouse button
    /// is kept so the game keeps locking the cursor for mouse look; Escape and Enter keep the menu and chat usable.
    /// </summary>
    internal static class InputBlocker
    {
        static readonly string[] KeptInputs = { "IsMouseButton1", "IsMouseButton1Up", "IsEscapeKeyDown", "EnterKeyDown", "EnterKeyUp" };
        static List<FieldInfo> _fields;

        /// <summary>Postfix of InputManager.Update.</summary>
        internal static void AfterInputUpdate(InputManager input)
        {
            if (!FreeCamera.IsActive)
                return;
            if (_fields == null)
                _fields = FindInputFields();
            foreach (FieldInfo field in _fields)
                field.SetValue(input, field.FieldType == typeof(bool) ? (object)false : 0f);
        }

        static List<FieldInfo> FindInputFields()
        {
            var fields = new List<FieldInfo>();
            foreach (FieldInfo field in typeof(InputManager).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
            {
                // Auto-property backing fields: "<Vertical>k__BackingField", "<IsSpaceKeyDown>k__BackingField"...
                if (!field.Name.StartsWith("<") || !field.Name.EndsWith(">k__BackingField"))
                    continue;
                if (field.FieldType != typeof(bool) && field.FieldType != typeof(float))
                    continue;
                string property = field.Name.Substring(1, field.Name.IndexOf('>') - 1);
                if (System.Array.IndexOf(KeptInputs, property) >= 0)
                    continue;
                fields.Add(field);
            }
            return fields;
        }
    }
}
