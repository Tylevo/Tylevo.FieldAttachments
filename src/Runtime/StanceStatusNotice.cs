using System;
using Tylevo.FieldAttachments.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Tylevo.FieldAttachments.Runtime
{
    // A main-menu child only. The owner decides when it exists and handles both callbacks.
    public sealed class StanceStatusNotice : IDisposable
    {
        private const float Width = 440, Inset = 18, TextWidth = Width - Inset * 2;
        private static readonly Color WarmWhite = new Color(.94f, .94f, .90f);
        private static readonly Color Gold = new Color(.73f, .70f, .55f);
        private readonly GameObject _root;
        private readonly RectTransform _panel, _accent;
        private readonly Text _heading, _detail, _linkLabel;
        private readonly Button _link, _dismiss;
        private readonly Font _font;
        private readonly bool _ownsFont;
        private Action? _openForge, _onDismiss;
        private bool _hasForge, _disposed;

        public StanceStatusNotice(Transform parent, Action openForge, Action dismiss)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            _openForge = openForge ?? throw new ArgumentNullException(nameof(openForge));
            _onDismiss = dismiss ?? throw new ArgumentNullException(nameof(dismiss));
            try
            {
                _font = AttachmentOverlay.GetFont(out _ownsFont);
                _root = new GameObject("Tylevo.FieldAttachments.StanceNotice", typeof(RectTransform));
                _root.layer = 5; _root.transform.SetParent(parent, false);
                var holder = (RectTransform)_root.transform;
                holder.anchorMin = Vector2.zero; holder.anchorMax = Vector2.one;
                holder.offsetMin = holder.offsetMax = Vector2.zero;
                Canvas parentCanvas = parent.GetComponentInParent<Canvas>();
                if (parentCanvas == null || parentCanvas.GetComponent<GraphicRaycaster>() == null)
                {
                    Canvas canvas = _root.AddComponent<Canvas>();
                    if (parentCanvas == null)
                    {
                        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
                        CanvasScaler scaler = _root.AddComponent<CanvasScaler>();
                        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
                    }
                    else canvas.overrideSorting = false;
                    _root.AddComponent<GraphicRaycaster>();
                }
                Image panel = Box(holder, "Panel", new Color(.035f, .039f, .036f, .90f), true);
                _panel = panel.rectTransform; Rect(_panel, 24, 110, Width, 180);
                _accent = Box(_panel, "Accent", Gold, false).rectTransform;
                Label(_panel, "Brand", "TYLEVO FIELD ATTACHMENTS", Inset, 12, TextWidth, 18, 12, new Color(.73f, .73f, .69f));
                _heading = Label(_panel, "Heading", "", Inset, 36, TextWidth, 28, 22, WarmWhite);
                _heading.fontStyle = FontStyle.Bold;
                _detail = Label(_panel, "Detail", "", Inset, 72, TextWidth, 42, 15, new Color(.77f, .78f, .74f));
                _link = MakeButton("Forge", "View on Forge", OpenForge);
                _linkLabel = _link.GetComponentInChildren<Text>();
                _dismiss = MakeButton("Dismiss", "Dismiss", Dismiss);
                _panel.gameObject.SetActive(false);
            }
            catch
            {
                _openForge = _onDismiss = null;
                if (_root != null) { _root.SetActive(false); UnityEngine.Object.Destroy(_root); }
                if (_ownsFont && _font != null) UnityEngine.Object.Destroy(_font);
                throw;
            }
        }

        public void Render(StanceNoticeContent content)
        {
            if (_disposed || _root == null) return;
            if (content == null) throw new ArgumentNullException(nameof(content));
            _heading.text = content.Heading ?? ""; _detail.text = content.Detail ?? "";
            _hasForge = !string.IsNullOrWhiteSpace(content.ForgeUrl);
            _linkLabel.text = string.IsNullOrWhiteSpace(content.LinkLabel) ? "View on Forge" : content.LinkLabel;
            _link.gameObject.SetActive(_hasForge);
            _accent.GetComponent<Image>().color = content.Ready ? Gold : new Color(.61f, .64f, .62f);
            float headingHeight = Mathf.Max(28, Mathf.Ceil(_heading.preferredHeight) + 2);
            Rect(_heading.rectTransform, Inset, 36, TextWidth, headingHeight);
            float detailTop = 36 + headingHeight + 8;
            float detailHeight = Mathf.Max(20, Mathf.Ceil(_detail.preferredHeight) + 2);
            Rect(_detail.rectTransform, Inset, detailTop, TextWidth, detailHeight);
            float buttonsTop = detailTop + detailHeight + 16;
            float linkWidth = Mathf.Min(TextWidth - 116, Mathf.Max(128, Mathf.Ceil(_linkLabel.preferredWidth) + 24));
            Rect((RectTransform)_link.transform, Inset, buttonsTop, linkWidth, 32);
            Rect(_linkLabel.rectTransform, 8, 4, linkWidth - 16, 24);
            Rect((RectTransform)_dismiss.transform, Width - Inset - 104, buttonsTop, 104, 32);
            Rect(_dismiss.GetComponentInChildren<Text>().rectTransform, 8, 4, 88, 24);
            float height = buttonsTop + 48;
            Rect(_panel, 24, 110, Width, height); Rect(_accent, 0, 0, 3, height);
            _panel.gameObject.SetActive(true);
        }

        public bool IsVisible => !_disposed && _root != null && _root.activeInHierarchy && _panel.gameObject.activeSelf;
        public void SetVisible(bool visible)
        { if (!_disposed && _root != null) _root.SetActive(visible); }

        private void OpenForge()
        { if (_hasForge && IsVisible) _openForge?.Invoke(); }
        private void Dismiss()
        { if (IsVisible) _onDismiss?.Invoke(); }

        private Button MakeButton(string name, string caption, UnityEngine.Events.UnityAction clicked)
        {
            Image image = Box(_panel, name, Color.white, true);
            Button button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(.13f, .14f, .12f, 1);
            colors.highlightedColor = new Color(.23f, .24f, .20f, 1);
            colors.pressedColor = new Color(.30f, .29f, .22f, 1);
            colors.selectedColor = colors.normalColor; colors.fadeDuration = .08f; button.colors = colors;
            Text label = Label(image.transform, "Label", caption, 8, 4, 112, 24, 14, WarmWhite);
            label.fontStyle = FontStyle.Bold; label.alignment = TextAnchor.MiddleCenter;
            button.onClick.AddListener(clicked); return button;
        }

        private static Image Box(Transform parent, string name, Color color, bool raycast)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); obj.layer = 5;
            obj.transform.SetParent(parent, false);
            Image image = obj.GetComponent<Image>(); image.color = color; image.raycastTarget = raycast; return image;
        }
        private Text Label(Transform parent, string name, string value, float x, float y, float width, float height, int size, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)); obj.layer = 5;
            obj.transform.SetParent(parent, false); Rect((RectTransform)obj.transform, x, y, width, height);
            Text text = obj.GetComponent<Text>(); text.font = _font; text.fontSize = size; text.color = color;
            text.text = value; text.supportRichText = false; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate; return text;
        }
        private static void Rect(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true; _openForge = _onDismiss = null;
            if (_link != null) _link.onClick.RemoveAllListeners();
            if (_dismiss != null) _dismiss.onClick.RemoveAllListeners();
            if (_root != null) { _root.SetActive(false); UnityEngine.Object.Destroy(_root); }
            if (_ownsFont && _font != null) UnityEngine.Object.Destroy(_font);
        }
    }
}
