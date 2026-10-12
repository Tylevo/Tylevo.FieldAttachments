using System;
using System.Collections.Generic;

namespace Tylevo.FieldAttachments.Core
{
    public static class ComparisonSummaryLayout
    {
        private const float Gap=8, Edge=20;

        public static bool TryPlace(SelectorRect anchor,float panelWidth,float panelHeight,float viewportWidth,float viewportHeight,
            IReadOnlyList<SelectorRect> obstacles,out SelectorRect bounds)
        {
            bounds=default;
            if(!Valid(anchor) || !Positive(panelWidth) || !Positive(panelHeight) || !Positive(viewportWidth) ||
                !Positive(viewportHeight) || panelWidth>viewportWidth-2*Edge || panelHeight>viewportHeight-2*Edge || obstacles==null) return false;
            foreach(var obstacle in obstacles)
                if(!Finite(obstacle.X) || !Finite(obstacle.Y) || !Finite(obstacle.Width) || !Finite(obstacle.Height)) return false;

            // A small fixed set keeps the same summary position while hovering rows in one popup.
            for(int side=0;side<4;side++)
                for(int alignment=0;alignment<3;alignment++)
                {
                    float fraction=alignment==0 ? 0 : alignment==1 ? 1 : .5f;
                    float x,y;
                    if(side<2)
                    {
                        x=Clamp(anchor.X+(anchor.Width-panelWidth)*fraction,Edge,viewportWidth-Edge-panelWidth);
                        y=side==0 ? anchor.Y-Gap-panelHeight : anchor.Y+anchor.Height+Gap;
                    }
                    else
                    {
                        x=side==2 ? anchor.X+anchor.Width+Gap : anchor.X-Gap-panelWidth;
                        y=Clamp(anchor.Y+(anchor.Height-panelHeight)*fraction,Edge,viewportHeight-Edge-panelHeight);
                    }
                    var candidate=new SelectorRect(x,y,panelWidth,panelHeight);
                    if(x<Edge || y<Edge || x+panelWidth>viewportWidth-Edge || y+panelHeight>viewportHeight-Edge ||
                        Overlaps(candidate,anchor)) continue;
                    bool clear=true;
                    foreach(var obstacle in obstacles)
                        if(obstacle.Width>0 && obstacle.Height>0 && Overlaps(candidate,obstacle)) { clear=false; break; }
                    if(!clear) continue;
                    bounds=candidate; return true;
                }
            return false;
        }

        private static bool Overlaps(SelectorRect a,SelectorRect b)=>a.X<b.X+b.Width+Gap && a.X+a.Width+Gap>b.X &&
            a.Y<b.Y+b.Height+Gap && a.Y+a.Height+Gap>b.Y;
        private static float Clamp(float value,float min,float max)=>Math.Max(min,Math.Min(max,value));
        private static bool Valid(SelectorRect rect)=>Finite(rect.X) && Finite(rect.Y) && Positive(rect.Width) && Positive(rect.Height);
        private static bool Positive(float value)=>Finite(value) && value>0;
        private static bool Finite(float value)=>!float.IsNaN(value) && !float.IsInfinity(value);
    }
}
