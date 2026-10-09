using System;
using System.Collections.Generic;
using Tylevo.FieldAttachments.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Tylevo.FieldAttachments.Runtime
{
    public sealed partial class AttachmentOverlay
    {
        public event Action<AttachmentAction,AttachmentGroup,string,string>? ActionClicked;
        public event Action? RefreshClicked;
        public bool LiveActionsEnabled { get; set; }
        private enum PointerKind { Slots, Candidates, ChooseSlot, ChooseItem, Uninstall, Previous, Next, Close, Refresh }
        private sealed class PointerTarget
        {
            public PointerKind Kind;
            public AttachmentAction Action;
            public AttachmentGroup Group;
            public string Path = "", Item = "", Surface = "";
            public string Key => Surface + "|" + Kind + "|" + Action + "|" + Group + "|" + Path + "|" + Item;
        }
        private sealed class PointerRow
        {
            public RectTransform Root = null!;
            public Image Background = null!, Icon = null!;
            public Text Name = null!, Detail = null!;
            public PointerTarget? Target;
            public ItemObservation? Item;
        }
        private RectTransform _pointerVisual = null!, _popup = null!;
        private Image[] _popupEdges=Array.Empty<Image>();
        private Text _popupTitle = null!, _popupPageLabel = null!;
        private RectTransform _previousButton=null!, _nextButton=null!;
        private readonly List<CandidateObservation> _popupChoices=new List<CandidateObservation>();
        private AttachmentGroup? _pointerFocus;
        private float _popupHeight=QuickSwapMenu.Height, _popupFooter;
        private bool _continuePopupAfterSlot, _popupPlaced;
        private Vector2 _popupHome;
        private PointerTarget? _positionPreview;
        private readonly PointerRow[] _pointerRows = new PointerRow[6];
        private readonly PointerGesture _pointerGesture = new PointerGesture();
        private SelectionState? _pointerState;
        private bool _pointerActive, _pointerIcons;
        private Vector2 _pointerPosition = new Vector2(.5f, .5f);
        private PointerKind? _popupKind;
        private AttachmentGroup _popupGroup;
        private int _popupPage, _pointerGeneration, _popupCount;
        private const float PopupWidth = QuickSwapMenu.Width;
        public bool PointerReady => _root.activeInHierarchy && _layoutWidth > 0 && _layoutHeight > 0 && _anchors.IsCurrent();

        private void BuildPointerUi()
        {
            foreach (GroupPanel panel in _groups)
                Label(panel.Installed.Background.transform, "DropdownArrow", "v", 80, 1, 14, 18, 13, Color.white)
                    .gameObject.AddComponent<ProjectedCardMesh>();
            // Include the new arrow in each group's shared projection.
            foreach (GroupPanel panel in _groups) panel.Meshes = panel.Root.GetComponentsInChildren<ProjectedCardMesh>(true);
            _popup = (RectTransform)Box(_root.transform, "AttachmentDropdown", 0, 0, PopupWidth, _popupHeight, new Color(.035f,.043f,.044f,1)).transform;
            _popupEdges=Outline(_popup, PopupWidth, _popupHeight, _accent);
            _popupTitle = Label(_popup, "Title", "", 8, 7, 178, 24, 13, Color.white);
            MenuButton("Close","X",QuickSwapMenu.Close,out _);
            for (int i=0; i<_pointerRows.Length; i++)
            {
                var row = new PointerRow();
                var cell=QuickSwapMenu.Cell(i);
                row.Root = (RectTransform)Box(_popup, "Choice", cell.X,cell.Y,cell.Width,cell.Height, new Color(.08f,.09f,.087f,1)).transform;
                row.Background = row.Root.GetComponent<Image>();
                row.Icon = Box(row.Root, "Icon", 8, 4, 86, 51, Color.white).GetComponent<Image>(); row.Icon.preserveAspect = true;
                row.Name = Label(row.Root,"Name","",4,57,94,27,12,Color.white); row.Name.alignment=TextAnchor.MiddleCenter;
                row.Detail = Label(row.Root,"State","",4,85,94,14,11,_muted); row.Detail.alignment=TextAnchor.MiddleCenter;
                _pointerRows[i] = row;
            }
            _previousButton=(RectTransform)MenuButton("Previous","<",new SelectorRect(8,350,32,30),out _).transform.parent;
            _nextButton=(RectTransform)MenuButton("Next",">",new SelectorRect(184,350,32,30),out _).transform.parent;
            _popupPageLabel = Label(_popup,"Page","",44,350,136,30,12,_muted); _popupPageLabel.alignment=TextAnchor.MiddleCenter;
            _popup.gameObject.SetActive(false);
            _pointerVisual = (RectTransform)Box(_root.transform,"AttachmentCursor",0,0,1,1,Color.clear).transform;
            Box(_pointerVisual,"OutlineH",-9,-2,19,5,Color.black); Box(_pointerVisual,"OutlineV",-2,-9,5,19,Color.black);
            Box(_pointerVisual,"H",-8,-1,17,3,Color.white); Box(_pointerVisual,"V",-1,-8,3,17,Color.white);
            _pointerVisual.gameObject.SetActive(false);
        }
        private Text MenuButton(string name,string text,SelectorRect r,out Image background)
        {
            var box=Box(_popup,name,r.X,r.Y,r.Width,r.Height,_crossPalette.Normal);
            background=box.GetComponent<Image>(); Outline(box.transform,r.Width,r.Height,_muted);
            var label=Label(box.transform,"Label",text,0,0,r.Width,r.Height,14,_crossPalette.Text);
            label.alignment=TextAnchor.MiddleCenter; return label;
        }
        private SlotObservation? PreviewSlot(AttachmentGroup group,SlotObservation? original)
        {
            if(_positionPreview==null || _positionPreview.Group!=group || _pointerState==null) return original;
            return _pointerState.GroupSlots(group).Find(s=>s.Slot.Path==_positionPreview.Path)?.Slot ?? original;
        }
        private void PointerViewChanged(SelectionState state, bool icons)
        {
            bool resume=_continuePopupAfterSlot && ReferenceEquals(_pointerState,state) && ReferenceEquals(_boundSnapshot,state.Snapshot);
            var group=_popupGroup; var home=_popupHome; bool placed=_popupPlaced;
            ClosePopup(); // Rescans and result refreshes invalidate old row identities.
            _pointerState=state; _pointerIcons=icons; _pointerGeneration++; _pointerGesture.Cancel();
            if(resume) { _popupHome=home; _popupPlaced=placed; _popupKind=PointerKind.Candidates; _popupGroup=group; FillPopup(); }
        }
        public void ResetPointer()
        {
            _pointerActive=false; ClosePopup(); _pointerGesture.Cancel(); HoverHex(null); HoverCross(null);
            if (_pointerVisual != null) _pointerVisual.gameObject.SetActive(false);
        }
        private void ClosePopup()
        {
            _popupKind=null; _popupPage=0; _popupPlaced=false; _continuePopupAfterSlot=false; _positionPreview=null; _pointerGeneration++; _pointerGesture.Cancel();
            if (_popup != null) _popup.gameObject.SetActive(false);
            foreach (var row in _pointerRows) if (row != null) { row.Item=null; row.Target=null; row.Icon.sprite=null; }
        }
        public bool HandlePointer(bool active, float dx, float dy, bool down, bool up, bool back, float scroll)
        {
            bool wasActive=_pointerActive;
            _pointerActive=active && !CrossPresentationHidden && _pointerState != null && _layoutWidth > 0 && _layoutHeight > 0;
            if (!_pointerActive) { ResetPointer(); return false; }
            _pointerVisual.gameObject.SetActive(true);
            _pointerPosition.x=Mathf.Clamp01(_pointerPosition.x+dx/_layoutWidth);
            _pointerPosition.y=Mathf.Clamp01(_pointerPosition.y-dy/_layoutHeight);
            if (back) { ClosePopup(); return false; }
            if (_popupKind != null && scroll != 0) ChangePage(scroll > 0 ? -1 : 1);
            PlacePointerUi();
            PointerTarget? target=HitPointer();
            if(target!=null && (target.Kind==PointerKind.Candidates || target.Kind==PointerKind.ChooseItem || target.Kind==PointerKind.Uninstall)) _pointerFocus=target.Group;
            _positionPreview=target?.Kind==PointerKind.ChooseSlot ? target : null;
            if (down && target==null && _popupKind!=null && !InsidePopup(_pointerPosition.x*_layoutWidth,_pointerPosition.y*_layoutHeight)) ClosePopup();
            foreach (var row in _pointerRows)
            {
                bool hover=row.Target!=null && row.Target.Key==target?.Key;
                row.Background.color=ExperimentalCrossSelectors ? (hover ? _crossPalette.Selected : _crossPalette.Disabled) :
                    hover ? new Color(.24f,.26f,.22f,.98f) : new Color(.08f,.09f,.087f,.98f);
            }
            HoverHex(target);
            HoverCross(target);
            if (down && wasActive) _pointerGesture.Press(target?.Key,_pointerGeneration);
            if (!up || !_pointerGesture.Release(target?.Key,_pointerGeneration) || target == null) return false;
            return Activate(target);
        }
        private bool Activate(PointerTarget target)
        {
            if (_pointerState == null) return false;
            switch (target.Kind)
            {
                case PointerKind.Slots: case PointerKind.Candidates:
                    if (_popupKind==null || _popupGroup!=target.Group)
                    { if(_popupKind==null || _popupGroup!=target.Group) _popupPlaced=false;
                      _popupKind=PointerKind.Candidates; _popupGroup=target.Group; _pointerFocus=target.Group; _popupPage=0; _positionPreview=null; FillPopup(); }
                    return false;
                case PointerKind.Close: ClosePopup(); return false;
                case PointerKind.Refresh: ClosePopup(); RefreshClicked?.Invoke(); return true;
                case PointerKind.Previous: ChangePage(-1); return false;
                case PointerKind.Next: ChangePage(1); return false;
                case PointerKind.ChooseItem: case PointerKind.Uninstall:
                    ClosePopup();
                    ActionClicked?.Invoke(target.Kind==PointerKind.Uninstall ? AttachmentAction.Uninstall : target.Action,
                        target.Group,target.Path,target.Item);
                    return true;
                default:
                    bool changed=_pointerState.Highlight(target.Group,target.Path);
                    if(changed) { _popupKind=PointerKind.Candidates; _popupPage=0; _positionPreview=null; _continuePopupAfterSlot=true; FillPopup(); }
                    return changed;
            }
        }
        private void ChangePage(int direction)
        {
            int page=QuickSwapMenu.Page(_popupPage+direction,_popupCount);
            if (page != _popupPage) { _popupPage=page; FillPopup(); }
        }
        private void FillPopup()
        {
            if (_pointerState == null || _popupKind==null) return;
            _pointerGeneration++; _pointerGesture.Cancel();
            SlotViewModel? current=_pointerState.Current(_popupGroup);
            _popupChoices.Clear();
            if(current!=null) _popupChoices.AddRange(current.Candidates.FindAll(c=>c.Evidence==CandidateEvidence.NativeFilterPass &&
                AttachmentRequest.ScopeDenial(AttachmentRequest.ChoiceAction(current.Slot),current.Slot,c.Item).Length==0));
            _popupCount=current==null ? 0 : _popupChoices.Count+1;
            _popupPage=QuickSwapMenu.Page(_popupPage,_popupCount);
            _popupTitle.text=_groups[(int)_popupGroup].Label;
            _popupPageLabel.text=_popupChoices.Count==0 ? "No carried matches" : (_popupPage+1)+" / "+QuickSwapMenu.Pages(_popupCount);
            int visible=Math.Min(6,Math.Max(0,_popupCount-_popupPage*6));
            _popupFooter=QuickSwapMenu.Footer(visible); _popupHeight=QuickSwapMenu.PopupHeight(visible);
            Rect(_previousButton,8,_popupFooter,32,30); Rect(_nextButton,184,_popupFooter,32,30);
            _previousButton.gameObject.SetActive(_popupPage>0); _nextButton.gameObject.SetActive(_popupPage+1<QuickSwapMenu.Pages(_popupCount));
            Rect(_popupPageLabel.rectTransform,44,_popupFooter,136,30);
            // Outline edges use the same dynamic bounds as hit testing.
            Rect(_popupEdges[0].rectTransform,0,0,PopupWidth,1); Rect(_popupEdges[1].rectTransform,0,_popupHeight-1,PopupWidth,1);
            Rect(_popupEdges[2].rectTransform,0,0,1,_popupHeight); Rect(_popupEdges[3].rectTransform,PopupWidth-1,0,1,_popupHeight);
            for (int i=0; i<6; i++)
            {
                var row=_pointerRows[i]; int index=_popupPage*6+i;
                row.Root.gameObject.SetActive(index<_popupCount); row.Target=null; row.Item=null; row.Icon.sprite=null; row.Icon.enabled=false;
                if (index>=_popupCount) continue;
                var slot=current!; bool uninstall=index==0;
                var candidate=uninstall ? null : _popupChoices[index-1];
                row.Item=uninstall ? null : candidate!.Item;
                row.Name.text=uninstall ? "NONE" : row.Item!.Name;
                var actionItem=uninstall ? slot.Slot.Installed : row.Item;
                string denial=actionItem==null ? "Empty" : ActionDenial(slot,actionItem,uninstall,candidate?.Evidence);
                row.Detail.text=denial.Length!=0 ? (LiveActionsEnabled ? "UNAVAILABLE" : "LIVE OFF") :
                    uninstall ? "REMOVE" : AttachmentRequest.ChoiceAction(slot.Slot).ToString().ToUpperInvariant();
                if(uninstall && actionItem==null) row.Detail.text="SELECTED";
                row.Name.color=denial.Length==0 ? Color.white : ExperimentalCrossSelectors ? _crossPalette.Selected : _muted;
                row.Background.color=ExperimentalCrossSelectors ? _crossPalette.Disabled : new Color(.08f,.09f,.087f,.98f);
                if (denial.Length==0) row.Target=new PointerTarget { Kind=uninstall ? PointerKind.Uninstall : PointerKind.ChooseItem,
                    Action=AttachmentRequest.ChoiceAction(slot.Slot), Group=_popupGroup, Path=slot.Slot.Path, Item=actionItem!.Id };

            }
            _popup.gameObject.SetActive(true); PollPointerIcons(); PlacePointerUi();
        }
        private string ActionDenial(SlotViewModel slot,ItemObservation item,bool uninstall,CandidateEvidence? evidence=null)
        {
            if (!LiveActionsEnabled) return "Live actions OFF";
            string denial=AttachmentRequest.ScopeDenial(uninstall ? AttachmentAction.Uninstall : AttachmentRequest.ChoiceAction(slot.Slot),slot.Slot,item);
            return denial.Length!=0 ? denial : !uninstall && evidence!=CandidateEvidence.NativeFilterPass ? "Native fit unknown" : "";
        }
        private void PollPointerIcons()
        {
            if (!_pointerIcons || _popupKind==null) return;
            foreach (var row in _pointerRows)
            {
                if (row.Item==null || row.Icon.enabled) continue;
                Sprite? icon=_resources.Icon(row.Item.NativeItem,row.Item.Id);
                if (icon != null) { row.Icon.sprite=icon; row.Icon.enabled=true; }
            }
        }
        private void PlacePointerUi()
        {
            if (!_pointerActive) return;
            if (_popupKind != null)
            {
                GroupPanel panel=_groups[(int)_popupGroup];
                bool compact=ExperimentalCrossSelectors || ExperimentalHexSelectors;
                PanelPoint p=panel.Projection?.Map(compact ? .5f : .27f,compact ? .4f : .32f) ?? new PanelPoint(panel.Root.anchoredPosition.x+116,-panel.Root.anchoredPosition.y+61,1);
                if(!_popupPlaced)
                {
                    var edge=panel.Projection?.Map(1,.2f) ?? p;
                    var left=panel.Projection?.Map(0,.2f) ?? p;
                    _popupHome=new Vector2(edge.X+8+PopupWidth>_layoutWidth-12 ? left.X-PopupWidth-8 : edge.X+8,p.Y);
                    _popupPlaced=true;
                }
                Rect(_popup,Mathf.Clamp(_popupHome.x,24,Mathf.Max(24,_layoutWidth-PopupWidth-24)),
                    Mathf.Clamp(_popupHome.y,24,Mathf.Max(24,_layoutHeight-_popupHeight-24)),PopupWidth,_popupHeight);
                _popup.SetAsLastSibling();
            }
            Rect(_pointerVisual,_pointerPosition.x*_layoutWidth,_pointerPosition.y*_layoutHeight,1,1);
            _pointerVisual.SetAsLastSibling();
        }
        private bool InsidePopup(float x,float y)
        {
            float px=x-_popup.anchoredPosition.x,py=y+_popup.anchoredPosition.y;
            return px>=0 && px<PopupWidth && py>=0 && py<_popupHeight;
        }
        public bool CyclePointerPosition()
        {
            if(!_pointerActive || _pointerState==null || CrossPresentationHidden) return false;
            AttachmentGroup group=_popupKind!=null ? _popupGroup : _pointerFocus ?? _pointerState.Group;
            if(_popupKind==null)
            {
                float x=_pointerPosition.x*_layoutWidth,y=_pointerPosition.y*_layoutHeight;
                // The hovered card wins, even over a disabled REMOVE or its plain heading.
                for(int g=3;g>=0;g--)
                {
                    var projection=_groups[g].Projection;
                    if(projection!=null && projection.TryUnmap(x,y,out float u,out float v) && u>=0 && u<=1 && v>=0 && v<=1)
                    { group=(AttachmentGroup)g; break; }
                }
            }
            var current=_pointerState.Current(group);
            if(current==null || _pointerState.GroupSlots(group).Count<2) return false;
            _pointerState.Highlight(group,current.Slot.Path); _pointerState.NextSlot(1); _pointerFocus=group;
            _pointerGesture.Cancel(); _pointerGeneration++; _positionPreview=null;
            _popupPage=0; _continuePopupAfterSlot=_popupKind!=null;
            if(_popupKind!=null) FillPopup();
            return true;
        }
        private PointerTarget? HitPointer()
        {
            if (_pointerState==null) return null;
            float x=_pointerPosition.x*_layoutWidth,y=_pointerPosition.y*_layoutHeight;
            if (_popupKind != null)
            {
                float px=x-_popup.anchoredPosition.x,py=y+_popup.anchoredPosition.y;
                if (InsidePopup(x,y))
                {
                    if(QuickSwapMenu.Close.Contains(px,py)) return new PointerTarget {Kind=PointerKind.Close};
                    if(new SelectorRect(8,_popupFooter,32,30).Contains(px,py) && _popupPage>0) return new PointerTarget {Kind=PointerKind.Previous};
                    if(new SelectorRect(184,_popupFooter,32,30).Contains(px,py) && _popupPage+1<QuickSwapMenu.Pages(_popupCount)) return new PointerTarget {Kind=PointerKind.Next};
                    int row=QuickSwapMenu.Row(px,py);
                    return row>=0 && _pointerRows[row].Root.gameObject.activeSelf ? _pointerRows[row].Target : null;
                }
            }
            if (ExperimentalCrossSelectors) return HitCross(x,y);
            if (ExperimentalHexSelectors) return HitHex(x,y);
            foreach (GroupPanel panel in _groups)
            {
                float u,v;
                if (panel.Projection != null) { if (!panel.Projection.TryUnmap(x,y,out u,out v)) continue; }
                else { u=(x-panel.Root.anchoredPosition.x)/OverlayLayout.PanelWidth; v=(y+panel.Root.anchoredPosition.y)/OverlayLayout.PanelHeight; }
                float px=u*OverlayLayout.PanelWidth,py=v*OverlayLayout.PanelHeight;
                if (px<0 || px>OverlayLayout.PanelWidth || py<0 || py>OverlayLayout.PanelHeight) continue;
                var slot=_pointerState.Current(panel.Group); if (slot==null) return null;
                if (py<=55) return new PointerTarget {Kind=PointerKind.Candidates,Group=panel.Group};
                if (py>=61 && py<=161)
                {
                    if (px>=12 && px<=108) return new PointerTarget {Kind=PointerKind.Candidates,Group=panel.Group};
                    for (int i=0;i<3;i++) if (px>=116+i*104 && px<=212+i*104 && panel.Candidates[i].Item != null && LiveActionsEnabled &&
                        ActionDenial(slot,panel.Candidates[i].Item!,false,slot.Candidates.Find(c=>c.Item.Id==panel.Candidates[i].Item!.Id)?.Evidence).Length==0)
                        return new PointerTarget {Kind=PointerKind.ChooseItem,Action=AttachmentRequest.ChoiceAction(slot.Slot),Group=panel.Group,Path=slot.Slot.Path,Item=panel.Candidates[i].Item!.Id};
                }
            }
            return null;
        }
    }
}
