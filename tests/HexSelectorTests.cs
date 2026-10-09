using System;
using Tylevo.FieldAttachments.Core;

internal static partial class Program
{
    private static void TestHexSelectors()
    {
        for(int i=0;i<7;i++)
        {
            var c=HexSelectorLayout.Center(i);
            Check(HexSelectorLayout.Contains(i,c.x,c.y),"Hex center is clickable "+i);
            Check(!HexSelectorLayout.Contains(i,c.x+35,c.y+30),"Hex bounding-box corner is not clickable "+i);
            Check(HexSelectorLayout.Contains(i,c.x+35,c.y) && !HexSelectorLayout.Contains(i,c.x+37,c.y),"Hex side hit boundary "+i);
            Check(c.x-36>=0 && c.x+36<=HexSelectorLayout.Width && c.y-HexSelectorLayout.HexHeight/2>=0 &&
                c.y+HexSelectorLayout.HexHeight/2<217,"Hex fits before category caption "+i);
        }
        bool disjoint=true;
        for(int y=0;y<217;y++) for(int x=0;x<300;x++)
        {
            int hits=0; for(int i=0;i<7;i++) if(HexSelectorLayout.Contains(i,x,y)) hits++;
            if(hits>1) disjoint=false;
        }
        Check(disjoint,"Hex choices have no overlapping clickable area");
        Check(!HexSelectorLayout.Contains(7,150,111) && !HexSelectorLayout.Contains(-1,150,111) &&
            !HexSelectorLayout.Contains(0,float.NaN,111) && !HexSelectorLayout.Contains(0,float.PositiveInfinity,111),"Invalid hex hit inputs fail closed");
        PanelProjection.TryCreate(new PanelPoint(0,0,1),new PanelPoint(432,0,1),new PanelPoint(0,190,1),out var flat);
        Check(HexSelectorLayout.TryPlane(flat!,out var hex) && Math.Abs(hex!.Right-hex.Left-300)<.001 &&
            Math.Abs(hex.Bottom-hex.Top-280)<.001,"Hex plane keeps its own physical aspect ratio");
        Check(flat!.Left==0 && flat.Right==432 && flat.Bottom==190,"Hex conversion leaves default card geometry unchanged");
        PanelProjection.TryCreate(new PanelPoint(100,150,1),new PanelPoint(520,105,.86f),new PanelPoint(122,346,1.08f),out var skew);
        Check(HexSelectorLayout.TryPlane(skew!,out var skewHex),"Hex plane retains depth and weapon tilt");
        var normal=skew!.Map(.5f,.5f); var converted=skewHex!.Map(.5f,.5f);
        Check(Math.Abs(normal.X-converted.X)<.001 && Math.Abs(normal.Y-converted.Y)<.001 &&
            Math.Abs(normal.Depth-converted.Depth)<.00001,"Hex plane is centered on the same weapon projection");
        foreach(float aspect in new[] {4f/3,16f/10,16f/9,21f/9,32f/9})
        {
            float width=(float)Math.Sqrt(1920*1080*aspect),height=width/aspect;
            var homes=new PanelProjection[4]; bool placed=true,hits=true,bounds=true,stable=true;
            var layoutPlane=skewHex;
            if(!DedicatedCardLayout.TryPlace(layoutPlane,AttachmentGroup.Optic,width,height,width*.6f,height*.72f,out _)) layoutPlane=hex!; // Same flat fallback as the renderer.
            for(int g=0;g<4;g++)
            {
                if(!DedicatedCardLayout.TryPlace(layoutPlane,(AttachmentGroup)g,width,height,width*.6f,height*.72f,out var panel))
                { placed=false; continue; }
                homes[g]=panel!;
                bounds &= panel!.Left>=23.9 && panel.Right<=width-23.9 && panel.Top>=111.9 && panel.Bottom<=height-23.9;
                DedicatedCardLayout.TryPlace(layoutPlane,(AttachmentGroup)g,width,height,width*.6f,height*.72f,out var reopened);
                stable &= Math.Abs(panel.Left-reopened!.Left)<.001 && Math.Abs(panel.Top-reopened.Top)<.001;
                for(int i=0;i<7;i++)
                {
                    var c=HexSelectorLayout.Center(i); var point=panel.Map(c.x/300,c.y/280);
                    hits &= panel.TryUnmap(point.X,point.Y,out float u,out float v) && HexSelectorLayout.Contains(i,u*300,v*280);
                }
            }
            bool overlaps=false;
            for(int a=0;a<4;a++) for(int b=a+1;b<4;b++) if(homes[a]!=null && homes[b]!=null && homes[a].Overlaps(homes[b])) overlaps=true;
            Check(placed && bounds && !overlaps,"Hex groups fit dedicated homes at aspect "+aspect);
            Check(hits && stable,"Skewed hex hits and reopening stay stable at aspect "+aspect);
        }
        var gesture=new PointerGesture();
        gesture.Press("hex0/1|Candidates",1);
        Check(!gesture.Release("hex0/6|Candidates",1),"Dragging between installed and more hexes does not click either");
        gesture.Press("hex0/5|Uninstall",1);
        Check(!gesture.Release("hex0/5|Uninstall",2),"Switching style/slot invalidates an uninstall press");
        gesture.Press("hex0/5|Uninstall",3);
        Check(gesture.Release("hex0/5|Uninstall",3) && !gesture.Release("hex0/5|Uninstall",3),"A valid hex click can be consumed only once");
    }
}
