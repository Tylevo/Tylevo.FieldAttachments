using System;
using System.Collections.Generic;
using Tylevo.FieldAttachments.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Tylevo.FieldAttachments.Runtime
{
    public sealed partial class AttachmentOverlay
    {
        // These rows contain only text. They never hold an item or an action target.
        private sealed class UnavailableRow
        {
            public RectTransform Root = null!;
            public Text Name = null!, Source = null!, Reason = null!;
        }
        private const int UnavailablePageSize = 3;
        private const float UnavailableRowHeight = 126;
        private readonly UnavailableRow[] _unavailableRows = new UnavailableRow[UnavailablePageSize];
        private readonly List<UnavailableAttachment> _popupUnavailable = new List<UnavailableAttachment>();
        private bool _showUnavailable;
        private RectTransform _availabilityButton = null!;
        private Text _availabilityLabel = null!;
        private int PopupPages => Math.Max(1, (_popupCount + PopupPageSize - 1) / PopupPageSize);
        private int PopupPageSize => _showUnavailable ? UnavailablePageSize : QuickSwapMenu.PageSize;
        private SelectorRect AvailabilityButtonRect => new SelectorRect(8, _popupFooter + 36, PopupWidth - 16, 30);

        private void BuildUnavailableUi()
        {
            for(int i=0; i<_unavailableRows.Length; i++)
            {
                var row = new UnavailableRow();
                row.Root = (RectTransform)Box(_popup,"UnavailablePart",8,QuickSwapMenu.RowsTop+i*UnavailableRowHeight,
                    PopupWidth-16,UnavailableRowHeight-4,new Color(.08f,.09f,.087f,1)).transform;
                row.Name = Label(row.Root,"Name","",8,4,PopupWidth-32,48,14,Color.white);
                row.Source = Label(row.Root,"Source","",8,52,PopupWidth-32,16,11,_muted);
                row.Reason = Label(row.Root,"Reason","",8,70,PopupWidth-32,48,13,_crossPalette.Text);
                row.Name.supportRichText=false; row.Source.supportRichText=false; row.Reason.supportRichText=false;
                row.Root.gameObject.SetActive(false);
                _unavailableRows[i]=row;
            }
            _availabilityLabel=MenuButton("Availability","",AvailabilityButtonRect,out _);
            _availabilityButton=(RectTransform)_availabilityLabel.transform.parent;
            _availabilityButton.gameObject.SetActive(false);
        }

        private void FillUnavailablePopup()
        {
            _popupCount=_popupUnavailable.Count;
            _popupPage=Math.Max(0,Math.Min(_popupPage,PopupPages-1));
            _popupTitle.text="Unavailable parts";
            _popupPageLabel.text=_popupCount==0 ? "None" : (_popupPage+1)+" / "+PopupPages;
            foreach(var row in _pointerRows)
            {
                row.Root.gameObject.SetActive(false); row.Target=null; row.Item=null; row.Icon.sprite=null; row.Icon.enabled=false;
            }
            float top=QuickSwapMenu.RowsTop;
            for(int i=0; i<_unavailableRows.Length; i++)
            {
                var row=_unavailableRows[i]; int index=_popupPage*UnavailablePageSize+i;
                row.Root.gameObject.SetActive(index<_popupCount);
                var entry=index<_popupCount ? _popupUnavailable[index] : null;
                row.Name.text=entry?.DisplayName ?? "";
                row.Source.text=entry?.Source ?? "";
                row.Reason.text=entry?.Reason ?? "";
                if(entry==null) continue;
                // Leave a little room for fractional CanvasScaler/text rounding.
                float nameHeight=Mathf.Clamp(Mathf.Ceil(row.Name.preferredHeight)+4,20,52);
                float reasonHeight=Mathf.Clamp(Mathf.Ceil(row.Reason.preferredHeight)+4,20,52);
                float reasonTop=4+nameHeight+16+4;
                float height=reasonTop+reasonHeight+8;
                Rect(row.Root,8,top,PopupWidth-16,height);
                Rect(row.Name.rectTransform,8,4,PopupWidth-32,nameHeight);
                Rect(row.Source.rectTransform,8,4+nameHeight,PopupWidth-32,16);
                Rect(row.Reason.rectTransform,8,reasonTop,PopupWidth-32,reasonHeight);
                top+=height+4;
            }
            _popupFooter=Math.Max(QuickSwapMenu.RowsTop+48,top); _popupHeight=_popupFooter+38;
            LayoutPopupFooter();
            _popup.gameObject.SetActive(true); PlacePointerUi();
        }

        private void LayoutPopupFooter()
        {
            Rect(_previousButton,8,_popupFooter,32,30); Rect(_nextButton,184,_popupFooter,32,30);
            _previousButton.gameObject.SetActive(_popupPage>0); _nextButton.gameObject.SetActive(_popupPage+1<PopupPages);
            Rect(_popupPageLabel.rectTransform,44,_popupFooter,136,30);
            bool available=_showUnavailable || _popupUnavailable.Count>0;
            _availabilityButton.gameObject.SetActive(available);
            if(available)
            {
                var r=AvailabilityButtonRect;
                Rect(_availabilityButton,r.X,r.Y,r.Width,r.Height);
                _availabilityLabel.text=_showUnavailable ? "Back to parts" : "Unavailable ("+_popupUnavailable.Count+")";
                _popupHeight+=36;
            }
            // Drawing and input use the same expanded popup bounds.
            Rect(_popupEdges[0].rectTransform,0,0,PopupWidth,1); Rect(_popupEdges[1].rectTransform,0,_popupHeight-1,PopupWidth,1);
            Rect(_popupEdges[2].rectTransform,0,0,1,_popupHeight); Rect(_popupEdges[3].rectTransform,PopupWidth-1,0,1,_popupHeight);
        }

        private void ClearUnavailableUi()
        {
            _popupUnavailable.Clear(); _showUnavailable=false;
            foreach(var row in _unavailableRows) if(row!=null)
            { row.Name.text=""; row.Source.text=""; row.Reason.text=""; row.Root.gameObject.SetActive(false); }
            if(_availabilityButton!=null) _availabilityButton.gameObject.SetActive(false);
            if(_availabilityLabel!=null) _availabilityLabel.text="";
        }
    }
}
