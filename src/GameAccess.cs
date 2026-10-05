using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CameraUnlock
{
    /// <summary>
    /// Cached, exception-safe access to the game objects the mod reads.
    /// The game's singleton getters fall back to FindAnyObjectByType when the instance is missing
    /// (e.g. in the main menu), so lookups of missing instances are throttled.
    /// </summary>
    internal static class GameAccess
    {
        const float LookupInterval = 1f;

        static TextChannelManager _chat;
        static float _nextChatLookup;

        static TextChannelManager Chat
        {
            get
            {
                if (_chat == null && Time.unscaledTime >= _nextChatLookup)
                {
                    _nextChatLookup = Time.unscaledTime + LookupInterval;
                    _chat = NetworkSingleton<TextChannelManager>.I;
                }
                return _chat;
            }
        }

        /// <summary>The local player's root transform, or null outside a session.</summary>
        public static Transform LocalPlayer
        {
            get
            {
                TextChannelManager chat = Chat;
                return chat != null ? chat.MainPlayer : null;
            }
        }

        public static bool IsPlayerOnSwan()
        {
            if (LocalPlayer == null)
                return false;
            SwanManager swans = NetworkSingleton<SwanManager>.I;
            return swans != null && swans.IsPlayerOnSwan();
        }

        /// <summary>True while any text field has keyboard focus (chat, journal, to-do list...).</summary>
        public static bool IsAnyTextFieldFocused()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
                return false;
            GameObject selected = eventSystem.currentSelectedGameObject;
            if (selected == null)
                return false;
            TMP_InputField tmpInput = selected.GetComponent<TMP_InputField>();
            if (tmpInput != null && tmpInput.isFocused)
                return true;
            InputField legacyInput = selected.GetComponent<InputField>();
            return legacyInput != null && legacyInput.isFocused;
        }

        /// <summary>True while the mouse is over a UI element (the chat, a panel...).</summary>
        public static bool IsPointerOverUi()
        {
            EventSystem eventSystem = EventSystem.current;
            return eventSystem != null && eventSystem.IsPointerOverGameObject();
        }

        /// <summary>
        /// Mirrors the check at the start of PlayerMovementController.Zoom: the wheel zooms unless the
        /// mouse is over a UI element that is not one of the game's overlay masks.
        /// </summary>
        public static bool GameWouldZoom()
        {
            if (MonoSingleton<OverlayManager>.I.ResolutionMode == ResolutionMode.Desk)
                return false;
            GameObject overUi = MonoSingleton<InputManager>.I.MouseOverUIObject;
            return overUi == null || MonoSingleton<UIManager>.I.OverlayMasks.Contains(overUi.transform);
        }
    }
}
