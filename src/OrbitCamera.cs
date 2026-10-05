using Cinemachine;
using HarmonyLib;
using UnityEngine;

namespace CameraUnlock
{
    /// <summary>
    /// Replaces the game's third-person camera rig with a real orbit.
    ///
    /// The game drives the player's Cinemachine3rdPersonFollow like this (PlayerMovementController):
    /// SetCameraRotation moves <c>RotationXPosY</c> (y) with the mouse, clamped to GameSettings ±1.9, and puts
    /// the shoulder on a 2 m circle: <c>ShoulderOffset = (0, y, -sqrt(4 - y²))</c>; Zoom adds the wheel to
    /// <c>_currentZoom</c>, clamped to 0.25..2.25, and uses it as <c>CameraDistance</c>, which pushes the camera
    /// back horizontally. That gives 2.3–4.3 m and at most ~70° of tilt, and the tilt flattens when zoomed out.
    ///
    /// Here the camera sits on a sphere around the follow target instead: the shoulder offset is placed at
    /// (pitch, distance) and CameraDistance is 0. The game's own code still runs unchanged (its y and zoom stay
    /// within the vanilla limits, so the swan camera and the shop camera routine keep working); the orbit is
    /// written over the rig right after it, in postfixes.
    /// </summary>
    internal static class OrbitCamera
    {
        /// <summary>Radius of the game's shoulder circle, used to match the vanilla camera when the orbit takes over.</summary>
        const float GameShoulderRadius = 2f;
        /// <summary>The game turns mouse movement into y with <c>-mouseY * CameraRotateSpeed * 0.05</c>.</summary>
        const float GameMouseToY = 0.05f;
        /// <summary>Near the horizon one unit of y tilts the vanilla camera by 1/2 rad; the orbit keeps that feel.</summary>
        const float DegreesPerY = Mathf.Rad2Deg / GameShoulderRadius;

        static readonly AccessTools.FieldRef<PlayerMovementController, float> CurrentZoomRef =
            AccessTools.FieldRefAccess<PlayerMovementController, float>("_currentZoom");
        static readonly AccessTools.FieldRef<PlayerMovementController, CinemachineVirtualCamera> VcamRef =
            AccessTools.FieldRefAccess<PlayerMovementController, CinemachineVirtualCamera>("_cinemachineVC");

        static PlayerMovementController _controller;
        static bool _engaged;
        static float _pitch;
        static float _distance;
        static float _lastGameY;

        /// <summary>The local player's virtual camera, once the game has used it.</summary>
        public static CinemachineVirtualCamera Vcam { get; private set; }

        static Plugin Settings => Plugin.Instance;

        /// <summary>Prefix of PlayerMovementController.SetCameraRotation.</summary>
        internal static void BeforeRotation(PlayerMovementController controller, Vector2 mouseDelta)
        {
            Vcam = VcamRef(controller);
            if (!Settings.OrbitEnabled.Value)
                return;

            if (!_engaged || controller != _controller)
                Engage(controller);
            else if (!Mathf.Approximately(controller.RotationXPosY, _lastGameY))
                FollowGameTilt(controller); // the game moved the camera itself (e.g. the shop opening)

            float deltaY = -mouseDelta.y * ScriptableSingleton<GameSettings>.I.CameraRotateSpeed * GameMouseToY;
            _pitch = ClampPitch(_pitch + deltaY * DegreesPerY * Settings.PitchSensitivity.Value);
        }

        /// <summary>Prefix of PlayerMovementController.Zoom: the wheel changes the orbit distance, or the FOV with Shift.</summary>
        internal static void OnZoom(PlayerMovementController controller)
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) < 0.001f || !GameAccess.GameWouldZoom())
                return;

            if (MonoSingleton<InputManager>.I.IsShiftKey)
            {
                // The game ignores the wheel while Shift is held.
                if (Settings.ShiftScrollFov.Value)
                    Lens.Adjust(VcamRef(controller), scroll);
                return;
            }

            if (!Settings.OrbitEnabled.Value || !_engaged || controller != _controller)
                return;
            float step = 1f + Settings.ZoomStep.Value / 100f;
            _distance = ClampDistance(_distance * Mathf.Pow(step, -scroll));
        }

        /// <summary>Postfix of SetCameraRotation and Zoom: writes the orbit over the game's rig values.</summary>
        internal static void Apply(PlayerMovementController controller)
        {
            if (!Settings.OrbitEnabled.Value || !_engaged || controller != _controller)
                return;
            Cinemachine3rdPersonFollow follow = FollowOf(controller);
            if (follow == null)
                return;

            // The limits may have been changed in the config since the last frame.
            _pitch = ClampPitch(_pitch);
            _distance = ClampDistance(_distance);

            float radians = _pitch * Mathf.Deg2Rad;
            follow.ShoulderOffset = new Vector3(
                follow.ShoulderOffset.x,
                _distance * Mathf.Sin(radians),
                -_distance * Mathf.Cos(radians));
            follow.CameraDistance = 0f;
            _lastGameY = controller.RotationXPosY;
        }

        /// <summary>Gives the rig back to the game's own formula.</summary>
        internal static void Disengage()
        {
            if (!_engaged)
                return;
            _engaged = false;

            PlayerMovementController controller = _controller;
            _controller = null;
            if (controller == null)
                return;
            Cinemachine3rdPersonFollow follow = FollowOf(controller);
            if (follow != null)
                follow.CameraDistance = CurrentZoomRef(controller);
            // Recomputes the vanilla shoulder offset from the game's current y (the patches are idle now).
            controller.SetCameraRotation(Vector2.zero);
        }

        /// <summary>Takes over from the vanilla camera without a visible jump.</summary>
        static void Engage(PlayerMovementController controller)
        {
            _controller = controller;
            _engaged = true;
            float y = controller.RotationXPosY;
            float back = VanillaBackOffset(controller, y);
            _pitch = ClampPitch(Mathf.Atan2(y, back) * Mathf.Rad2Deg);
            _distance = ClampDistance(Mathf.Sqrt(y * y + back * back));
            _lastGameY = y;
        }

        static void FollowGameTilt(PlayerMovementController controller)
        {
            float y = controller.RotationXPosY;
            _pitch = ClampPitch(Mathf.Atan2(y, VanillaBackOffset(controller, y)) * Mathf.Rad2Deg);
            _lastGameY = y;
        }

        /// <summary>How far behind the target the vanilla camera sits, for the game's current y and zoom.</summary>
        static float VanillaBackOffset(PlayerMovementController controller, float y)
        {
            float shoulderBack = Mathf.Sqrt(Mathf.Max(0f, GameShoulderRadius * GameShoulderRadius - y * y));
            return shoulderBack + Mathf.Max(0f, CurrentZoomRef(controller));
        }

        static Cinemachine3rdPersonFollow FollowOf(PlayerMovementController controller)
        {
            CinemachineVirtualCamera vcam = VcamRef(controller);
            return vcam != null ? vcam.GetCinemachineComponent<Cinemachine3rdPersonFollow>() : null;
        }

        static float ClampPitch(float pitch)
        {
            float min = Settings.MinPitch.Value;
            float max = Mathf.Max(min, Settings.MaxPitch.Value);
            return Mathf.Clamp(pitch, min, max);
        }

        static float ClampDistance(float distance)
        {
            float min = Settings.MinDistance.Value;
            float max = Mathf.Max(min, Settings.MaxDistance.Value);
            return Mathf.Clamp(distance, min, max);
        }
    }
}
