using System;

namespace Tylevo.FieldAttachments.Core
{
    public struct SelectorRect
    {
        public float X,Y,Width,Height;
        public SelectorRect(float x,float y,float width,float height) { X=x; Y=y; Width=width; Height=height; }
        public bool Contains(float x,float y) => x>=X && x<=X+Width && y>=Y && y<=Y+Height;
    }
    public static class CrossSelectorLayout
    {
        public const float Width=1264, Height=1000, TileWidth=144, TileHeight=112;
        public static SelectorRect Group(AttachmentGroup group) => group==AttachmentGroup.Optic ? new SelectorRect(524,0,216,468) :
            group==AttachmentGroup.Underbarrel ? new SelectorRect(524,532,216,468) :
            group==AttachmentGroup.Muzzle ? new SelectorRect(32,386,456,228) : new SelectorRect(776,386,456,228);
        public static SelectorRect Header(AttachmentGroup group)
        { var r=Group(group); return new SelectorRect(r.X,r.Y+(group==AttachmentGroup.Optic ? 240 : 0),r.Width,32); }
        // Installed item, two carried choices, uninstall, full list. Transparent gaps never accept clicks.
        public static SelectorRect Cell(AttachmentGroup group,int index)
        {
            if(index<0 || index>4) throw new ArgumentOutOfRangeException(nameof(index));
            var r=Group(group); bool vertical=group==AttachmentGroup.Optic || group==AttachmentGroup.Underbarrel;
            // Keep actions beside the installed item even when the two outward choices are absent.
            if(index>=3) return new SelectorRect(r.X+(group==AttachmentGroup.Muzzle ? 240 : 0)+(index-3)*112,
                r.Y+(group==AttachmentGroup.Optic ? 396 : 156),104,36);
            float x=vertical ? 36 : (group==AttachmentGroup.Muzzle ? 2-index : index)*156;
            float y=group==AttachmentGroup.Optic ? (index==0 ? 276 : index==1 ? 120 : 0) :
                group==AttachmentGroup.Underbarrel ? (index==0 ? 36 : index==1 ? 236 : 356) : 36;
            return new SelectorRect(r.X+x,r.Y+y,TileWidth,TileHeight);
        }
        public static SelectorRect Caption(AttachmentGroup group)
        { var r=Group(group); return new SelectorRect(r.X+(group==AttachmentGroup.Muzzle ? 240 : 0),r.Y+(group==AttachmentGroup.Optic ? 440 : 200),216,24); }
        public static bool TryPlace(PanelProjection card,float width,float height,float weaponX,float weaponY,out PanelProjection? cross,bool faceCamera=false)
        {
            float u=Width/OverlayLayout.PanelWidth/2,v=Height/OverlayLayout.PanelHeight/2;
            cross=null;
            if(!PanelProjection.TryCreate(card.Map(.5f-u,.5f-v),card.Map(.5f+u,.5f-v),card.Map(.5f-u,.5f+v),out var plane)) return false;
            return Fit(plane!,width,height,weaponX,weaponY,out cross,faceCamera);
        }
        public static bool TryFlat(float width,float height,out PanelProjection? cross)
        {
            PanelProjection.TryCreate(new PanelPoint(0,0,1),new PanelPoint(Width,0,1),new PanelPoint(0,Height,1),out var plane);
            return Fit(plane!,width,height,width*.60f,height*.60f,out cross);
        }
        private static bool Fit(PanelProjection plane,float width,float height,float x,float y,out PanelProjection? cross,bool faceCamera=false)
        {
            cross=null;
            if(!Finite(width)||!Finite(height)||!Finite(x)||!Finite(y)||width<480||height<400) return false;
            float motionX=faceCamera ? DedicatedCardLayout.FacingMotionX : 0, motionY=faceCamera ? DedicatedCardLayout.FacingMotionY : 0;
            float w=plane.Right-plane.Left,h=plane.Bottom-plane.Top;
            float scale=Math.Min(1,Math.Min((width-48-motionX*2)/w,(height-136-motionY*2)/h));
            if(!Finite(scale)||scale<.35f) return false;
            float hw=w*scale/2,hh=h*scale/2;
            if(faceCamera)
                return plane.TryPlace(Math.Max(24+hw+motionX,Math.Min(width-24-hw-motionX,width*.60f))+DedicatedCardLayout.SoftMotion(x-width*.60f,motionX),
                    Math.Max(112+hh+motionY,Math.Min(height-24-hh-motionY,height*.60f))+DedicatedCardLayout.SoftMotion(y-height*.72f,motionY),scale,out cross);
            return plane.TryPlace(Math.Max(24+hw,Math.Min(width-24-hw,x)),
                Math.Max(112+hh,Math.Min(height-24-hh,y)),scale,out cross);
        }
        private static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
    }
}
