using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CameraUnlock
{
    /// <summary>
    /// Hides the game's interface for clean screenshots by switching off the root canvases (and their raycasters,
    /// so invisible buttons cannot be clicked). Canvases that appear while hidden are picked up by a periodic rescan.
    /// </summary>
    internal sealed class UiHider
    {
        const float RescanInterval = 0.5f;

        readonly List<Behaviour> _disabled = new List<Behaviour>();
        float _nextScan;

        public bool IsHidden { get; private set; }

        public void Hide()
        {
            if (IsHidden)
                return;
            IsHidden = true;
            Scan();
        }

        public void Show()
        {
            if (!IsHidden)
                return;
            IsHidden = false;
            foreach (Behaviour behaviour in _disabled)
            {
                if (behaviour != null)
                    behaviour.enabled = true;
            }
            _disabled.Clear();
        }

        public void Tick()
        {
            if (IsHidden && Time.unscaledTime >= _nextScan)
                Scan();
        }

        void Scan()
        {
            _nextScan = Time.unscaledTime + RescanInterval;
            bool includeWorldSpace = Plugin.Instance.HideWorldSpaceUi.Value;
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!canvas.enabled || !canvas.isRootCanvas)
                    continue;
                if (canvas.renderMode == RenderMode.WorldSpace && !includeWorldSpace)
                    continue;
                canvas.enabled = false;
                _disabled.Add(canvas);
                foreach (BaseRaycaster raycaster in canvas.GetComponents<BaseRaycaster>())
                {
                    if (!raycaster.enabled)
                        continue;
                    raycaster.enabled = false;
                    _disabled.Add(raycaster);
                }
            }
        }
    }
}
