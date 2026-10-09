using UnityEngine;
using UnityEngine.UI;

namespace Tylevo.FieldAttachments.Runtime
{
    public sealed partial class AttachmentOverlay
    {
        private RectTransform _menuControlPlate = null!;
        private Text _menuControlLabel = null!;
        private string _menuControlHint = "";

        // Display text only; the input owner supplies the currently configured controls.
        public string MenuControlHint
        {
            get => _menuControlHint;
            set
            {
                string text = value ?? "";
                if (_menuControlHint == text) return;
                _menuControlHint = text;
                if (_menuControlLabel == null) return;
                _menuControlLabel.text = text;
                UpdateMenuControlHint(_layoutWidth, _layoutHeight);
            }
        }

        private void BuildMenuControlHint()
        {
            _menuControlPlate = (RectTransform)Box(_root.transform, "MenuControlHint", 0, 0, 640, 32,
                new Color(.035f, .043f, .044f, .94f)).transform;
            _menuControlLabel = Label(_menuControlPlate, "Controls", _menuControlHint, 12, 4, 616, 24, 13,
                new Color(.84f, .85f, .82f, 1));
            _menuControlLabel.alignment = TextAnchor.MiddleCenter;
            _menuControlLabel.supportRichText = false;
            _menuControlLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            _menuControlLabel.resizeTextForBestFit = true;
            _menuControlLabel.resizeTextMinSize = 10;
            _menuControlLabel.resizeTextMaxSize = 13;
            HideMenuControlHint();
        }

        private void UpdateMenuControlHint(float width, float height)
        {
            if (_menuControlPlate == null) return;
            bool visible = _root.activeInHierarchy && _pointerState != null && !CrossPresentationHidden &&
                _menuControlHint.Length != 0 && width > 32 && height > 52;
            _menuControlPlate.gameObject.SetActive(visible);
            if (!visible) return;
            float plateWidth = Mathf.Min(width - 32, Mathf.Max(240, _menuControlLabel.preferredWidth + 24));
            Rect(_menuControlPlate, (width - plateWidth) / 2, height - 52, plateWidth, 32);
            Rect(_menuControlLabel.rectTransform, 12, 4, plateWidth - 24, 24);
        }

        private void HideMenuControlHint()
        { if (_menuControlPlate != null) _menuControlPlate.gameObject.SetActive(false); }
    }
}
