using System;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace CameraUnlock
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("OnTogether.exe")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ontogether.cameraunlock";
        public const string PluginName = "CameraUnlock";
        public const string PluginVersion = "1.0.3";

        const float ToastDuration = 2f;
        const float FreeCamHintDuration = 7f;

        internal static Plugin Instance { get; private set; }
        internal static ManualLogSource Log { get; private set; }

        internal ConfigEntry<bool> OrbitEnabled;
        internal ConfigEntry<float> MinDistance;
        internal ConfigEntry<float> MaxDistance;
        internal ConfigEntry<float> MinPitch;
        internal ConfigEntry<float> MaxPitch;
        internal ConfigEntry<float> ZoomStep;
        internal ConfigEntry<float> PitchSensitivity;
        internal ConfigEntry<bool> CameraCollision;

        internal ConfigEntry<float> FieldOfView;
        internal ConfigEntry<bool> ShiftScrollFov;

        internal ConfigEntry<KeyboardShortcut> FreeCamToggleKey;
        internal ConfigEntry<float> FreeCamSpeed;
        internal ConfigEntry<float> FreeCamFastMultiplier;
        internal ConfigEntry<float> FreeCamSlowMultiplier;
        internal ConfigEntry<float> FreeCamLookSensitivity;
        internal ConfigEntry<bool> FreeCamInvertY;
        internal ConfigEntry<float> FreeCamSmoothing;
        internal ConfigEntry<bool> HideUiInFreeCam;
        internal ConfigEntry<KeyCode> FreeCamForward;
        internal ConfigEntry<KeyCode> FreeCamBack;
        internal ConfigEntry<KeyCode> FreeCamLeft;
        internal ConfigEntry<KeyCode> FreeCamRight;
        internal ConfigEntry<KeyCode> FreeCamUp;
        internal ConfigEntry<KeyCode> FreeCamDown;
        internal ConfigEntry<KeyCode> FreeCamFast;
        internal ConfigEntry<KeyCode> FreeCamSlow;

        internal ConfigEntry<KeyboardShortcut> HideUiKey;
        internal ConfigEntry<bool> HideWorldSpaceUi;
        internal ConfigEntry<bool> ShowHints;

        Harmony _harmony;
        readonly FreeCamera _freeCamera = new FreeCamera();
        readonly UiHider _uiHider = new UiHider();
        bool _uiHiddenByFreeCam;

        readonly ColliderSwitch _collider = new ColliderSwitch();

        static string _toast;
        static float _toastUntil;
        float _hintUntil;
        GUIStyle _hintStyle;
        static string _lastError;

        void Awake()
        {
            Instance = this;
            Log = Logger;
            BindConfig();

            _harmony = new Harmony(PluginGuid);
            TryPatch(typeof(SetCameraRotationPatch), "camera tilt");
            TryPatch(typeof(ZoomPatch), "camera zoom");
            TryPatch(typeof(InputManagerUpdatePatch), "input blocking for the free camera");

            OrbitEnabled.SettingChanged += (_, __) =>
            {
                if (!OrbitEnabled.Value)
                    OrbitCamera.Disengage();
            };
            Log.LogInfo($"{PluginName} {PluginVersion} loaded. Free camera: {FreeCamToggleKey.Value}, hide UI: {HideUiKey.Value}");
        }

        void BindConfig()
        {
            OrbitEnabled = Config.Bind("Orbit", "Enabled", true,
                "Replace the game's short zoom and tilt range with a free orbit around your character " +
                "(mouse wheel = distance, right mouse button = tilt). Off: the game's own camera.");
            MinDistance = Config.Bind("Orbit", "MinDistance", 0.4f,
                new ConfigDescription("Closest camera distance in metres (the game: about 2.3).",
                    new AcceptableValueRange<float>(0.1f, 5f)));
            MaxDistance = Config.Bind("Orbit", "MaxDistance", 40f,
                new ConfigDescription("Farthest camera distance in metres (the game: about 4.3).",
                    new AcceptableValueRange<float>(3f, 300f)));
            MinPitch = Config.Bind("Orbit", "MinPitch", -75f,
                new ConfigDescription("Lowest camera angle in degrees; negative looks up at your character from below.",
                    new AcceptableValueRange<float>(-89f, 0f)));
            MaxPitch = Config.Bind("Orbit", "MaxPitch", 89f,
                new ConfigDescription("Highest camera angle in degrees; 89 is a straight top-down view.",
                    new AcceptableValueRange<float>(0f, 89f)));
            ZoomStep = Config.Bind("Orbit", "ZoomStepPercent", 12f,
                new ConfigDescription("How much one wheel notch changes the distance, in percent.",
                    new AcceptableValueRange<float>(2f, 50f)));
            PitchSensitivity = Config.Bind("Orbit", "TiltSensitivity", 1f,
                new ConfigDescription("Multiplier for vertical mouse movement (1 = like the game near the horizon).",
                    new AcceptableValueRange<float>(0.1f, 5f)));
            CameraCollision = Config.Bind("Orbit", "CameraCollision", true,
                "Keep the camera in front of walls and the ground, as the game does. Turn off to see through walls.");

            FieldOfView = Config.Bind("Lens", "FieldOfView", 0f,
                new ConfigDescription("Vertical field of view of the normal camera in degrees; 0 = the game's default.",
                    new AcceptableValueRange<float>(0f, Lens.MaxFov)));
            ShiftScrollFov = Config.Bind("Lens", "ShiftWheelChangesFov", true,
                "Shift + mouse wheel changes the field of view (both cameras); Shift + middle click resets it.");

            FreeCamToggleKey = Config.Bind("FreeCamera", "ToggleKey", new KeyboardShortcut(KeyCode.F6),
                "Turns the free camera on/off (ignored while typing). Your character stays in place.");
            FreeCamSpeed = Config.Bind("FreeCamera", "Speed", 4f,
                new ConfigDescription("Flying speed in metres per second (the mouse wheel scales it while flying).",
                    new AcceptableValueRange<float>(0.1f, 100f)));
            FreeCamFastMultiplier = Config.Bind("FreeCamera", "FastMultiplier", 4f,
                new ConfigDescription("Speed multiplier while the Fast key is held.", new AcceptableValueRange<float>(1f, 50f)));
            FreeCamSlowMultiplier = Config.Bind("FreeCamera", "SlowMultiplier", 0.25f,
                new ConfigDescription("Speed multiplier while the Slow key is held.", new AcceptableValueRange<float>(0.01f, 1f)));
            FreeCamLookSensitivity = Config.Bind("FreeCamera", "LookSensitivity", 2f,
                new ConfigDescription("Mouse look sensitivity.", new AcceptableValueRange<float>(0.1f, 20f)));
            FreeCamInvertY = Config.Bind("FreeCamera", "InvertY", false, "Invert vertical mouse look.");
            FreeCamSmoothing = Config.Bind("FreeCamera", "Smoothing", 0f,
                new ConfigDescription("Smooth, cinematic movement: 0 = none, 0.9 = very smooth.",
                    new AcceptableValueRange<float>(0f, 0.98f)));
            HideUiInFreeCam = Config.Bind("FreeCamera", "HideUiWhileFlying", false,
                "Hide the interface automatically while the free camera is on.");
            FreeCamForward = Config.Bind("FreeCamera.Keys", "Forward", KeyCode.W, "Fly forward.");
            FreeCamBack = Config.Bind("FreeCamera.Keys", "Back", KeyCode.S, "Fly back.");
            FreeCamLeft = Config.Bind("FreeCamera.Keys", "Left", KeyCode.A, "Fly left.");
            FreeCamRight = Config.Bind("FreeCamera.Keys", "Right", KeyCode.D, "Fly right.");
            FreeCamUp = Config.Bind("FreeCamera.Keys", "Up", KeyCode.E, "Fly straight up.");
            FreeCamDown = Config.Bind("FreeCamera.Keys", "Down", KeyCode.Q, "Fly straight down.");
            FreeCamFast = Config.Bind("FreeCamera.Keys", "Fast", KeyCode.LeftShift, "Hold to fly faster.");
            FreeCamSlow = Config.Bind("FreeCamera.Keys", "Slow", KeyCode.LeftControl, "Hold to fly slower.");

            HideUiKey = Config.Bind("Screenshots", "HideUiKey", new KeyboardShortcut(KeyCode.F7),
                "Hides/shows the game's interface (ignored while typing).");
            HideWorldSpaceUi = Config.Bind("Screenshots", "HideWorldSpaceUi", false,
                "Also hide interface elements placed in the world (signs, screens and labels drawn with canvases).");
            ShowHints = Config.Bind("Screenshots", "ShowHints", true,
                "Show short on-screen hints (controls, field of view, speed). Never drawn while the interface is hidden.");
        }

        void TryPatch(Type patch, string feature)
        {
            try
            {
                _harmony.CreateClassProcessor(patch).Patch();
            }
            catch (Exception e)
            {
                Log.LogError($"Could not patch the game for {feature}; that part will not work: {e}");
            }
        }

        void Update()
        {
            if (GameAccess.IsAnyTextFieldFocused())
                return;

            ReadShortcuts(FreeCamToggleKey.Value, HideUiKey.Value, out bool freeCam, out bool hideUi);
            if (freeCam)
                ToggleFreeCamera();
            if (hideUi)
            {
                if (_uiHider.IsHidden)
                    _uiHider.Show();
                else
                    _uiHider.Hide();
                _uiHiddenByFreeCam = false;
            }
            if (ShiftScrollFov.Value && Input.GetMouseButtonDown(2) &&
                (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
            {
                if (FreeCamera.IsActive)
                    _freeCamera.ResetFov();
                else
                    Lens.Reset();
            }
        }

        /// <summary>
        /// Checks two hotkeys that may share a key. Shift+F7 also satisfies a plain F7 shortcut, so when both fire on
        /// the same key only the one with more modifiers counts (both do if they have as many).
        /// </summary>
        static void ReadShortcuts(KeyboardShortcut first, KeyboardShortcut second, out bool firstPressed, out bool secondPressed)
        {
            firstPressed = Pressed(first);
            secondPressed = Pressed(second);
            if (!firstPressed || !secondPressed || first.MainKey != second.MainKey)
                return;
            int difference = first.Modifiers.Count() - second.Modifiers.Count();
            firstPressed = difference >= 0;
            secondPressed = difference <= 0;
        }

        // KeyboardShortcut.IsDown() does not fire while any other key is held (e.g. W while walking or flying), so only
        // the shortcut's own keys are checked.
        static bool Pressed(KeyboardShortcut shortcut)
        {
            KeyCode mainKey = shortcut.MainKey;
            if (mainKey == KeyCode.None || !Input.GetKeyDown(mainKey))
                return false;
            foreach (KeyCode modifier in shortcut.Modifiers)
            {
                if (!Input.GetKey(modifier))
                    return false;
            }
            return true;
        }

        void LateUpdate()
        {
            try
            {
                if (!_freeCamera.LateTick())
                    OnFreeCameraEnded("Free camera: off (the camera changed)");
                Lens.Tick(OrbitCamera.Vcam);
                _collider.Apply(OrbitCamera.Vcam, CameraCollision.Value);
                _uiHider.Tick();
            }
            catch (Exception e)
            {
                LogOnce(e);
            }
        }

        /// <summary>Logs each distinct error once instead of every frame.</summary>
        internal static void LogOnce(Exception e)
        {
            string message = e.GetType().Name + ": " + e.Message;
            if (message == _lastError)
                return;
            _lastError = message;
            Log.LogError(e);
        }

        void ToggleFreeCamera()
        {
            if (FreeCamera.IsActive)
            {
                _freeCamera.Exit();
                OnFreeCameraEnded("Free camera: off");
                return;
            }
            if (!_freeCamera.TryEnter(out string error))
            {
                Toast("Free camera: " + error);
                return;
            }
            if (HideUiInFreeCam.Value && !_uiHider.IsHidden)
            {
                _uiHider.Hide();
                _uiHiddenByFreeCam = true;
            }
            _hintUntil = Time.unscaledTime + FreeCamHintDuration;
            _toastUntil = 0f;
        }

        void OnFreeCameraEnded(string message)
        {
            if (_uiHiddenByFreeCam)
                _uiHider.Show();
            _uiHiddenByFreeCam = false;
            _hintUntil = 0f;
            Toast(message);
        }

        internal static void Toast(string message)
        {
            _toast = message;
            _toastUntil = Time.unscaledTime + ToastDuration;
        }

        string FreeCamHint()
        {
            return $"Free camera — {FreeCamForward.Value}/{FreeCamLeft.Value}/{FreeCamBack.Value}/{FreeCamRight.Value} fly, " +
                   $"{FreeCamUp.Value}/{FreeCamDown.Value} up/down, hold right mouse to look, " +
                   $"{FreeCamFast.Value} fast, {FreeCamSlow.Value} slow, wheel = speed, Shift + wheel = zoom (FOV), " +
                   $"{HideUiKey.Value} hide UI, {FreeCamToggleKey.Value} back to the character";
        }

        void OnGUI()
        {
            if (!ShowHints.Value || _uiHider.IsHidden)
                return;
            float now = Time.unscaledTime;
            string text = now < _toastUntil ? _toast : now < _hintUntil ? FreeCamHint() : null;
            if (string.IsNullOrEmpty(text))
                return;

            if (_hintStyle == null)
            {
                _hintStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true,
                    richText = false
                };
                _hintStyle.normal.textColor = Color.white;
            }
            float scale = Mathf.Max(1f, Screen.height / 1080f);
            _hintStyle.fontSize = Mathf.RoundToInt(16 * scale);
            _hintStyle.padding = new RectOffset(Mathf.RoundToInt(12 * scale), Mathf.RoundToInt(12 * scale),
                Mathf.RoundToInt(8 * scale), Mathf.RoundToInt(8 * scale));

            float width = Mathf.Min(Screen.width - 32f, 900f * scale);
            float height = _hintStyle.CalcHeight(new GUIContent(text), width);
            GUI.Box(new Rect((Screen.width - width) / 2f, 24f * scale, width, height), text, _hintStyle);
        }

        void OnDestroy()
        {
            try
            {
                _freeCamera.Exit();
                _uiHider.Show();
                OrbitCamera.Disengage();
                Lens.Restore();
                _collider.Restore();
            }
            catch (Exception e)
            {
                // The scene may already be torn down when the game quits.
                Log.LogDebug("Cleanup skipped: " + e.Message);
            }
            if (_harmony != null)
                _harmony.UnpatchSelf();
        }
    }

    /// <summary>Switches the player camera's obstacle avoidance off and back, remembering the game's state.</summary>
    internal sealed class ColliderSwitch
    {
        Cinemachine.CinemachineCollider _collider;
        bool _gameEnabled;

        public void Apply(Cinemachine.CinemachineVirtualCamera vcam, bool enabled)
        {
            if (vcam == null)
                return;
            if (_collider == null || _collider.gameObject != vcam.gameObject)
            {
                _collider = vcam.GetComponent<Cinemachine.CinemachineCollider>();
                if (_collider == null)
                    return;
                _gameEnabled = _collider.enabled;
            }
            bool wanted = enabled && _gameEnabled;
            if (_collider.enabled != wanted)
                _collider.enabled = wanted;
        }

        public void Restore()
        {
            if (_collider != null)
                _collider.enabled = _gameEnabled;
        }
    }

    [HarmonyPatch(typeof(PlayerMovementController), nameof(PlayerMovementController.SetCameraRotation))]
    static class SetCameraRotationPatch
    {
        static void Prefix(PlayerMovementController __instance, Vector2 mouseDelta)
        {
            try
            {
                OrbitCamera.BeforeRotation(__instance, mouseDelta);
            }
            catch (Exception e)
            {
                Plugin.LogOnce(e);
            }
        }

        static void Postfix(PlayerMovementController __instance)
        {
            try
            {
                OrbitCamera.Apply(__instance);
            }
            catch (Exception e)
            {
                Plugin.LogOnce(e);
            }
        }
    }

    [HarmonyPatch(typeof(PlayerMovementController), "Zoom")]
    static class ZoomPatch
    {
        static bool Prefix(PlayerMovementController __instance)
        {
            // While flying, the wheel belongs to the free camera.
            if (FreeCamera.IsActive)
                return false;
            try
            {
                OrbitCamera.OnZoom(__instance);
            }
            catch (Exception e)
            {
                Plugin.LogOnce(e);
            }
            return true;
        }

        static void Postfix(PlayerMovementController __instance)
        {
            try
            {
                OrbitCamera.Apply(__instance);
            }
            catch (Exception e)
            {
                Plugin.LogOnce(e);
            }
        }
    }

    [HarmonyPatch(typeof(InputManager), "Update")]
    static class InputManagerUpdatePatch
    {
        static void Postfix(InputManager __instance)
        {
            try
            {
                InputBlocker.AfterInputUpdate(__instance);
            }
            catch (Exception e)
            {
                Plugin.LogOnce(e);
            }
        }
    }
}
