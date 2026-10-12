using System.Collections.Generic;
using UnityEngine;

namespace Tylevo.FieldAttachments.Runtime
{
    public sealed partial class AttachmentOverlay
    {
        private const float LayerRevealDuration = .24f;
        private float _layerRevealStarted = float.NegativeInfinity;
        private readonly List<CanvasGroup> _layerSurfaces = new List<CanvasGroup>();
        public bool LayerTransitionActive => Time.unscaledTime - _layerRevealStarted < LayerRevealDuration;

        private void BuildLayerReveal()
        {
            AddLayerSurface(_crossRoot.gameObject);
            foreach (var panel in _hexPanels) AddLayerSurface(panel.Root.gameObject);
            foreach (var panel in _groups)
            {
                AddLayerSurface(panel.Root.gameObject);
                AddLayerSurface(panel.Leader.First.gameObject);
                AddLayerSurface(panel.Leader.Second.gameObject);
                AddLayerSurface(panel.Leader.Dot.gameObject);
            }
        }

        private void AddLayerSurface(GameObject surface)
        {
            var group = surface.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false; group.interactable = false;
            _layerSurfaces.Add(group);
        }

        private void BeginLayerReveal()
        {
            // The new layer is already bound. Only opacity changes; projected homes stay fixed.
            ResetPointer();
            _layerRevealStarted = Time.unscaledTime;
            SetLayerOpacity(0);
        }

        private void UpdateLayerReveal()
        {
            if (float.IsNegativeInfinity(_layerRevealStarted)) return;
            float t = Mathf.Clamp01((Time.unscaledTime - _layerRevealStarted) / LayerRevealDuration);
            SetLayerOpacity(t * t * (3 - 2 * t));
            if (t >= 1) _layerRevealStarted = float.NegativeInfinity;
        }

        private void EndLayerReveal()
        {
            _layerRevealStarted = float.NegativeInfinity;
            SetLayerOpacity(1);
        }

        private void SetLayerOpacity(float alpha)
        {
            foreach (var surface in _layerSurfaces) surface.alpha = alpha;
        }
    }
}
