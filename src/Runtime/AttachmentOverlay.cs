using System;
using System.Collections.Generic;
using System.Text;
using Tylevo.FieldAttachments.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Tylevo.FieldAttachments.Runtime
{
    public sealed partial class AttachmentOverlay : IDisposable
    {
        private readonly GameObject _root;
        private readonly Font _font;
        private readonly bool _ownsFont;
        private readonly NativeResources _resources;
        private readonly WeaponAnchorReader _anchors;
        private readonly RectTransform _canvasRect;
        private float _layoutWidth, _layoutHeight;
        private int _lastProjected = -1;
        private bool _lastFollowing;
        private readonly PanelProjection[] _projections = new PanelProjection[4];
        private RaidSnapshot? _boundSnapshot;
        private int _sampleFrame = -1;
        private string _layoutReason = "Not drawn";
        public bool FollowWeapon { get; set; } = true;
        private bool _cameraFacingCards = true, _drawnCameraFacing;
        public bool CameraFacingCards
        {
            get => _cameraFacingCards;
            set { if (_cameraFacingCards == value) return; _cameraFacingCards = value; ResetPointer(); _sampleFrame = -1; }
        }
        public float PerspectiveStrength { get; set; } = .55f;
        public float ClockwiseDegrees { get; set; } = 5;
        private float _drawnPerspective = float.NaN, _drawnClockwise = float.NaN;
        private readonly Text _weapon, _actionStatus;
        private readonly GroupPanel[] _groups = new GroupPanel[4];
        private readonly Color _panel = new Color(0.035f, 0.043f, 0.044f, 0.86f);
        private readonly Color _muted = new Color(0.61f, 0.64f, 0.62f, 1f);
        private readonly Color _accent = new Color(0.73f, 0.70f, 0.55f, 1f);
        private readonly List<Card> _cards = new List<Card>();

        public AttachmentOverlay(NativeResources resources, ReadAccess read)
        {
            _resources = resources;
            _anchors = new WeaponAnchorReader(read);
            _font = GetFont(out _ownsFont);
            _root = new GameObject("Tylevo.FieldAttachments.Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            _canvasRect = (RectTransform)_root.transform;
            UnityEngine.Object.DontDestroyOnLoad(_root);
            Canvas canvas = _root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 220;
            CanvasScaler scaler = _root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight = 0.5f;
            CanvasGroup cg = _root.GetComponent<CanvasGroup>(); cg.blocksRaycasts = false; cg.interactable = false;
            Transform header = Box(_root.transform, "Header", 24, 24, 540, 84, _panel).transform;
            Label(header, "Title", "TYLEVO FIELD ATTACHMENTS", 16, 8, 508, 30, 24, Color.white);
            _weapon = Label(header, "Weapon", "", 16, 38, 508, 20, 15, _muted);
            _actionStatus = Label(header, "ActionStatus", "", 16, 61, 508, 17, 12, _accent);
            _groups[0] = CreateGroup(AttachmentGroup.Optic, "OPTIC");
            _groups[1] = CreateGroup(AttachmentGroup.Muzzle, "MUZZLE");
            _groups[2] = CreateGroup(AttachmentGroup.Tactical, "TACTICAL DEVICE");
            _groups[3] = CreateGroup(AttachmentGroup.Underbarrel, "FOREGRIP");
            _crossPalette=TarkovUiPalette.Read(read);
            BuildPointerUi(); BuildHexUi(); BuildCrossUi(read); BuildMenuControlHint();
        }
        public void SetVisible(bool visible) { if (!visible) { ResetPointer(); HideMenuControlHint(); } if (_root != null) _root.SetActive(visible); }
        public void SetActionStatus(SelectionState state, InstallSession session, bool poseFault, string clickStatus = "")
        { _actionStatus.text = clickStatus.Length!=0 && !session.Blocked && !poseFault ? clickStatus : OverlayFeedback.Status(state.ArmedMatches, session, poseFault); }
        private static Font GetFont(out bool owns)
        {
            owns = false;
            foreach (string name in new[] { "Arial.ttf", "LegacyRuntime.ttf" })
            {
                try { Font font = Resources.GetBuiltinResource<Font>(name); if (font != null) return font; } catch { }
            }
            Font fallback = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "DejaVu Sans", "Liberation Sans" }, 16);
            if (fallback == null) throw new InvalidOperationException("No built-in or OS UI font available");
            owns = true;
            return fallback;
        }
        private GroupPanel CreateGroup(AttachmentGroup group, string label)
        {
            // Leaders are behind every panel; their endpoints are real native slot bones.
            var leader = new Leader {
                First = Box(_root.transform, "Leader", 0, 0, 0, 1, _muted).GetComponent<Image>(),
                Second = Box(_root.transform, "Leader", 0, 0, 0, 1, _muted).GetComponent<Image>(),
                Dot = Box(_root.transform, "Anchor", 0, 0, 5, 5, _accent).GetComponent<Image>() };
            leader.First.transform.SetAsFirstSibling(); leader.Second.transform.SetAsFirstSibling(); leader.Dot.transform.SetAsFirstSibling();
            leader.Show(false);
            GameObject box = Box(_root.transform, group.ToString(), 0, 0, OverlayLayout.PanelWidth, OverlayLayout.PanelHeight, _panel);
            Outline(box.transform, OverlayLayout.PanelWidth, OverlayLayout.PanelHeight, new Color(.52f, .57f, .56f, .45f));
            Image focus = Box(box.transform, "Focus", 0, 0, OverlayLayout.PanelWidth, 2, _accent).GetComponent<Image>();
            var panel = new GroupPanel { Group = group, Label = label, Focus = focus, Root = (RectTransform)box.transform, Leader = leader,
                Header = Label(box.transform, "Header", label, 12, 10, 408, 24, 18, Color.white),
                Subheader = Label(box.transform, "Slot", "", 12, 36, 408, 19, 11, _muted) };
            panel.Installed = MakeCard(box.transform, 12, true);
            for (int i = 0; i < 3; i++) panel.Candidates[i] = MakeCard(box.transform, 116 + i * 104, false);
            Graphic[] graphics = box.GetComponentsInChildren<Graphic>(true);
            panel.Meshes = new ProjectedCardMesh[graphics.Length];
            for (int i = 0; i < graphics.Length; i++) panel.Meshes[i] = graphics[i].gameObject.AddComponent<ProjectedCardMesh>();
            return panel;
        }
        private Card MakeCard(Transform parent, float x, bool installed)
        {
            GameObject box = Box(parent, installed ? "Installed" : "Candidate", x, 61, 96, 100, new Color(0.08f, 0.09f, 0.087f, 0.93f));
            Image image = Box(box.transform, "Icon", 6, 5, 84, 51, Color.white).GetComponent<Image>(); image.preserveAspect = true; image.enabled = false;
            var card = new Card { Background = box.GetComponent<Image>(), Borders = Outline(box.transform, 96, 100, _muted), Image = image,
                Name = Label(box.transform, "Name", "", 4, 59, 88, 24, 11, Color.white),
                Tag = Label(box.transform, "Tag", "", 3, 85, 90, 13, 9, _muted),
                Placeholder = Label(box.transform, "Placeholder", "—", 6, 13, 84, 34, 16, _muted) };
            card.Name.alignment = TextAnchor.UpperCenter; card.Placeholder.alignment = TextAnchor.MiddleCenter; card.Tag.alignment = TextAnchor.UpperCenter;
            _cards.Add(card); return card;
        }
        public void Render(SelectionState state, bool icons)
        {
            PointerViewChanged(state, icons);
            if (!ReferenceEquals(_boundSnapshot, state.Snapshot) || !_anchors.IsCurrent())
            {
                if (!ReferenceEquals(_boundSnapshot?.Weapon,state.Snapshot.Weapon)) ClearCrossAttachments();
                _anchors.Bind(state.Snapshot); _boundSnapshot = state.Snapshot;
            }
            _weapon.text = state.Snapshot.WeaponName;
            foreach (GroupPanel panel in _groups)
            {
                bool focused = panel.Group == state.Group; panel.Focus.color = focused ? _accent : _muted * new Color(1, 1, 1, 0.35f);
                SlotViewModel? vm = state.Current(panel.Group);
                panel.Slot = vm?.Slot;
                int slots = state.GroupSlots(panel.Group).Count;
                panel.Header.text = (focused ? "> " : "") + panel.Label + (slots > 1 ? "  [" + (state.SlotIndex(panel.Group) + 1) + "/" + slots + "] v" : "");
                if (vm == null)
                {
                    panel.Subheader.text = "";
                    SetCard(panel.Installed, null, "NO SLOT", false, false, icons);
                    foreach (Card card in panel.Candidates) SetCard(card, null, "", false, false, icons);
                    continue;
                }
                panel.Subheader.text = vm.Slot.ParentName;
                SetCard(panel.Installed, vm.Slot.Installed, vm.Slot.Installed == null ? "EMPTY SLOT" : "INSTALLED", false, false, icons);
                int selected = state.CandidateIndex(panel.Group), page = selected / 3;
                for (int i = 0; i < 3; i++)
                {
                    int index = page * 3 + i;
                    CandidateObservation? c = index < vm.Candidates.Count ? vm.Candidates[index] : null;
                    string tag = c == null ? "" : "CANDIDATE";
                    SetCard(panel.Candidates[i], c?.Item, tag, c != null && focused && index == selected,
                        c != null && c.Item.Id == state.StagedItemId && vm.Slot.Path == state.StagedSlotPath, icons);
                }

            }
            RenderHex(state,icons);
            RenderCross(state,icons);
        }
        private void SetCard(Card c, ItemObservation? item, string tag, bool selected, bool staged, bool icons)
        {
            c.Item = item; c.Image.enabled = false;
            c.Background.gameObject.SetActive(item != null || tag.Length != 0);
            c.Name.text = item?.Name ?? "—";
            c.Tag.text = staged ? "ARMED" : selected ? "HIGHLIGHTED" : tag;
            c.Background.color = selected ? new Color(0.19f, 0.20f, 0.17f, 0.97f) : new Color(0.08f, 0.09f, 0.087f, 0.93f);
            foreach (Image border in c.Borders) border.color = staged ? new Color(.42f, .82f, .72f) : selected ? _accent : new Color(.44f, .49f, .48f, .5f);
            c.Placeholder.text = item == null ? "—" : "NO ICON";
            c.Placeholder.enabled = true;
            if (icons) UpdateIcon(c);
        }
        private Image[] Outline(Transform parent, float width, float height, Color color)
        {
            return new[] {
                Box(parent, "EdgeTop", 0, 0, width, 1, color).GetComponent<Image>(),
                Box(parent, "EdgeBottom", 0, height - 1, width, 1, color).GetComponent<Image>(),
                Box(parent, "EdgeLeft", 0, 0, 1, height, color).GetComponent<Image>(),
                Box(parent, "EdgeRight", width - 1, 0, 1, height, color).GetComponent<Image>() };
        }

        public void UpdateLayout()
        {
            if (!_root.activeInHierarchy || _sampleFrame == Time.frameCount) return;
            float width = _canvasRect.rect.width, height = _canvasRect.rect.height;
            if (width <= 0 || height <= 0) return;
            _sampleFrame = Time.frameCount;
            _drawnPerspective = PerspectiveStrength; _drawnClockwise = ClockwiseDegrees;
            _drawnCameraFacing = CameraFacingCards;
            _layoutWidth = width; _layoutHeight = height;
            UpdateMenuControlHint(width, height);
            bool current = _anchors.IsCurrent();
            if (ExperimentalCrossSelectors) { UpdateCrossLayout(width,height,current); return; }
            bool following = FollowWeapon && current;
            bool placed = false;
            Vector2 weaponPoint = new Vector2(width * .60f, height * .72f);
            _layoutReason = FollowWeapon ? "Held weapon binding unavailable" : "Display.WeaponFollowingCards OFF";
            if (following)
            {
                following = _anchors.ProjectCardPlane(width, height, PerspectiveStrength, ClockwiseDegrees,
                    out PanelProjection? plane, out weaponPoint, CameraFacingCards);
                _layoutReason = "Shared weapon plane unavailable/off-screen";
                if (following && ExperimentalHexSelectors) following=HexSelectorLayout.TryPlane(plane!,out plane);
                if (following)
                {
                    following = placed = PlaceAtHomes(plane!, width, height, weaponPoint);
                    _layoutReason = following ? (CameraFacingCards ? "Camera-facing surfaces; live weapon sway; dedicated category homes" : "Shared weapon perspective; dedicated category homes") : "Insufficient space for dedicated homes";
                }
            }
            if (!following)
            {
                // Missing geometry and the display-OFF option retain the same category homes.
                if (weaponPoint == default) weaponPoint = new Vector2(width * .60f, height * .72f);
                if (PanelProjection.TryCreate(new PanelPoint(0, 0, 1), new PanelPoint(ExperimentalHexSelectors ? HexSelectorLayout.Width : OverlayLayout.PanelWidth, 0, 1),
                    new PanelPoint(0, ExperimentalHexSelectors ? HexSelectorLayout.Height : OverlayLayout.PanelHeight, 1), out var flat))
                    placed = PlaceAtHomes(flat!, width, height, weaponPoint);
            }
            int projected = 0;
            for (int i = 0; i < _groups.Length; i++)
            {
                GroupPanel panel = _groups[i];
                panel.Projection = placed ? _projections[i] : null;
                if (ExperimentalHexSelectors)
                { if (LayoutHex(i,panel,width,height,current)) projected++; continue; }
                var position = placed ? (x: 0f, y: 0f) : OverlayLayout.Panel(panel.Group, width, height);
                Rect(panel.Root, position.x, position.y, OverlayLayout.PanelWidth, OverlayLayout.PanelHeight);
                foreach (ProjectedCardMesh mesh in panel.Meshes) mesh.SetProjection(panel.Projection, panel.Root, _canvasRect);
                Vector2 end = default;
                bool valid = current && _anchors.Project(panel.Slot, width, height, out end);
                panel.Leader.Show(valid);
                if (!valid) continue;
                Vector2 start, center;
                if (panel.Projection != null)
                {
                    if (!panel.Projection.LeaderStart(end.x, end.y, out PanelPoint edge)) { panel.Leader.Show(false); continue; }
                    PanelPoint middle = panel.Projection.Map(.5f, .5f);
                    center = new Vector2(middle.X, middle.Y); start = new Vector2(edge.X, edge.Y);
                }
                else
                {
                    Vector2 top = new Vector2(panel.Root.anchoredPosition.x, -panel.Root.anchoredPosition.y);
                    center = top + new Vector2(OverlayLayout.PanelWidth / 2, OverlayLayout.PanelHeight / 2);
                    Vector2 direction = end - center;
                    float ratio = Mathf.Max(Mathf.Abs(direction.x) / (OverlayLayout.PanelWidth / 2), Mathf.Abs(direction.y) / (OverlayLayout.PanelHeight / 2));
                    if (ratio <= 1) { panel.Leader.Show(false); continue; }
                    start = center + direction / ratio;
                }
                Vector2 delta = end - center;
                projected++;
                Vector2 elbow = start + (end - start) * .32f;
                elbow.x = start.x + Mathf.Sign(delta.x) * Mathf.Min(24, Mathf.Abs(end.x - start.x));
                Line(panel.Leader.First, start, elbow);
                Line(panel.Leader.Second, elbow, end);
                Rect(panel.Leader.Dot.rectTransform, end.x - 2.5f, end.y - 2.5f, 5, 5);
            }
            _lastProjected = projected; _lastFollowing = following;
            PlacePointerUi();
        }

        private bool PlaceAtHomes(PanelProjection plane, float width, float height, Vector2 weaponPoint)
        {
            for (int i = 0; i < _groups.Length; i++)
            {
                if (!DedicatedCardLayout.TryPlace(plane, _groups[i].Group, width, height, weaponPoint.x, weaponPoint.y, out var placed, CameraFacingCards)) return false;
                _projections[i] = placed!;
            }
            return true;
        }


        private static void Line(Image image, Vector2 start, Vector2 end)
        {
            Vector2 delta = end - start;
            RectTransform rt = image.rectTransform;
            Rect(rt, start.x, start.y, delta.magnitude, 1);
            rt.pivot = new Vector2(0, .5f);
            rt.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        public string LayoutReport()
        {
            var report = new StringBuilder("Weapon-relative 3D card planes projected into uGUI; no game camera/transform/animation writes.\n");
            report.AppendLine("Last displayed layout, before F10 rescan: frame=" + _sampleFrame +
                "; phase=UniTask.LastPreLateUpdate; placement=dedicated homes; native leaders=" + _lastProjected);
            report.AppendLine("Canvas: " + _layoutWidth + " x " + _layoutHeight + "; camera=" + _anchors.CameraName);
            report.AppendLine("Binding: " + _anchors.Status + "; current=" + _anchors.IsCurrent());
            report.AppendLine("Frame: " + _anchors.FrameName + "; following=" + _lastFollowing + "; reason=" + _layoutReason);
            report.AppendLine("Plane: " + _anchors.PlaneStatus);
            report.AppendLine("Card tuning: cameraFacing=" + _drawnCameraFacing + "; perspective=" + _drawnPerspective + "; clockwiseDegrees=" + _drawnClockwise +
                "; maxSurfacePitchDegrees=" + WeaponCardBasis.MaxSurfacePitchDegrees +
                "; maxSurfaceYawDegrees=" + WeaponCardBasis.MaxSurfaceYawDegrees);
            report.AppendLine("Selector style: " + (ExperimentalCrossSelectors ? "experimental Tarkov cross" : ExperimentalHexSelectors ? "experimental hex clusters" : "cards"));
            report.AppendLine("Pointer: active=" + _pointerActive + "; dropdown=" + _popupKind + "; group=" + _popupGroup + "; page=" + _popupPage);
            foreach (GroupPanel panel in _groups)
            {
                report.AppendLine(panel.Group + " | slot=" + panel.Slot?.Path + " | panel=" + panel.Root.anchoredPosition +
                    " | leader=" + panel.Leader.Dot.enabled + " | point=" + panel.Leader.Dot.rectTransform.anchoredPosition);
                if (panel.Projection != null)
                {
                    PanelProjection p = panel.Projection;
                    report.AppendLine("  projected bounds=" + p.Left + "," + p.Top + " -> " + p.Right + "," + p.Bottom);
                    foreach (PanelPoint point in new[] { p.Map(0, 0), p.Map(1, 0), p.Map(1, 1), p.Map(0, 1) })
                        report.AppendLine("  corner=" + point.X + "," + point.Y + " depth=" + point.Depth);
                }
            }
            AppendHexReport(report);
            AppendCrossReport(report);
            return report.ToString();
        }
        public void PollIcons() { foreach (Card c in _cards) if (c.Item != null && !c.Image.enabled) UpdateIcon(c); PollPointerIcons(); PollHexIcons(); PollCrossIcons(); }
        private void UpdateIcon(Card c)
        {
            if (c.Item == null) return;
            Sprite? sprite = _resources.Icon(c.Item.NativeItem, c.Item.Id);
            if (sprite != null) { c.Image.sprite = sprite; c.Image.enabled = true; c.Placeholder.enabled = false; }
        }
        private GameObject Box(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image)); obj.layer = 5;
            obj.transform.SetParent(parent, false); Rect((RectTransform)obj.transform, x, y, w, h);
            Image image = obj.GetComponent<Image>(); image.color = color; image.raycastTarget = false; return obj;
        }
        private Text Label(Transform parent, string name, string value, float x, float y, float w, float h, int size, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Text)); obj.layer = 5;
            obj.transform.SetParent(parent, false); Rect((RectTransform)obj.transform, x, y, w, h);
            Text text = obj.GetComponent<Text>(); text.font = _font; text.fontSize = size; text.color = color;
            text.supportRichText = false; text.raycastTarget = false; text.text = value;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }
        private static void Rect(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1); rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
        }
        public void ClearLiveReferences()
        {
            ResetPointer(); ClearHexItems(); ClearCrossItems(); _pointerState = null;
            HideMenuControlHint();
            _boundSnapshot = null; _sampleFrame = -1;
            ClearCrossAttachments(); PoseHeld=false;
            _crossHiddenReason = "";
            _anchors.Clear();
            foreach (GroupPanel panel in _groups)
            {
                panel.Slot = null; panel.Projection = null; panel.Leader.Show(false);
                foreach (ProjectedCardMesh mesh in panel.Meshes) mesh.SetProjection(null, panel.Root, _canvasRect);
            }
            foreach (Card c in _cards) { c.Item = null; c.Image.sprite = null; c.Image.enabled = false; }
        }
        public void Dispose()
        {
            ClearLiveReferences();
            if (_root != null) UnityEngine.Object.Destroy(_root);
            if (_ownsFont && _font != null) UnityEngine.Object.Destroy(_font);
        }
        private sealed class GroupPanel
        {
            public RectTransform Root = null!; public Leader Leader = null!; public SlotObservation? Slot;
            public PanelProjection? Projection; public ProjectedCardMesh[] Meshes = Array.Empty<ProjectedCardMesh>();
            public AttachmentGroup Group; public string Label = ""; public Image Focus = null!;
            public Text Header = null!, Subheader = null!; public Card Installed = null!;
            public readonly Card[] Candidates = new Card[3];
        }
        private sealed class Card
        {
            public Image Background = null!, Image = null!; public Image[] Borders = null!;
            public Text Name = null!, Tag = null!, Placeholder = null!; public ItemObservation? Item;
        }
        private sealed class Leader
        {
            public Image First = null!, Second = null!, Dot = null!;
            public void Show(bool visible) { First.enabled = Second.enabled = Dot.enabled = visible; }
        }
    }
}
