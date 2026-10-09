using System;

namespace Tylevo.FieldAttachments.Core
{
    public static class HexSelectorLayout
    {
        public const float Width=300, Height=280, Radius=36;
        public const float HexHeight=62.35383f, CenterX=150, CenterY=111, Step=67;
        public static (float x,float y) Center(int index)
        {
            if (index<0 || index>6) throw new ArgumentOutOfRangeException(nameof(index));
            if (index==0) return (CenterX,CenterY);
            double angle=(-90+(index-1)*60)*Math.PI/180;
            return (CenterX+Step*(float)Math.Cos(angle),CenterY+Step*(float)Math.Sin(angle));
        }
        public static bool Contains(int index,float x,float y)
        {
            if (index<0 || index>6 || float.IsNaN(x) || float.IsNaN(y) || float.IsInfinity(x) || float.IsInfinity(y)) return false;
            var center=Center(index);
            float dx=Math.Abs(x-center.x),dy=Math.Abs(y-center.y);
            return dy<=HexHeight/2 && dx<=Radius-dy/1.7320508f;
        }
        public static bool TryPlane(PanelProjection card,out PanelProjection? hex)
        {
            float u=Width/OverlayLayout.PanelWidth/2,v=Height/OverlayLayout.PanelHeight/2;
            return PanelProjection.TryCreate(card.Map(.5f-u,.5f-v),card.Map(.5f+u,.5f-v),card.Map(.5f-u,.5f+v),out hex);
        }
    }
}
