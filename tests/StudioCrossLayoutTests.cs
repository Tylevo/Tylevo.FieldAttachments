using System;
using System.Reflection;
using System.Text.Json;
using Tylevo.FieldAttachments.Core;

internal static partial class Program
{
    private static void TestStudioCrossLayout()
    {
        // Independent expected points produced by Layout Studio's existing model.mjs from the
        // untouched user export. 216x187 DOM size measured in the actual preview browser.
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("studio-cross-projection.json")!;
        using var fixture=JsonDocument.Parse(stream);
        foreach(var entry in fixture.RootElement.GetProperty("groups").EnumerateObject())
        {
            var group=Enum.Parse<AttachmentGroup>(entry.Name);
            Check(StudioCrossLayout.TryProject(group,1902,992,0,0,false,out var projection),"Studio plane created: "+group);
            foreach(var point in entry.Value.EnumerateArray())
            {
                float x=point.GetProperty("localX").GetSingle(),y=point.GetProperty("localY").GetSingle();
                var actual=projection!.Map(x/216,y/187);
                Check(Math.Abs(actual.X-point.GetProperty("x").GetDouble())<.002 &&
                    Math.Abs(actual.Y-point.GetProperty("y").GetDouble())<.002,
                    "Matches original browser projection within .002 reference pixels: "+group+" "+x+","+y);
            }
            foreach(var size in new[] {(1902f,992f),(1993.983f,1039.929f),(1440f,1080f),(3440f,1440f)})
            {
                float w=size.Item1,h=size.Item2,s=Math.Min(w/1902,h/992);
                StudioCrossLayout.TryProject(group,w,h,w*StudioCrossLayout.RestX,h*StudioCrossLayout.RestY,true,out var plane);
                var origin=projection!.Map(0,0); var fitted=plane!.Map(0,0);
                Check(Math.Abs(fitted.X-((w-1902*s)/2+origin.X*s))<.002 &&
                    Math.Abs(fitted.Y-((h-992*s)/2+origin.Y*s))<.002,"Aspect-fit preserves authored position and perspective: "+group+" "+size);
                Check(plane.Left>0 && plane.Top>112 && plane.Right<w && plane.Bottom<h,"Export is visible without independent panel packing: "+group+" "+size);
                bool hits=true;
                foreach(int target in new[] {0,3,4,5})
                {
                    var rect=target==5 ? StudioCrossLayout.Header : StudioCrossLayout.Cell(target);
                    foreach(float fraction in new[] {.04f,.5f,.96f})
                    {
                        var pixel=plane.Map((rect.X+rect.Width*fraction)/216,(rect.Y+rect.Height*fraction)/187);
                        hits &= plane.TryUnmap(pixel.X,pixel.Y,out float u,out float v) && StudioCrossLayout.Hit(u*216,v*187)==target;
                    }
                }
                Check(hits,"Tilted item/header/REMOVE/LIST click centers and edges match the mesh: "+group+" "+size);
                var gap=plane.Map(108f/216,168f/187);
                Check(plane.TryUnmap(gap.X,gap.Y,out float gu,out float gv) && StudioCrossLayout.Hit(gu*216,gv*187)==-1,
                    "Gap between buttons cannot uninstall or open LIST: "+group+" "+size);
                StudioCrossLayout.TryProject(group,w,h,w*StudioCrossLayout.RestX+120,h*StudioCrossLayout.RestY+60,true,out var moved);
                var a=moved!.Map(0,0); var b=moved.Map(1,1); var oldB=plane.Map(1,1);
                Check(a.X>fitted.X && a.X-fitted.X<48*s && a.Y>fitted.Y && a.Y-fitted.Y<32*s &&
                    Math.Abs((b.X-oldB.X)-(a.X-fitted.X))<.002 && Math.Abs((b.Y-oldB.Y)-(a.Y-fitted.Y))<.002,
                    "Native sway translates the authored shape without counter-rotation or resizing: "+group+" "+size);
                StudioCrossLayout.TryProject(group,w,h,w*StudioCrossLayout.RestX,h*StudioCrossLayout.RestY,true,out var reopened);
                Check(Math.Abs(reopened!.Left-plane.Left)<.0001 && Math.Abs(reopened.Top-plane.Top)<.0001,
                    "Reopening after motion returns to the same dedicated position: "+group+" "+size);
            }
        }
        Check(!StudioCrossLayout.TryProject(AttachmentGroup.Optic,float.NaN,992,0,0,false,out _) &&
            !StudioCrossLayout.TryProject(AttachmentGroup.Optic,1902,0,0,0,false,out _) &&
            !StudioCrossLayout.TryProject((AttachmentGroup)9,1902,992,0,0,false,out _) &&
            !StudioCrossLayout.TryProject(AttachmentGroup.Optic,1902,992,float.PositiveInfinity,0,true,out _),"Invalid studio dimensions/group/native anchor fail closed");
        Check(StudioCrossLayout.TryProject(AttachmentGroup.Optic,1902,992,float.NaN,float.NaN,false,out _),"Missing native geometry retains the exported preset");
        Check(StudioCrossLayout.Hit(float.NaN,10)==-1 && StudioCrossLayout.Hit(10,float.PositiveInfinity)==-1 &&
            StudioCrossLayout.Hit(10,70)==-1 && StudioCrossLayout.Hit(80,30.5f)==-1,"Transparent studio margins and invalid pointer values have no actions");
        Check(StudioCrossLayout.Cell(1).Width==0 && StudioCrossLayout.Cell(2).Width==0,"Zero preview choices cannot move the exported installed card; candidates remain in LIST");
        var press=new PointerGesture(); press.Press("cross1/3|Uninstall",1); press.Cancel();
        Check(!press.Release("cross1/3|Uninstall",1),"Switching studio/legacy layout cancels an unfinished removal click");
        TestStudioMovementStability();
    }

