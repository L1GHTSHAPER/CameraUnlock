using Cinemachine;
using UnityEngine;

namespace CameraUnlock
{
    /// <summary>Field of view of the player's virtual camera: the config value, adjusted with Shift + wheel.</summary>
    internal static class Lens
    {
        public const float MinFov = 10f;
        public const float MaxFov = 120f;
        public const float StepPerNotch = 2f;

        static CinemachineVirtualCamera _vcam;
        static float _gameFov;
        static float? _wheelFov;
        static bool _modified;

        static Plugin Settings => Plugin.Instance;

        public static void Adjust(CinemachineVirtualCamera vcam, float scroll)
        {
            if (vcam == null)
                return;
            Capture(vcam);
            _wheelFov = Mathf.Clamp(TargetFov() - scroll * StepPerNotch, MinFov, MaxFov);
            Plugin.Toast($"Field of view: {_wheelFov.Value:0}°");
        }

        public static void Reset()
        {
            _wheelFov = null;
            if (_vcam != null)
                Plugin.Toast($"Field of view: {TargetFov():0}° (default)");
        }

        /// <summary>Called every frame; only touches the lens once the FOV differs from the game's.</summary>
        public static void Tick(CinemachineVirtualCamera vcam)
        {
            if (vcam == null)
                return;
            Capture(vcam);
            float target = TargetFov();
            if (!_modified && Mathf.Approximately(target, _gameFov))
                return;
            SetFov(vcam, target);
            _modified = !Mathf.Approximately(target, _gameFov);
        }

        public static void Restore()
        {
            _wheelFov = null;
            if (_vcam != null && _modified)
                SetFov(_vcam, _gameFov);
            _modified = false;
        }

        static void Capture(CinemachineVirtualCamera vcam)
        {
            if (vcam == _vcam)
                return;
            _vcam = vcam;
            _gameFov = vcam.m_Lens.FieldOfView;
            _modified = false;
        }

        static float TargetFov()
        {
            if (_wheelFov.HasValue)
                return _wheelFov.Value;
            float configured = Settings.FieldOfView.Value;
            return configured >= MinFov ? Mathf.Min(configured, MaxFov) : _gameFov;
        }

        static void SetFov(CinemachineVirtualCamera vcam, float fov)
        {
            LensSettings lens = vcam.m_Lens;
            if (Mathf.Approximately(lens.FieldOfView, fov))
                return;
            lens.FieldOfView = fov;
            vcam.m_Lens = lens;
        }
    }
}
