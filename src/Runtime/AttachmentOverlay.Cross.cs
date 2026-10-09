using System;
using System.Text;
using Tylevo.FieldAttachments.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Tylevo.FieldAttachments.Runtime
{
    public sealed partial class AttachmentOverlay
    {
        private sealed class CrossCell
        {
            public RectTransform Root=null!; public Image Background=null!,Icon=null!;
            public Image[] Edges=Array.Empty<Image>(); public Text Name=null!,Tag=null!;
            public ItemObservation? Item; public PointerTarget? Target; public bool Installed;
        }
        private sealed class CrossGroup
        {
            public RectTransform Root=null!;
            public ProjectedCardMesh[] Meshes=Array.Empty<ProjectedCardMesh>();
            public PanelProjection? Projection;
            public Text Header=null!,Caption=null!; public PointerTarget? HeaderTarget;
            public readonly CrossCell[] Cells=new CrossCell[5];
            public bool Visible;
        }
        private readonly CrossGroup[] _crossGroups=new CrossGroup[4];
        private TarkovUiPalette _crossPalette=null!;
        private RectTransform _crossRoot=null!;
        private PanelProjection? _crossProjection;
        private bool _studioCrossLayout=true;
        public bool UseStudioCrossLayout
        {
            get=>_studioCrossLayout;
            set
            {
                if(_studioCrossLayout==value) return;
                _studioCrossLayout=value; ResetPointer(); _sampleFrame=-1;
                ClearCrossAttachments();
                SetCrossGeometry();
                if(_pointerState!=null) RenderCross(_pointerState,_pointerIcons);
            }
        }
        private bool _experimentalCrossSelectors;
        public bool ExperimentalCrossSelectors
        {
            get=>_experimentalCrossSelectors;
            set
            {
                if(_experimentalCrossSelectors==value) return;
                _experimentalCrossSelectors=value; ResetPointer(); _sampleFrame=-1; _crossProjection=null;
                ClearCrossAttachments();
                ApplySelectorVisibility(); ApplyCrossPalette();
                if(_pointerState!=null) { RenderHex(_pointerState,_pointerIcons); RenderCross(_pointerState,_pointerIcons); }
            }
        }
        private void ApplySelectorVisibility()
        {
            foreach(var panel in _groups) panel.Root.gameObject.SetActive(!ExperimentalCrossSelectors && !ExperimentalHexSelectors);
            foreach(var panel in _hexPanels) panel.Root.gameObject.SetActive(!ExperimentalCrossSelectors && ExperimentalHexSelectors);
            _crossRoot.gameObject.SetActive(ExperimentalCrossSelectors && !CrossPresentationHidden);
        }
        private void BuildCrossUi(ReadAccess read)
        {
            // Palette is initialized before the shared mouse menu.
            var obj=new GameObject("CrossSelectors",typeof(RectTransform)); obj.layer=5; obj.transform.SetParent(_root.transform,false);
            _crossRoot=(RectTransform)obj.transform; Rect(_crossRoot,0,0,CrossSelectorLayout.Width,CrossSelectorLayout.Height);
            for(int g=0;g<4;g++)
            {
                var group=(AttachmentGroup)g; var panel=new CrossGroup(); _crossGroups[g]=panel;
                var holder=new GameObject(group.ToString(),typeof(RectTransform)); holder.layer=5; holder.transform.SetParent(obj.transform,false);
                panel.Root=(RectTransform)holder.transform;
                var heading=CrossSelectorLayout.Header(group); var caption=CrossSelectorLayout.Caption(group);
                panel.Header=Label(holder.transform,"Category",_groups[g].Label,heading.X,heading.Y,heading.Width,heading.Height,20,_crossPalette.Text);
                panel.Header.alignment=TextAnchor.MiddleCenter;
                panel.Caption=Label(holder.transform,"Name","",caption.X,caption.Y,caption.Width,caption.Height,14,_crossPalette.Text);
                panel.Caption.alignment=TextAnchor.UpperCenter;
                for(int i=0;i<5;i++)
                {
                    var r=CrossSelectorLayout.Cell(group,i); var cell=new CrossCell(); panel.Cells[i]=cell;
                    cell.Root=(RectTransform)Box(holder.transform,"Choice"+g+"/"+i,r.X,r.Y,r.Width,r.Height,_crossPalette.Normal).transform;
                    cell.Background=cell.Root.GetComponent<Image>(); cell.Edges=Outline(cell.Root,r.Width,r.Height,_crossPalette.Disabled);
                    cell.Icon=Box(cell.Root,"Icon",6,5,r.Width-12,i<3 ? 64 : 1,Color.white).GetComponent<Image>(); cell.Icon.preserveAspect=true;
                    cell.Name=Label(cell.Root,"Name","",4,i<3 ? 73 : 0,r.Width-8,i<3 ? 21 : 36,16,_crossPalette.Text);
                    cell.Name.alignment=TextAnchor.MiddleCenter;
                    cell.Tag=Label(cell.Root,"State","",4,95,r.Width-8,16,12,_crossPalette.Selected); cell.Tag.alignment=TextAnchor.MiddleCenter;
                    if(i>=3) { cell.Tag.gameObject.SetActive(false); cell.Icon.gameObject.SetActive(false); }
                }
            }
            foreach(var panel in _crossGroups)
            {
                foreach(Graphic graphic in panel.Root.GetComponentsInChildren<Graphic>(true)) graphic.gameObject.AddComponent<ProjectedCardMesh>();
                panel.Meshes=panel.Root.GetComponentsInChildren<ProjectedCardMesh>(true);
            }
            SetCrossGeometry(); obj.SetActive(false);
        }
        private float CrossWidth=>UseStudioCrossLayout ? StudioCrossLayout.Width : CrossSelectorLayout.Width;
        private float CrossHeight=>UseStudioCrossLayout ? StudioCrossLayout.Height : CrossSelectorLayout.Height;
        private SelectorRect CrossCellRect(AttachmentGroup group,int index)=>UseStudioCrossLayout ? StudioCrossLayout.Cell(index) : CrossSelectorLayout.Cell(group,index);
        private void SetCrossGeometry()
        {
            for(int g=0;g<4;g++)
            {
                var panel=_crossGroups[g]; panel.Projection=null; panel.Root.gameObject.SetActive(true);
                Rect(panel.Root,0,0,CrossWidth,CrossHeight);
                var heading=UseStudioCrossLayout ? StudioCrossLayout.Header : CrossSelectorLayout.Header((AttachmentGroup)g);
                Rect(panel.Header.rectTransform,heading.X,heading.Y,heading.Width,heading.Height);
                panel.Header.horizontalOverflow=UseStudioCrossLayout ? HorizontalWrapMode.Overflow : HorizontalWrapMode.Wrap;
                panel.Caption.gameObject.SetActive(panel.Visible && !UseStudioCrossLayout);
                for(int i=0;i<5;i++)
                {
                    // Hidden preview choices keep their ordinary geometry for restoring the legacy layout.
                    var r=UseStudioCrossLayout && (i==1 || i==2) ? CrossSelectorLayout.Cell((AttachmentGroup)g,i) : CrossCellRect((AttachmentGroup)g,i);
                    var cell=panel.Cells[i]; Rect(cell.Root,r.X,r.Y,r.Width,r.Height);
                    if(i<3)
                    {
                        Rect(cell.Icon.rectTransform,UseStudioCrossLayout ? 22 : 6,UseStudioCrossLayout ? 12 : 5,UseStudioCrossLayout ? 100 : r.Width-12,UseStudioCrossLayout ? 55 : 64);
                        Rect(cell.Name.rectTransform,4,UseStudioCrossLayout ? 69 : 73,r.Width-8,21);
                        Rect(cell.Tag.rectTransform,4,UseStudioCrossLayout ? 88 : 95,r.Width-8,16);
                    }
                }
            }
        }
        private void RenderCross(SelectionState state,bool icons)
        {
            if(!ExperimentalCrossSelectors) return;
            for(int g=0;g<4;g++)
            {
                var group=(AttachmentGroup)g; var panel=_crossGroups[g]; var vm=state.Current(group);
                panel.Visible=vm!=null; panel.Header.gameObject.SetActive(panel.Visible); panel.Caption.gameObject.SetActive(panel.Visible && !UseStudioCrossLayout);
                panel.HeaderTarget=null;
                int slots=state.GroupSlots(group).Count;
                panel.Header.fontSize=13;
                panel.Header.text=_groups[g].Label+(slots>1 ? " ["+(state.SlotIndex(group)+1)+"/"+slots+"]" : "");
                panel.Caption.text=vm?.Slot.Installed?.Name ?? "Empty slot";
                SetCross(panel.Cells[0],vm?.Slot.Installed,vm?.Slot.Installed==null ? "EMPTY" : "INSTALLED",
                    vm==null ? null : new PointerTarget {Kind=PointerKind.Candidates,Group=group},vm?.Slot.Installed!=null,icons);
                int first=state.CandidateIndex(group)/2*2;
                for(int i=1;i<3;i++)
                {
                    var candidate=vm!=null && first+i-1<vm.Candidates.Count ? vm.Candidates[first+i-1] : null;
                    var target=candidate!=null && ActionDenial(vm!,candidate.Item,false,candidate.Evidence).Length==0 ?
                        new PointerTarget {Kind=PointerKind.ChooseItem,Action=AttachmentRequest.ChoiceAction(vm!.Slot),Group=group,Path=vm.Slot.Path,Item=candidate.Item.Id} : null;
                    SetCross(panel.Cells[i],candidate?.Item,candidate==null ? "" : target==null ? "LOCKED" : target.Action.ToString().ToUpperInvariant(),target,false,icons);
                    panel.Cells[i].Root.gameObject.SetActive(candidate!=null && !UseStudioCrossLayout);
                }
                var installed=vm?.Slot.Installed;
                SetCross(panel.Cells[3],null,"REMOVE",installed!=null && ActionDenial(vm!,installed,true).Length==0 ?
                    new PointerTarget {Kind=PointerKind.Uninstall,Group=group,Path=vm!.Slot.Path,Item=installed.Id} : null,false,false);
                SetCross(panel.Cells[4],null,"LIST",vm==null ? null : new PointerTarget {Kind=PointerKind.Candidates,Group=group},false,false);
                for(int i=0;i<5;i++)
                {
                    if(!panel.Visible) panel.Cells[i].Root.gameObject.SetActive(false);
                    if(panel.Cells[i].Target!=null) panel.Cells[i].Target!.Surface="cross"+g+"/"+i;
                }
            }
        }
        private void SetCross(CrossCell cell,ItemObservation? item,string tag,PointerTarget? target,bool installed,bool icons)
        {
            cell.Root.gameObject.SetActive(true); cell.Item=item; cell.Target=target; cell.Installed=installed;
            cell.Name.fontSize=tag=="ATTACHMENTS" ? 12 : 16;
            cell.Name.text=item?.Name ?? tag; cell.Tag.text=item==null ? "" : tag;
            cell.Icon.sprite=null; cell.Icon.enabled=false;
            StyleCross(cell,false); if(icons) UpdateCrossIcon(cell);
        }
        private void StyleCross(CrossCell cell,bool hover)
        {
            cell.Background.color=hover ? _crossPalette.Selected : cell.Installed ? _crossPalette.Disabled : _crossPalette.Normal;
            foreach(var edge in cell.Edges) edge.color=hover || cell.Installed ? _crossPalette.Text : _crossPalette.Selected;
            cell.Name.color=cell.Tag.color=cell.Target!=null || cell.Installed ? _crossPalette.Text : _crossPalette.Selected;
            cell.Icon.color=cell.Target!=null || cell.Installed ? _crossPalette.Text : _crossPalette.Selected;
        }
        private void UpdateCrossIcon(CrossCell cell)
        {
            if(cell.Item==null || cell.Icon.enabled) return;
            var sprite=_resources.Icon(cell.Item.NativeItem,cell.Item.Id);
            if(sprite!=null) { cell.Icon.sprite=sprite; cell.Icon.enabled=true; }
        }
        private void PollCrossIcons()
        { if(ExperimentalCrossSelectors && _pointerIcons) foreach(var group in _crossGroups) foreach(var cell in group.Cells) UpdateCrossIcon(cell); }
        private void UpdateCrossLayout(float width,float height,bool current)
        {
            if (CrossPresentationHidden) { HideCrossPresentation(); return; }
            if(UseStudioCrossLayout) { UpdateStudioCrossLayout(width,height,current); return; }
            Vector2 weaponPoint=new Vector2(width*.60f,height*.60f); PanelProjection? plane=null;
            bool following=FollowWeapon && current && _anchors.ProjectCardPlane(width,height,PerspectiveStrength,ClockwiseDegrees,out plane,out weaponPoint,CameraFacingCards);
            following=following && CrossSelectorLayout.TryPlace(plane!,width,height,weaponPoint.x,weaponPoint.y,out _crossProjection,CameraFacingCards);
            if(!following) CrossSelectorLayout.TryFlat(width,height,out _crossProjection);
            _layoutReason=following ? "Cross live weapon anchor" : "Cross flat fallback";
            FollowCrossPanel(0,width,height,current,ref _crossProjection);
            _crossRoot.gameObject.SetActive(_crossProjection!=null);
            foreach(var panel in _crossGroups)
            {
                panel.Projection=_crossProjection;
                foreach(var mesh in panel.Meshes) mesh.SetProjection(_crossProjection,panel.Root,_canvasRect,CrossSelectorLayout.Width,CrossSelectorLayout.Height);
            }
            _lastFollowing=following; _lastProjected=0;
            for(int g=0;g<4;g++)
            {
                var group=_groups[g]; var r=CrossSelectorLayout.Group(group.Group);
                group.Projection=null;
                if(_crossProjection!=null) PanelProjection.TryCreate(_crossProjection.Map(r.X/CrossSelectorLayout.Width,r.Y/CrossSelectorLayout.Height),
                    _crossProjection.Map((r.X+r.Width)/CrossSelectorLayout.Width,r.Y/CrossSelectorLayout.Height),
                    _crossProjection.Map(r.X/CrossSelectorLayout.Width,(r.Y+r.Height)/CrossSelectorLayout.Height),out group.Projection);
                Vector2 end=default;
                bool valid=_crossProjection!=null && _crossGroups[g].Visible && current && _anchors.Project(PreviewSlot(group.Group,group.Slot),width,height,out end);
                group.Leader.Show(valid); group.Leader.Second.enabled=false;
                if(!valid) continue;
                var cell=CrossSelectorLayout.Cell(group.Group,0);
                var start=_crossProjection!.Map((cell.X+cell.Width/2)/CrossSelectorLayout.Width,(cell.Y+cell.Height/2)/CrossSelectorLayout.Height);
                Line(group.Leader.First,new Vector2(start.X,start.Y),end); Rect(group.Leader.Dot.rectTransform,end.x-2,end.y-2,4,4); _lastProjected++;
            }
            PlacePointerUi();
        }
        private PointerTarget? HitCross(float x,float y)
        {
            if (CrossPresentationHidden) return null;
            // Reverse paint order; inverse-map the same per-group surface used by every graphic.
            for(int g=3;g>=0;g--)
            {
                var panel=_crossGroups[g]; if(!panel.Visible) continue; var group=(AttachmentGroup)g;
                if(panel.Projection==null || !panel.Projection.TryUnmap(x,y,out float u,out float v)) continue;
                float px=u*CrossWidth,py=v*CrossHeight;
                if(UseStudioCrossLayout)
                {
                    int target=StudioCrossLayout.Hit(px,py);
                    if(target==5) return panel.HeaderTarget;
                    if(target>=0 && panel.Cells[target].Root.gameObject.activeSelf) return panel.Cells[target].Target;
                }
                else
                {
                    if(CrossSelectorLayout.Header(group).Contains(px,py)) return panel.HeaderTarget;
                    for(int i=0;i<5;i++) if(panel.Cells[i].Root.gameObject.activeSelf && CrossCellRect(group,i).Contains(px,py)) return panel.Cells[i].Target;
                }
            }
            return null;
        }
        private void HoverCross(PointerTarget? target)
        {
            if(!ExperimentalCrossSelectors) return;
            for(int g=0;g<4;g++)
            {
                var group=_crossGroups[g]; group.Caption.text=_groups[g].Slot?.Installed?.Name ?? "Empty slot";
                foreach(var cell in group.Cells)
                {
                    bool hover=cell.Target!=null && cell.Target.Key==target?.Key; StyleCross(cell,hover);
                    if(hover && cell.Target!.Kind==PointerKind.Uninstall) group.Caption.text="REMOVE "+_groups[g].Slot?.Installed?.Name;
                    if(hover && cell.Target!.Kind==PointerKind.ChooseItem) group.Caption.text=cell.Target.Action.ToString().ToUpperInvariant()+" "+cell.Item?.Name;
                }
            }
        }
        private void ClearCrossItems()
        {
            _crossProjection=null;
            foreach(var group in _crossGroups) { group.Projection=null; group.HeaderTarget=null; foreach(var cell in group.Cells) { cell.Item=null; cell.Target=null; cell.Icon.sprite=null; cell.Icon.enabled=false; } }
        }
        private void ApplyCrossPalette()
        {
            bool cross=ExperimentalCrossSelectors;
            _weapon.transform.parent.GetComponent<Image>().color=cross ? _crossPalette.Normal : _panel;
            _weapon.color=cross ? _crossPalette.Selected : _muted; _actionStatus.color=cross ? _crossPalette.Text : _accent;
            _popup.GetComponent<Image>().color=cross ? _crossPalette.Normal : new Color(.035f,.043f,.044f,.98f);
            foreach(var edge in _popupEdges) edge.color=cross ? _crossPalette.Selected : _accent;
            foreach(var row in _pointerRows) row.Detail.color=cross ? _crossPalette.Selected : _muted;
            _popupPageLabel.color=cross ? _crossPalette.Selected : _muted;
            foreach(var panel in _groups)
            { panel.Leader.First.color=panel.Leader.Second.color=cross ? _crossPalette.Highlight : _muted; panel.Leader.Dot.color=cross ? _crossPalette.Text : _accent; }
        }
        private void AppendCrossReport(StringBuilder report)
        {
            if(!ExperimentalCrossSelectors) return;
            report.AppendLine("Cross visibility: " + (CrossPresentationHidden ? "hidden: " + _crossHiddenReason : "shown") +
                "; held=" + PoseHeld + "; bound panels=" + Array.FindAll(_crossAttachments,p=>p!=null).Length);
            report.AppendLine("Cross palette: "+_crossPalette.Source+"; normal="+_crossPalette.Normal+" disabled="+_crossPalette.Disabled+" selected="+_crossPalette.Selected+" highlight="+_crossPalette.Highlight);
            report.AppendLine("Cross geometry: "+(UseStudioCrossLayout ? "Studio export "+StudioCrossLayout.Export+"; per-group 216x187; reference=1902x992; perspective=1400; XYZ=17/-39/30; scale/opacity=1; candidates=LIST; aspect-fit + shared bounded native sway" : "1264x1000 shared plane; optic above, muzzle left, tactical right, foregrip below."));
            for(int g=0;g<4;g++) for(int i=0;i<5;i++)
            {
                var cell=_crossGroups[g].Cells[i];
                report.AppendLine("cross "+(AttachmentGroup)g+"/"+i+" visible="+cell.Root.gameObject.activeInHierarchy+" item="+cell.Item?.Id+" target="+cell.Target?.Key);
                var projection=_crossGroups[g].Projection;
                if(projection==null || !cell.Root.gameObject.activeInHierarchy) continue;
                var r=CrossCellRect((AttachmentGroup)g,i);
                var a=projection.Map(r.X/CrossWidth,r.Y/CrossHeight);
                var b=projection.Map((r.X+r.Width)/CrossWidth,r.Y/CrossHeight);
                var c=projection.Map(r.X/CrossWidth,(r.Y+r.Height)/CrossHeight);
                report.AppendLine("  displayed edges="+Vector2.Distance(new Vector2(a.X,a.Y),new Vector2(b.X,b.Y)).ToString("F1")+"x"+
                    Vector2.Distance(new Vector2(a.X,a.Y),new Vector2(c.X,c.Y)).ToString("F1")+" canvas pixels; same projected rectangle used for hit testing");
            }
        }
    }
}