    private static void TestStudioMovementStability()
    {
        // Projected receiver/muzzle excursions from bob, turning and changing camera distance.
        // Returning to zero models reopening/resuming at rest after each extreme excursion.
        var motion=new[] {(0f,0f),(250f,180f),(-800f,-500f),(10000f,-10000f),
            (0f,0f),(-10000f,10000f),(50f,-30f),(0f,0f)};
        foreach(var size in new[] {(1902f,992f),(1440f,1080f),(3440f,1440f)})
        {
            float w=size.Item1,h=size.Item2,scale=Math.Min(w/1902,h/992);
            var homes=new PanelProjection[4];
            for(int g=0;g<4;g++)
            {
                StudioCrossLayout.TryProject((AttachmentGroup)g,w,h,0,0,false,out var home);
                homes[g]=home!;
            }
            foreach(var offset in motion)
            {
                bool bounded=true,coherent=true,hits=true;
                float sharedX=0,sharedY=0;
                for(int g=0;g<4;g++)
                {
                    bool valid=StudioCrossLayout.TryProject((AttachmentGroup)g,w,h,
                        w*StudioCrossLayout.RestX+offset.Item1*scale,
                        h*StudioCrossLayout.RestY+offset.Item2*scale,true,out var plane);
                    if(!valid) { bounded=coherent=hits=false; continue; }
                    var origin=homes[g].Map(0,0); var actual=plane!.Map(0,0);
                    if(g==0) { sharedX=actual.X-origin.X; sharedY=actual.Y-origin.Y; }
                    bounded &= Math.Abs(actual.X-origin.X)<=48*scale+.002f &&
                        Math.Abs(actual.Y-origin.Y)<=32*scale+.002f &&
                        plane.Left>0 && plane.Top>0 && plane.Right<w && plane.Bottom<h;
                    foreach(var uv in new[] {(0f,0f),(1f,0f),(0f,1f),(1f,1f),(.5f,.5f)})
                    {
                        var before=homes[g].Map(uv.Item1,uv.Item2); var after=plane.Map(uv.Item1,uv.Item2);
                        coherent &= Math.Abs(after.X-before.X-sharedX)<.002f &&
                            Math.Abs(after.Y-before.Y-sharedY)<.002f && after.Depth==before.Depth;
                    }
                    foreach(int target in new[] {0,3,4,5})
                    {
                        var r=target==5 ? StudioCrossLayout.Header : StudioCrossLayout.Cell(target);
                        foreach(float edge in new[] {.04f,.96f})
                        {
                            var p=plane.Map((r.X+r.Width*edge)/216,(r.Y+r.Height*edge)/187);
                            hits &= plane.TryUnmap(p.X,p.Y,out float u,out float v) && StudioCrossLayout.Hit(u*216,v*187)==target;
                        }
                    }
                }
                Check(bounded,"Extreme native movement keeps all Studio cards visible within sway bounds: "+size+" "+offset);
                Check(coherent,"All Studio corners share one translation; no depth scaling, rotation or relative drift: "+size+" "+offset);
                Check(hits,"Moving Studio item/header/REMOVE/LIST edges remain clickable: "+size+" "+offset);
                if(offset==(0f,0f)) Check(Math.Abs(sharedX)<.002f && Math.Abs(sharedY)<.002f,
                    "Returning to rest after motion/open/resume has no captured-frame offset: "+size);
            }
        }
    }
}
