using System;
using System.Text;
using Tylevo.FieldAttachments.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Tylevo.FieldAttachments.Runtime
{
    public sealed partial class AttachmentOverlay
    {
        private sealed class HexCell
        {
            public HexGraphic Shape=null!; public Image Icon=null!; public Text Glyph=null!, Tag=null!;
            public ItemObservation? Item; public PointerTarget? Target;
            public Color Edge; public bool Slash;
        }
        private sealed class HexPanel
        {
            public RectTransform Root=null!; public Text Title=null!, Caption=null!;
            public ProjectedCardMesh[] Meshes=Array.Empty<ProjectedCardMesh>();
            public readonly HexCell[] Cells=new HexCell[7];
        }
        private readonly HexPanel[] _hexPanels=new HexPanel[4];
        private readonly Color _hexMint=new Color(.58f,.85f,.73f,1), _hexAmber=new Color(1,.67f,.18f,1);
        private bool _experimentalHexSelectors;
        public bool ExperimentalHexSelectors
        {
            get => _experimentalHexSelectors;
            set
            {
                if (_experimentalHexSelectors==value) return;
                _experimentalHexSelectors=value; ResetPointer(); _sampleFrame=-1;
                ApplySelectorVisibility();
                // Switching presentations invalidates any press captured in the old geometry.
                if (_pointerState!=null) RenderHex(_pointerState,_pointerIcons);
            }
        }
        private void BuildHexUi()
        {
            for(int g=0;g<4;g++)
            {
                var obj=new GameObject("Hex"+_groups[g].Group,typeof(RectTransform)); obj.layer=5;
                obj.transform.SetParent(_root.transform,false);
                var panel=new HexPanel {Root=(RectTransform)obj.transform}; _hexPanels[g]=panel;
                Rect(panel.Root,0,0,HexSelectorLayout.Width,HexSelectorLayout.Height);
                for(int i=0;i<7;i++)
                {
                    var center=HexSelectorLayout.Center(i);
                    var cellObject=new GameObject("HexChoice"+i,typeof(RectTransform),typeof(HexGraphic)); cellObject.layer=5;
                    cellObject.transform.SetParent(obj.transform,false);
                    Rect((RectTransform)cellObject.transform,center.x-HexSelectorLayout.Radius,center.y-HexSelectorLayout.HexHeight/2,
                        HexSelectorLayout.Radius*2,HexSelectorLayout.HexHeight);
                    var cell=new HexCell {Shape=cellObject.GetComponent<HexGraphic>()}; panel.Cells[i]=cell;
                    cell.Shape.raycastTarget=false;
                    cell.Icon=Box(cellObject.transform,"Icon",10,12,52,32,Color.white).GetComponent<Image>(); cell.Icon.preserveAspect=true;
                    cell.Glyph=Label(cellObject.transform,"Glyph","",8,12,56,36,i==0 ? 12 : 20,_hexMint); cell.Glyph.alignment=TextAnchor.MiddleCenter;
                    cell.Tag=Label(cellObject.transform,"State","",18,45,36,12,9,_hexMint); cell.Tag.alignment=TextAnchor.MiddleCenter;
                }
                panel.Title=Label(obj.transform,"Category","",15,217,270,23,18,_hexMint); panel.Title.alignment=TextAnchor.MiddleCenter;
                panel.Caption=Label(obj.transform,"AttachmentName","",10,243,280,34,12,Color.white); panel.Caption.alignment=TextAnchor.UpperCenter;
                foreach(Graphic graphic in obj.GetComponentsInChildren<Graphic>(true)) graphic.gameObject.AddComponent<ProjectedCardMesh>();
                panel.Meshes=obj.GetComponentsInChildren<ProjectedCardMesh>(true);
                obj.SetActive(false);
            }
        }
        private void RenderHex(SelectionState state,bool icons)
        {
            if (!ExperimentalHexSelectors || ExperimentalCrossSelectors) return;
            for(int g=0;g<4;g++)
            {
                var panel=_hexPanels[g]; var group=(AttachmentGroup)g; var vm=state.Current(group);
                int count=state.GroupSlots(group).Count;
                panel.Root.gameObject.SetActive(vm!=null);
                panel.Title.text=_groups[g].Label+(count>1 ? " ["+(state.SlotIndex(group)+1)+"/"+count+"]" : "");
                panel.Caption.text=vm?.Slot.Installed?.Name ?? (vm==null ? "No slot" : "Empty slot");
                string shortName=group==AttachmentGroup.Optic ? "SCOPE" : group==AttachmentGroup.Muzzle ? "MUZZLE" : group==AttachmentGroup.Tactical ? "TAC" : "GRIP";
                panel.Cells[0].Target=null; panel.Cells[0].Shape.gameObject.SetActive(false);
                SetHex(panel.Cells[1],vm?.Slot.Installed,vm==null ? "-" : "EMPTY",vm?.Slot.Installed==null ? "" : "ON",
                    vm==null ? null : new PointerTarget {Kind=PointerKind.Candidates,Group=group},vm?.Slot.Installed!=null,false,icons);
                int first=state.CandidateIndex(group)/3*3;
                for(int i=0;i<3;i++)
                {
                    var candidate=vm!=null && first+i<vm.Candidates.Count ? vm.Candidates[first+i] : null;
                    PointerTarget? target=candidate!=null && ActionDenial(vm!,candidate.Item,false,candidate.Evidence).Length==0 ?
                        new PointerTarget {Kind=PointerKind.ChooseItem,Action=AttachmentRequest.ChoiceAction(vm!.Slot),Group=group,Path=vm.Slot.Path,Item=candidate.Item.Id} : null;
                    SetHex(panel.Cells[i+2],candidate?.Item,"-",candidate==null ? "" : target==null ? "LOCK" : "+",target,false,false,icons);
                    panel.Cells[i+2].Shape.gameObject.SetActive(candidate!=null);
                }
                var installed=vm?.Slot.Installed;
                var remove=installed!=null && ActionDenial(vm!,installed,true).Length==0 ?
                    new PointerTarget {Kind=PointerKind.Uninstall,Group=group,Path=vm!.Slot.Path,Item=installed.Id} : null;
                SetHex(panel.Cells[5],null,"","",remove,false,true,false);
                SetHex(panel.Cells[6],null,"...","",vm==null ? null : new PointerTarget {Kind=PointerKind.Candidates,Group=group},false,false,false);
                for(int i=0;i<7;i++) if(panel.Cells[i].Target!=null) panel.Cells[i].Target!.Surface="hex"+g+"/"+i;
            }
        }
        private void SetHex(HexCell cell,ItemObservation? item,string glyph,string tag,PointerTarget? target,bool installed,bool slash,bool icons)
        {
            cell.Shape.gameObject.SetActive(true);
            cell.Item=item; cell.Target=target; cell.Slash=slash;
            cell.Edge=installed ? _hexAmber : target!=null ? _hexMint : new Color(.34f,.40f,.37f,.85f);
            cell.Shape.Style(new Color(.015f,.035f,.03f,.9f),cell.Edge,slash);
            cell.Icon.sprite=null; cell.Icon.enabled=false; cell.Icon.color=target!=null || installed ? Color.white : new Color(.55f,.55f,.55f,.8f);
            cell.Glyph.text=item!=null ? item.Name : glyph; cell.Glyph.fontSize=item!=null ? 10 : glyph.Length>3 ? 12 : 20;
            cell.Glyph.enabled=!slash; cell.Glyph.color=cell.Edge; cell.Tag.text=tag; cell.Tag.color=cell.Edge;
            if (icons) UpdateHexIcon(cell);
        }
        private void UpdateHexIcon(HexCell cell)
        {
            if (cell.Item==null || cell.Icon.enabled) return;
            var icon=_resources.Icon(cell.Item.NativeItem,cell.Item.Id);
            if (icon!=null) { cell.Icon.sprite=icon; cell.Icon.enabled=true; cell.Glyph.enabled=false; }
        }
        private void PollHexIcons()
        { if (ExperimentalHexSelectors && !ExperimentalCrossSelectors && _pointerIcons) foreach(var panel in _hexPanels) foreach(var cell in panel.Cells) UpdateHexIcon(cell); }
        private bool LayoutHex(int index,GroupPanel group,float width,float height,bool current)
        {
            var panel=_hexPanels[index];
            var fallback=OverlayLayout.Panel(group.Group,width,height);
            Rect(panel.Root,group.Projection==null ? fallback.x : 0,group.Projection==null ? fallback.y : 0,HexSelectorLayout.Width,HexSelectorLayout.Height);
            foreach(var mesh in panel.Meshes) mesh.SetProjection(group.Projection,panel.Root,_canvasRect,HexSelectorLayout.Width,HexSelectorLayout.Height);
            Vector2 end=default;
            bool valid=current && _anchors.Project(PreviewSlot(group.Group,group.Slot),width,height,out end);
            group.Leader.Show(valid);
            if (!valid) return false;
            // Leaders begin at the ring, not at the now-hidden rectangular card edge.
            Vector2 start=default; float nearest=float.MaxValue;
            for(int i=1;i<7;i++)
            {
                if(!panel.Cells[i].Shape.gameObject.activeSelf) continue;
                var center=HexSelectorLayout.Center(i);
                PanelPoint p=group.Projection?.Map(center.x/HexSelectorLayout.Width,center.y/HexSelectorLayout.Height) ??
                    new PanelPoint(panel.Root.anchoredPosition.x+center.x,-panel.Root.anchoredPosition.y+center.y,1);
                var point=new Vector2(p.X,p.Y); float distance=(point-end).sqrMagnitude;
                if (distance<nearest) { nearest=distance; start=point; }
            }
            Line(group.Leader.First,start,end); group.Leader.Second.enabled=false;
            Rect(group.Leader.Dot.rectTransform,end.x-2,end.y-2,4,4);
            return true;
        }
        private PointerTarget? HitHex(float x,float y)
        {
            for(int g=0;g<4;g++)
            {
                var panel=_hexPanels[g]; if(!panel.Root.gameObject.activeSelf) continue; var projection=_groups[g].Projection;
                float u,v;
                if (projection!=null) { if(!projection.TryUnmap(x,y,out u,out v)) continue; }
                else { u=(x-panel.Root.anchoredPosition.x)/HexSelectorLayout.Width; v=(y+panel.Root.anchoredPosition.y)/HexSelectorLayout.Height; }
                for(int i=0;i<7;i++)
                    if(panel.Cells[i].Shape.gameObject.activeSelf && HexSelectorLayout.Contains(i,u*HexSelectorLayout.Width,v*HexSelectorLayout.Height)) return panel.Cells[i].Target;
            }
            return null;
        }
        private void HoverHex(PointerTarget? target)
        {
            if (!ExperimentalHexSelectors || ExperimentalCrossSelectors) return;
            for(int g=0;g<4;g++)
            {
                var panel=_hexPanels[g];
                panel.Caption.text=_groups[g].Slot?.Installed?.Name ?? (_groups[g].Slot==null ? "No slot" : "Empty slot");
                foreach(var cell in panel.Cells)
                {
                bool hover=cell.Target!=null && cell.Target.Key==target?.Key;
                cell.Shape.Style(hover ? new Color(.12f,.22f,.18f,.97f) : new Color(.015f,.035f,.03f,.9f),hover ? Color.white : cell.Edge,cell.Slash);
                if(hover) panel.Caption.text=cell.Target!.Kind==PointerKind.Uninstall ? "UNINSTALL "+_groups[g].Slot?.Installed?.Name :
                    cell.Target.Kind==PointerKind.ChooseItem ? cell.Target.Action.ToString().ToUpperInvariant()+" "+cell.Item?.Name : cell.Item?.Name ?? panel.Caption.text;
                }
            }
        }
        private void ClearHexItems()
        {
            foreach(var panel in _hexPanels) foreach(var cell in panel.Cells)
            { cell.Item=null; cell.Target=null; cell.Icon.sprite=null; cell.Icon.enabled=false; }
        }
        private void AppendHexReport(StringBuilder report)
        {
            if(!ExperimentalHexSelectors || ExperimentalCrossSelectors) return;
            report.AppendLine("Hex geometry: 7 cells per group; logical 300x280; radius=36; native action path unchanged.");
            for(int g=0;g<4;g++) for(int i=0;i<7;i++)
            {
                var cell=_hexPanels[g].Cells[i];
                report.AppendLine("hex "+(AttachmentGroup)g+"/"+i+" item="+cell.Item?.Id+" target="+cell.Target?.Key);
            }
        }
    }
}
