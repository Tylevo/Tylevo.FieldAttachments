using System;

namespace Tylevo.FieldAttachments.Core
{
    // Applied from docs/layouts/mcx-gentle.json, not estimated from the screenshot.
    // CSS reference coordinates: top-left origin, positive Z toward the viewer, Rz * Ry * Rx.
    public static class StudioCrossLayout
    {
        public const string Export = "FieldAttachments-layout-2026-09-19T14-00-53-481Z-5224602d.json";
        public const float ReferenceWidth=1902, ReferenceHeight=992, Perspective=1400;
        public const float Width=216, Height=187, Pitch=17, Yaw=-39, Roll=30;
        private static readonly double[] X={71.97033318119578,36.34824281150159,56.72295755362849,49.85632131446827};
        private static readonly double[] Y={58.67896896790442,53.61232062151632,44.03222908564197,72.88969464524963};
        // Stable MCX receiver/muzzle midpoint from F10 20260919-141340 (saved late pose).
        // No recentering on open, slot changes or scans. Every group gets the same bounded sway.
        public const float RestX=1249.1f/1993.983f, RestY=784.4f/1039.929f;
        public static SelectorRect Header => new SelectorRect(0,0,Width,30);
        public static SelectorRect Cell(int index)
        {
            if(index==0) return new SelectorRect(36,31,144,112);
            if(index==3 || index==4) return new SelectorRect((index-3)*112,151,104,36);
            if(index==1 || index==2) return new SelectorRect(0,0,0,0); // Export has zero inline candidates; use LIST.
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        public static int Hit(float x,float y)
        {
            if(Header.Contains(x,y)) return 5;
            if(Cell(0).Contains(x,y)) return 0;
            if(Cell(3).Contains(x,y)) return 3;
            if(Cell(4).Contains(x,y)) return 4;
            return -1;
        }
        public static bool TryProject(AttachmentGroup group,float width,float height,float weaponX,float weaponY,
            bool follow,out PanelProjection? plane)
        {
            plane=null;
            if((int)group<0 || (int)group>=4 || !Finite(width) || !Finite(height) || width<480 || height<400 ||
                (follow && (!Finite(weaponX) || !Finite(weaponY)))) return false;
            // Uniform aspect-fit preserves the authored shapes on a different canvas aspect ratio.
            float scale=Math.Min(width/ReferenceWidth,height/ReferenceHeight);
            float dx=follow ? DedicatedCardLayout.SoftMotion((weaponX-width*RestX)/scale,48)*scale : 0;
            float dy=follow ? DedicatedCardLayout.SoftMotion((weaponY-height*RestY)/scale,32)*scale : 0;
            PanelPoint Corner(float x,float y)
            {
                var p=ReferencePoint(group,x,y);
                return new PanelPoint((width-ReferenceWidth*scale)/2+p.X*scale+dx,
                    (height-ReferenceHeight*scale)/2+p.Y*scale+dy,p.Depth);
            }
            return PanelProjection.TryCreate(Corner(0,0),Corner(Width,0),Corner(0,Height),out plane);
        }
        private static PanelPoint ReferencePoint(AttachmentGroup group,float localX,float localY)
        {
            double x=localX-Width/2, y=localY-Height/2, z;
            double rad=Math.PI/180, c=Math.Cos(Pitch*rad), s=Math.Sin(Pitch*rad);
            z=y*s; y*=c;
            c=Math.Cos(Yaw*rad); s=Math.Sin(Yaw*rad);
            double nextX=x*c+z*s; z=-x*s+z*c; x=nextX;
            c=Math.Cos(Roll*rad); s=Math.Sin(Roll*rad);
            nextX=x*c-y*s; y=x*s+y*c; x=nextX;
            double depth=Perspective-z, k=Perspective/depth;
            return new PanelPoint((float)(ReferenceWidth/2+(X[(int)group]/100*ReferenceWidth+x-ReferenceWidth/2)*k),
                (float)(ReferenceHeight/2+(Y[(int)group]/100*ReferenceHeight+y-ReferenceHeight/2)*k),(float)depth);
        }
        private static bool Finite(float value)=>!float.IsNaN(value) && !float.IsInfinity(value);
    }
}
