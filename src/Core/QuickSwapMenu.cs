using System;

namespace Tylevo.FieldAttachments.Core
{
    // One mouse menu for every UI style. Position choices retain exact native slot identities.
    public static class QuickSwapMenu
    {
        public const float Width=224, Height=388, RowsTop=38, RowHeight=104;
        public const int PageSize=6;
        public static SelectorRect Close => new SelectorRect(188,4,28,28);
        public static int Pages(int count) => Math.Max(1,(Math.Max(0,count)+PageSize-1)/PageSize);
        public static int Page(int page,int count) => Math.Max(0,Math.Min(Pages(count)-1,page));
        public static float Footer(int visible) => RowsTop+Math.Max(1,(Math.Min(PageSize,Math.Max(0,visible))+1)/2)*RowHeight;
        public static float PopupHeight(int visible) => Footer(visible)+38;
        public static SelectorRect Cell(int index) => new SelectorRect(8+index%2*106,RowsTop+index/2*RowHeight,102,100);
        public static int Row(float x,float y)
        {
            if(!PoseMarker.Finite(x) || !PoseMarker.Finite(y)) return -1;
            for(int i=0;i<PageSize;i++) if(Cell(i).Contains(x,y)) return i;
            return -1;
        }
    }
}
