using System;
using System.Collections.Generic;
using System.Globalization;
using Tylevo.FieldAttachments.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Tylevo.FieldAttachments.Runtime
{
    public sealed partial class AttachmentOverlay
    {
        public Func<RaidSnapshot,SlotObservation,ItemObservation?,AttachmentComparison>? ComparisonProvider { get; set; }
        private sealed class ComparisonRow
        {
            public Text Name=null!, Values=null!, Change=null!;
            public Image Track=null!, Baseline=null!, Delta=null!, Marker=null!;
        }
        private RectTransform _comparisonPanel=null!;
        private RectTransform _comparisonSummary=null!;
        private Text _comparisonTitle=null!, _comparisonStatus=null!;
        private readonly ComparisonRow[] _comparisonRows=new ComparisonRow[4];
        private readonly ComparisonRow[] _comparisonSummaryRows=new ComparisonRow[2];
        private readonly List<SelectorRect> _comparisonSummaryObstacles=new List<SelectorRect>();
        private AttachmentGroup? _comparisonGroup;
        private bool _comparisonSummaryAvailable;
        private float _comparisonLastHover;
        private const float ComparisonHoverGrace=.25f, SummaryWidth=224, SummaryHeight=48, SummaryTrackWidth=90;
        private RaidSnapshot? _comparisonSnapshot;
        private Func<RaidSnapshot,SlotObservation,ItemObservation?,AttachmentComparison>? _comparisonRead;
        private string _comparisonIdentity="";
        private int _comparisonGeneration=-1;
        private PointerRow? _comparisonPointerRow;
        private const float ComparisonWidth=320, ComparisonHeight=258, ComparisonTrackWidth=218;
        private readonly Color _comparisonBetter=new Color(.35f,.82f,.49f,1), _comparisonWorse=new Color(.94f,.40f,.36f,1);

        private void BuildComparisonUi()
        {
            _comparisonPanel=(RectTransform)Box(_root.transform,"AttachmentComparison",24,124,ComparisonWidth,ComparisonHeight,
                new Color(.035f,.043f,.044f,.97f)).transform;
            Outline(_comparisonPanel,ComparisonWidth,ComparisonHeight,new Color(.50f,.54f,.50f,.7f));
            Label(_comparisonPanel,"Heading","WEAPON STATS",12,9,296,18,13,_accent);
            _comparisonTitle=Label(_comparisonPanel,"Attachment","",12,30,296,32,13,Color.white);
            _comparisonTitle.resizeTextForBestFit=true; _comparisonTitle.resizeTextMinSize=10; _comparisonTitle.resizeTextMaxSize=13;
            _comparisonStatus=Label(_comparisonPanel,"ComparisonStatus","",12,64,296,30,11,_muted);
            string[] names={"Ergonomics","Vertical recoil","Horizontal recoil","Weight"};
            for(int i=0;i<_comparisonRows.Length;i++)
            {
                float y=100+i*39;
                var row=new ComparisonRow(); _comparisonRows[i]=row;
                row.Name=Label(_comparisonPanel,"StatName"+i,names[i],12,y,118,17,12,Color.white);
                row.Values=Label(_comparisonPanel,"StatValues"+i,"",132,y,176,17,12,Color.white);
                row.Values.alignment=TextAnchor.MiddleRight;
                row.Change=Label(_comparisonPanel,"StatChange"+i,"",236,y+18,72,15,11,_muted);
                row.Change.alignment=TextAnchor.MiddleRight;
                row.Track=Box(_comparisonPanel,"StatTrack"+i,12,y+23,ComparisonTrackWidth,6,new Color(.13f,.16f,.16f,1)).GetComponent<Image>();
                row.Baseline=Box(row.Track.transform,"Current",0,0,0,6,new Color(.61f,.65f,.62f,1)).GetComponent<Image>();
                row.Delta=Box(row.Track.transform,"Delta",0,0,0,6,_comparisonBetter).GetComponent<Image>();
                row.Marker=Box(row.Track.transform,"CurrentMarker",0,-2,2,10,Color.white).GetComponent<Image>();
            }
            _comparisonSummary=(RectTransform)Box(_root.transform,"AttachmentComparisonSummary",0,0,SummaryWidth,SummaryHeight,
                new Color(.035f,.043f,.044f,.97f)).transform;
            Outline(_comparisonSummary,SummaryWidth,SummaryHeight,new Color(.50f,.54f,.50f,.7f));
            for(int i=0;i<_comparisonSummaryRows.Length;i++)
            {
                float y=4+i*22;
                var row=new ComparisonRow(); _comparisonSummaryRows[i]=row;
                row.Name=Label(_comparisonSummary,"SummaryName"+i,i==0 ? "Ergo" : "V. recoil",8,y,54,18,11,Color.white);
                row.Change=Label(_comparisonSummary,"SummaryChange"+i,"",160,y,56,18,11,_muted);
                row.Change.alignment=TextAnchor.MiddleRight;
                row.Track=Box(_comparisonSummary,"SummaryTrack"+i,65,y+7,SummaryTrackWidth,6,new Color(.13f,.16f,.16f,1)).GetComponent<Image>();
                row.Baseline=Box(row.Track.transform,"Current",0,0,0,6,new Color(.61f,.65f,.62f,1)).GetComponent<Image>();
                row.Delta=Box(row.Track.transform,"Delta",0,0,0,6,_comparisonBetter).GetComponent<Image>();
                row.Marker=Box(row.Track.transform,"CurrentMarker",0,-2,2,10,Color.white).GetComponent<Image>();
            }
            HideComparison();
        }

        private void HideComparison()
        {
            if(_comparisonPanel!=null) _comparisonPanel.gameObject.SetActive(false);
            if(_comparisonSummary!=null) _comparisonSummary.gameObject.SetActive(false);
            _comparisonGroup=null; _comparisonSummaryAvailable=false; _comparisonLastHover=0;
            _comparisonSnapshot=null; _comparisonRead=null; _comparisonIdentity=""; _comparisonGeneration=-1; _comparisonPointerRow=null;
        }

        private void PlaceComparisonUi(float width,float height)
        {
            if(_comparisonPanel==null || !_comparisonPanel.gameObject.activeSelf) return;
            // This independent readout never feeds category placement or popup hit geometry.
            float scale=Mathf.Min(1,Mathf.Min((width-32)/ComparisonWidth,(height-148)/ComparisonHeight));
            _comparisonPanel.localScale=Vector3.one*Mathf.Max(.1f,scale);
            Rect(_comparisonPanel,Mathf.Max(16,width-24-ComparisonWidth*_comparisonPanel.localScale.x),24,ComparisonWidth,ComparisonHeight);
            PlaceComparisonSummary(width,height);
            _comparisonPanel.SetAsLastSibling();
            if(_pointerVisual!=null) _pointerVisual.SetAsLastSibling();
        }

        private bool InsideComparison(float x,float y)
        {
            return InsideComparisonRect(_comparisonPanel,x,y) || InsideComparisonRect(_comparisonSummary,x,y);
        }

        private static bool InsideComparisonRect(RectTransform panel,float x,float y)
        {
            if(panel==null || !panel.gameObject.activeInHierarchy) return false;
            float px=x-panel.anchoredPosition.x,py=y+panel.anchoredPosition.y;
            return px>=0 && py>=0 && px<panel.rect.width*panel.localScale.x && py<panel.rect.height*panel.localScale.y;
        }

        private void PlaceComparisonSummary(float width,float height)
        {
            _comparisonSummary.gameObject.SetActive(false);
            if(!_comparisonSummaryAvailable || !_comparisonGroup.HasValue) return;
            var source=PanelForGroup(_comparisonGroup.Value)?.Projection;
            if(source==null) return;
            var anchor=_popupKind!=null ? ComparisonRect(_popup) : new SelectorRect(source.Left,source.Top,source.Right-source.Left,source.Bottom-source.Top);
            _comparisonSummaryObstacles.Clear();
            _comparisonSummaryObstacles.Add(new SelectorRect(20,20,548,92));
            _comparisonSummaryObstacles.Add(ComparisonRect(_comparisonPanel));
            if(_popupKind!=null) _comparisonSummaryObstacles.Add(ComparisonRect(_popup));
            if(_menuControlPlate!=null && _menuControlPlate.gameObject.activeSelf) _comparisonSummaryObstacles.Add(ComparisonRect(_menuControlPlate));
            if(_layerControl!=null && _layerControl.gameObject.activeSelf) _comparisonSummaryObstacles.Add(ComparisonRect(_layerControl));
            if(_layerStatusPlate!=null && _layerStatusPlate.gameObject.activeSelf) _comparisonSummaryObstacles.Add(ComparisonRect(_layerStatusPlate));
            foreach(var group in _groups)
            {
                var plane=group.Projection;
                if(group.Slot!=null && plane!=null)
                    _comparisonSummaryObstacles.Add(new SelectorRect(plane.Left,plane.Top,plane.Right-plane.Left,plane.Bottom-plane.Top));
            }
            if(!ComparisonSummaryLayout.TryPlace(anchor,SummaryWidth,SummaryHeight,width,height,_comparisonSummaryObstacles,out var bounds)) return;
            Rect(_comparisonSummary,bounds.X,bounds.Y,SummaryWidth,SummaryHeight);
            _comparisonSummary.gameObject.SetActive(true); _comparisonSummary.SetAsLastSibling();
        }

        private static SelectorRect ComparisonRect(RectTransform panel)
        {
            return new SelectorRect(panel.anchoredPosition.x,-panel.anchoredPosition.y,
                panel.rect.width*panel.localScale.x,panel.rect.height*panel.localScale.y);
        }

        private void UpdateComparisonHover()
        {
            float x=_pointerPosition.x*_layoutWidth,y=_pointerPosition.y*_layoutHeight;
            // Keep the readout visible while reading it and consume its surface without a click target.
            if(InsideComparison(x,y)) { _comparisonLastHover=Time.unscaledTime; return; }
            var previousRow=_comparisonPointerRow;
            _comparisonPointerRow=null;
            if(_pointerState==null || InsideMenuControls(x,y) || !TryComparisonHover(x,y,out SlotObservation? slot,out ItemObservation? item))
            {
                if(_comparisonPanel.gameObject.activeSelf && Time.unscaledTime-_comparisonLastHover<=ComparisonHoverGrace)
                { _comparisonPointerRow=previousRow; return; }
                HideComparison(); return;
            }
            _comparisonLastHover=Time.unscaledTime; _comparisonGroup=slot!.Group;
            var snapshot=_pointerState.Snapshot;
            string identity=slot!.Group+"|"+slot.Path+"|"+(item?.Id ?? "<remove>")+"|"+(slot.Installed?.Id ?? "<empty>");
            if(ReferenceEquals(_comparisonSnapshot,snapshot) && _comparisonGeneration==_pointerGeneration &&
                _comparisonIdentity==identity && ReferenceEquals(_comparisonRead,ComparisonProvider)) return;
            _comparisonSnapshot=snapshot; _comparisonGeneration=_pointerGeneration; _comparisonIdentity=identity; _comparisonRead=ComparisonProvider;
            string label=item?.Name ?? "REMOVE "+slot.Installed?.Name;
            AttachmentComparison comparison;
            try
            {
                comparison=ComparisonProvider?.Invoke(snapshot,slot,item) ?? new AttachmentComparison(new WeaponComparisonValues(),
                    new WeaponComparisonValues(),label,"Comparison unavailable.");
            }
            catch
            {
                comparison=new AttachmentComparison(new WeaponComparisonValues(),new WeaponComparisonValues(),label,"Comparison unavailable.");
            }
            _comparisonTitle.text=comparison.Label.Length!=0 ? comparison.Label : label;
            _comparisonStatus.text=comparison.UnavailableReason.Length!=0 ? comparison.UnavailableReason : "Current \u2192 preview. Green better / red worse.";
            RenderComparisonRow(0,comparison.Current.Ergonomics,comparison.Preview.Ergonomics,100,true,false);
            RenderComparisonRow(1,comparison.Current.VerticalRecoil,comparison.Preview.VerticalRecoil,
                Mathf.Max(100,comparison.Current.VerticalRecoil.GetValueOrDefault()*1.25f),false,false);
            RenderComparisonRow(2,comparison.Current.HorizontalRecoil,comparison.Preview.HorizontalRecoil,
                Mathf.Max(200,comparison.Current.HorizontalRecoil.GetValueOrDefault()*1.25f),false,false);
            RenderComparisonRow(3,comparison.Current.Weight,comparison.Preview.Weight,10,false,true);
            _comparisonSummaryAvailable=(comparison.Current.Ergonomics.HasValue && comparison.Preview.Ergonomics.HasValue) ||
                (comparison.Current.VerticalRecoil.HasValue && comparison.Preview.VerticalRecoil.HasValue);
            _comparisonPanel.gameObject.SetActive(true); PlaceComparisonUi(_layoutWidth,_layoutHeight);
        }

        private void RenderComparisonRow(int index,float? current,float? preview,float scale,bool higherIsBetter,bool weight)
        {
            bool recoil=index==1 || index==2;
            RenderComparisonBar(_comparisonRows[index],current,preview,scale,higherIsBetter,weight,recoil,ComparisonTrackWidth);
            if(index<_comparisonSummaryRows.Length)
                RenderComparisonBar(_comparisonSummaryRows[index],current,preview,scale,higherIsBetter,weight,recoil,SummaryTrackWidth);
        }

        private void RenderComparisonBar(ComparisonRow row,float? current,float? preview,float scale,bool higherIsBetter,bool weight,bool recoil,float trackWidth)
        {
            var bar=new AttachmentComparisonBar(current,preview,scale,higherIsBetter);
            float? displayCurrent=recoil && current.HasValue ? Mathf.RoundToInt(current.Value) : current;
            float? displayPreview=recoil && preview.HasValue ? Mathf.RoundToInt(preview.Value) : preview;
            float? displayDelta=displayCurrent.HasValue && displayPreview.HasValue ? displayPreview.Value-displayCurrent.Value : (float?)null;
            string format=weight ? "0.###" : recoil ? "0" : "0.##";
            string Value(float? value)=>value.HasValue ? value.Value.ToString(format,CultureInfo.InvariantCulture) : "\u2014";
            if(row.Values!=null) row.Values.text=Value(displayCurrent)+" \u2192 "+Value(displayPreview)+(weight ? " kg" : "");
            row.Change.text=displayDelta.HasValue ? (displayDelta.Value>0 ? "+" : "")+displayDelta.Value.ToString(format,CultureInfo.InvariantCulture) : "\u2014";
            int benefit=recoil && displayDelta==0 ? 0 : bar.Benefit;
            Color change=benefit>0 ? _comparisonBetter : benefit<0 ? _comparisonWorse : _muted;
            row.Change.color=row.Delta.color=change;
            Rect(row.Baseline.rectTransform,0,0,trackWidth*bar.CurrentFill,6);
            Rect(row.Delta.rectTransform,trackWidth*bar.DeltaStart,0,trackWidth*bar.DeltaWidth,6);
            Rect(row.Marker.rectTransform,Mathf.Clamp(trackWidth*bar.CurrentFill-1,0,trackWidth-2),-2,2,10);
            row.Marker.enabled=bar.HasCurrent; row.Delta.enabled=benefit!=0;
        }

        private bool TryComparisonHover(float x,float y,out SlotObservation? slot,out ItemObservation? item)
        {
            slot=null; item=null;
            if(_pointerState==null) return false;
            if(_popupKind!=null && InsidePopup(x,y))
            {
                int index=QuickSwapMenu.Row(x-_popup.anchoredPosition.x,y+_popup.anchoredPosition.y);
                if(index<0 || !_pointerRows[index].Root.gameObject.activeSelf) return false;
                var row=_pointerRows[index]; bool remove=_popupPage*6+index==0;
                bool found=ResolveComparison(_popupGroup,row.Item,remove,out slot,out item);
                if(found) _comparisonPointerRow=row;
                return found;
            }
            if(ExperimentalCrossSelectors)
            {
                for(int g=3;g>=0;g--)
                {
                    var panel=_crossGroups[g];
                    if(!panel.Visible || panel.Projection==null || !panel.Projection.TryUnmap(x,y,out float u,out float v)) continue;
                    bool header=UseStudioCrossLayout ? StudioCrossLayout.Header.Contains(u*CrossWidth,v*CrossHeight) :
                        CrossSelectorLayout.Header((AttachmentGroup)g).Contains(u*CrossWidth,v*CrossHeight);
                    if(header) return false;
                    for(int i=0;i<5;i++)
                        if(panel.Cells[i].Root.gameObject.activeSelf && CrossCellRect((AttachmentGroup)g,i).Contains(u*CrossWidth,v*CrossHeight))
                            return i>=1 && i<=3 && ResolveComparison(_groups[g].Group,panel.Cells[i].Item,i==3,out slot,out item);
                }
                return false;
            }
            if(ExperimentalHexSelectors)
            {
                for(int g=0;g<4;g++)
                {
                    var panel=_hexPanels[g]; if(!panel.Root.gameObject.activeSelf) continue;
                    var projection=_groups[g].Projection; float u,v;
                    if(projection!=null) { if(!projection.TryUnmap(x,y,out u,out v)) continue; }
                    else { u=(x-panel.Root.anchoredPosition.x)/HexSelectorLayout.Width; v=(y+panel.Root.anchoredPosition.y)/HexSelectorLayout.Height; }
                    for(int i=2;i<=5;i++)
                        if(panel.Cells[i].Shape.gameObject.activeSelf && HexSelectorLayout.Contains(i,u*HexSelectorLayout.Width,v*HexSelectorLayout.Height))
                            return ResolveComparison(_groups[g].Group,panel.Cells[i].Item,i==5,out slot,out item);
                }
                return false;
            }
            foreach(var panel in _groups)
            {
                float u,v;
                if(panel.Projection!=null) { if(!panel.Projection.TryUnmap(x,y,out u,out v)) continue; }
                else { u=(x-panel.Root.anchoredPosition.x)/OverlayLayout.PanelWidth; v=(y+panel.Root.anchoredPosition.y)/OverlayLayout.PanelHeight; }
                float px=u*OverlayLayout.PanelWidth,py=v*OverlayLayout.PanelHeight;
                if(py<61 || py>161) continue;
                for(int i=0;i<3;i++)
                    if(px>=116+i*104 && px<=212+i*104 && panel.Candidates[i].Background.gameObject.activeSelf)
                        return ResolveComparison(panel.Group,panel.Candidates[i].Item,false,out slot,out item);
            }
            return false;
        }

        private bool ResolveComparison(AttachmentGroup group,ItemObservation? displayed,bool remove,out SlotObservation? slot,out ItemObservation? item)
        {
            slot=null; item=null;
            var vm=_pointerState?.Current(group); if(vm==null) return false;
            if(_pointerState!.GroupSlots(group).FindAll(s=>s.Slot.Path==vm.Slot.Path).Count!=1) return false;
            if(remove) { if(vm.Slot.Installed==null) return false; slot=vm.Slot; return true; }
            if(displayed==null || displayed.Id.Length==0) return false;
            var candidates=vm.Candidates.FindAll(c=>c.Item.Id==displayed.Id);
            if(candidates.Count!=1) return false;
            slot=vm.Slot; item=candidates[0].Item; return true;
        }
    }
}
