using System;
using System.Collections.Generic;
using Tylevo.FieldAttachments.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Tylevo.FieldAttachments.Runtime
{
    public sealed partial class AttachmentOverlay
    {
        private RectTransform _menuControlPlate = null!;
        private readonly List<ControlPrompt> _controlPrompts = new List<ControlPrompt>();
        private string _menuControlHint = "";
        private RectTransform _layerControl = null!, _layerStatusPlate = null!;
        private Text _layerControlLabel = null!, _layerStatusLabel = null!;
        public event Action? LayerCycleRequested;
        public bool LayerCycleEnabled { get; set; } = true;

        // Display text only; the input owner supplies the currently configured controls.
        public string MenuControlHint
        {
            get => _menuControlHint;
            set
            {
                string text = value ?? "";
                if (_menuControlHint == text) return;
                _menuControlHint = text;
                if (_menuControlPlate == null) return;
                BuildControlPrompts();
                UpdateMenuControlHint(_layoutWidth, _layoutHeight);
            }
        }

        private void BuildMenuControlHint()
        {
            _menuControlPlate = (RectTransform)Box(_root.transform, "MenuControlHint", 0, 0, 640, 36, Color.clear).transform;
            StyleHintPlate(_menuControlPlate);
            BuildControlPrompts();
            _layerControl = (RectTransform)Box(_root.transform, "AttachmentLayer", 0, 0, 230, 36, Color.clear).transform;
            StyleHintPlate(_layerControl);
            _layerControlLabel = HintLabel(_layerControl, "Layer", "LAYER 1/2: BASIC  >", 10, 6, 210, 24, 16);
            _layerControlLabel.alignment = TextAnchor.MiddleCenter;
            _layerControlLabel.resizeTextForBestFit = true;
            _layerControlLabel.resizeTextMinSize = 12;
            _layerControlLabel.resizeTextMaxSize = 16;
            _layerStatusPlate = (RectTransform)Box(_root.transform, "MultitoolStatus", 0, 0, 640, 32, Color.clear).transform;
            StyleHintPlate(_layerStatusPlate);
            _layerStatusLabel = HintLabel(_layerStatusPlate, "Reason", "", 18, 4, 604, 24, 16);
            HideMenuControlHint();
        }

        private Text HintLabel(Transform parent, string name, string value, float x, float y, float width, float height, int size)
        {
            Text label = Label(parent, name, value, x, y, width, height, size, _headerMode.color);
            label.font = _headerMode.font; label.fontStyle = _headerMode.fontStyle;
            return label;
        }

        private void StyleHintPlate(RectTransform plate)
        {
            var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(HudBackdrop));
            backdrop.transform.SetParent(plate, false);
            RectTransform rect = (RectTransform)backdrop.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            HudBackdrop shade = backdrop.GetComponent<HudBackdrop>();
            shade.color = new Color(.035f, .039f, .036f, .78f); shade.raycastTarget = false;
            foreach (bool top in new[] { true, false })
            {
                RectTransform rule = (RectTransform)Box(plate, top ? "TopRule" : "BottomRule", 0, 0, 0, 1,
                    new Color(_headerMode.color.r, _headerMode.color.g, _headerMode.color.b, .13f)).transform;
                rule.anchorMin = new Vector2(0, top ? 1 : 0); rule.anchorMax = new Vector2(1, top ? 1 : 0);
                rule.pivot = new Vector2(.5f, top ? 1 : 0); rule.anchoredPosition = Vector2.zero;
            }
        }

        private void BuildControlPrompts()
        {
            foreach (var prompt in _controlPrompts)
            { prompt.Root.gameObject.SetActive(false); UnityEngine.Object.Destroy(prompt.Root.gameObject); }
            _controlPrompts.Clear();
            foreach (string part in _menuControlHint.Split('|'))
            {
                string source = part.Trim();
                if (source.Length == 0) continue;
                string key = "", caption = source;
                foreach (var pair in new[] { (" HOLD MENU", "Hold for menu"), (" TOGGLE MENU", "Toggle menu"),
                    (" SELECT", "Select"), (" CLOSE LIST", "Close list"), (" MOUNT", "Mounting point"), (" LAYER", "Switch layer") })
                {
                    if (!source.EndsWith(pair.Item1, StringComparison.Ordinal)) continue;
                    key = source.Substring(0, source.Length - pair.Item1.Length).Trim(); caption = pair.Item2; break;
                }
                if (key == "MENU DISABLED") { key = ""; caption = "Menu key disabled"; }
                RectTransform holder = (RectTransform)Box(_menuControlPlate, "Prompt", 0, 0, 200, 26, Color.clear).transform;
                RectTransform keycap = (RectTransform)Box(holder, "Keycap", 0, 0, 64, 26, new Color(1, 1, 1, .045f)).transform;
                Image[] edges = Outline(keycap, 64, 26, new Color(_headerMode.color.r, _headerMode.color.g, _headerMode.color.b, .45f));
                Text keyLabel = HintLabel(keycap, "Key", key, 6, 2, 52, 22, 14);
                keyLabel.alignment = TextAnchor.MiddleCenter;
                keyLabel.resizeTextForBestFit = true; keyLabel.resizeTextMinSize = 10; keyLabel.resizeTextMaxSize = 14;
                Text action = HintLabel(holder, "Action", caption, 72, 1, 128, 24, 16);
                action.alignment = TextAnchor.MiddleLeft;
                keycap.gameObject.SetActive(key.Length != 0);
                _controlPrompts.Add(new ControlPrompt { Root = holder, Keycap = keycap, Key = keyLabel, Action = action, Edges = edges });
            }
        }

        private Vector2 LayoutControlPrompts(float maximumWidth)
        {
            float naturalWidth = 36;
            foreach (var prompt in _controlPrompts)
                naturalWidth += Mathf.Ceil(prompt.Action.preferredWidth) + (prompt.Key.text.Length == 0 ? 0 : Mathf.Ceil(prompt.Key.preferredWidth) + 24) + 24;
            float plateWidth = Mathf.Min(maximumWidth, Mathf.Max(240, naturalWidth - 24));
            float contentWidth = plateWidth - 36, x = 18, y = 5;
            foreach (var prompt in _controlPrompts)
            {
                float captionWidth = Mathf.Ceil(prompt.Action.preferredWidth);
                float keyWidth = prompt.Key.text.Length == 0 ? 0 : Mathf.Min(Mathf.Ceil(prompt.Key.preferredWidth) + 16, contentWidth - captionWidth - 8);
                float promptWidth = captionWidth + (keyWidth > 0 ? keyWidth + 8 : 0);
                if (x > 18 && x + promptWidth > plateWidth - 18) { x = 18; y += 32; }
                Rect(prompt.Root, x, y, promptWidth, 26);
                Rect(prompt.Keycap, 0, 0, keyWidth, 26);
                Rect(prompt.Key.rectTransform, 6, 2, Mathf.Max(0, keyWidth - 12), 22);
                Rect(prompt.Action.rectTransform, keyWidth > 0 ? keyWidth + 8 : 0, 1, captionWidth, 24);
                Rect(prompt.Edges[0].rectTransform, 0, 0, keyWidth, 1);
                Rect(prompt.Edges[1].rectTransform, 0, 25, keyWidth, 1);
                Rect(prompt.Edges[2].rectTransform, 0, 0, 1, 26);
                Rect(prompt.Edges[3].rectTransform, keyWidth - 1, 0, 1, 26);
                x += promptWidth + 24;
            }
            return new Vector2(plateWidth, y + 31);
        }

        private sealed class ControlPrompt
        {
            public RectTransform Root = null!, Keycap = null!;
            public Text Key = null!, Action = null!;
            public Image[] Edges = null!;
        }

        private void UpdateMenuControlHint(float width, float height)
        {
            if (_menuControlPlate == null) return;
            bool visible = _root.activeInHierarchy && _pointerState != null && !CrossPresentationHidden &&
                width > 480 && height > 200;
            _menuControlPlate.gameObject.SetActive(visible && _menuControlHint.Length != 0);
            _layerControl.gameObject.SetActive(visible);
            _layerStatusPlate.gameObject.SetActive(visible && _displayLayer == 1);
            if (!visible) return;
            float layerWidth = 230;
            bool stacked = width - 48 - layerWidth - 12 < 420;
            Vector2 plate = _menuControlHint.Length == 0 ? Vector2.zero : LayoutControlPrompts(width - 48 - (stacked ? 0 : layerWidth + 12));
            float totalWidth = stacked ? Mathf.Max(plate.x, layerWidth) : plate.x + (plate.x > 0 ? 12 : 0) + layerWidth;
            float left = (width - totalWidth) / 2;
            float bottom = height - 20;
            float plateTop = bottom - plate.y - (stacked && plate.x > 0 ? 48 : 0);
            Rect(_menuControlPlate, left, plateTop, plate.x, plate.y);
            Rect(_layerControl, left + totalWidth - layerWidth, bottom - 36, layerWidth, 36);
            _layerControlLabel.text = _displayLayer == 0 ? "LAYER 1/2: BASIC  >" : "LAYER 2/2: MULTITOOL  >";
            _layerControlLabel.color = LayerCycleEnabled && !LayerTransitionActive ? _headerMode.color : _muted;
            if (_displayLayer == 1)
            {
                float statusWidth = Mathf.Min(width - 48, Mathf.Max(640, totalWidth));
                _layerStatusLabel.text = LayerStatusText();
                Rect(_layerStatusLabel.rectTransform, 18, 4, statusWidth - 36, 24);
                float statusHeight = Mathf.Max(24, Mathf.Ceil(_layerStatusLabel.preferredHeight));
                Rect(_layerStatusLabel.rectTransform, 18, 4, statusWidth - 36, statusHeight);
                Rect(_layerStatusPlate, (width - statusWidth) / 2, Mathf.Min(plateTop, bottom - 36) - statusHeight - 12, statusWidth, statusHeight + 8);
            }
        }

        private string LayerStatusText()
        {
            if (_pointerState == null) return "";
            var group = _popupKind != null ? _popupGroup : _pointerFocus ?? _pointerState.Group;
            if (AttachmentLayers.Layer(group) != _displayLayer) group = _pointerState.Group;
            var slot = _pointerState.Current(group)?.Slot;
            string denial = AttachmentLayers.Denial(_pointerState.Snapshot, slot ??
                new SlotObservation { Group = group, Required = false, Locked = false });
            if (denial.Length != 0) return denial;
            return slot == null ? "Multitool ready. No " + AttachmentLayers.Label(group).ToLowerInvariant() + " mounting point." :
                slot.Required == true && slot.Installed == null ? "Required slot is empty. Repair this weapon outside the raid." :
                slot.Required == true ? "Required part: choose a carried replacement. Removal alone is unavailable." :
                "Multitool ready. Choose a carried part.";
        }

        private void UpdateLayerFocus()
        {
            if (_displayLayer != 1 || _popupKind != null) return;
            float x = _pointerPosition.x * _layoutWidth, y = _pointerPosition.y * _layoutHeight;
            if (InsideComparison(x, y) || InsideMenuControls(x, y)) return;
            for (int i = _groups.Length - 1; i >= 0; i--)
            {
                var plane = _groups[i].Projection;
                if (plane != null && plane.TryUnmap(x, y, out _, out _))
                { _pointerFocus = _groups[i].Group; return; }
            }
        }

        private bool InsideMenuControls(float x, float y) => InsideComparisonRect(_menuControlPlate, x, y) ||
            InsideComparisonRect(_layerControl, x, y) || InsideComparisonRect(_layerStatusPlate, x, y);

        private void RaiseMenuControls()
        {
            if (_menuControlPlate != null) _menuControlPlate.SetAsLastSibling();
            if (_layerStatusPlate != null) _layerStatusPlate.SetAsLastSibling();
            if (_layerControl != null) _layerControl.SetAsLastSibling();
        }

        private void HideMenuControlHint()
        {
            if (_menuControlPlate != null) _menuControlPlate.gameObject.SetActive(false);
            if (_layerControl != null) _layerControl.gameObject.SetActive(false);
            if (_layerStatusPlate != null) _layerStatusPlate.gameObject.SetActive(false);
        }
    }
}
