using System;
using System.Collections.Generic;
using Tylevo.FieldAttachments.Core;

internal static partial class Program
{
    private static void TestCrossSelectors()
    {
        var targets=new List<SelectorRect>();
        for(int g=0;g<4;g++)
        {
            var group=(AttachmentGroup)g; var bounds=CrossSelectorLayout.Group(group);
            var header=CrossSelectorLayout.Header(group); targets.Add(header);
            Check(bounds.X>=0 && bounds.Y>=0 && bounds.X+bounds.Width<=CrossSelectorLayout.Width &&
                bounds.Y+bounds.Height<=CrossSelectorLayout.Height,"Cross group stays inside shared logical plane "+group);
            for(int i=0;i<5;i++)
            {
                var cell=CrossSelectorLayout.Cell(group,i); targets.Add(cell);
                Check(bounds.Contains(cell.X,cell.Y) && bounds.Contains(cell.X+cell.Width,cell.Y+cell.Height),
                    "Cross choice stays inside its category "+group+"/"+i);
            }
            var installed=CrossSelectorLayout.Cell(group,0); var choice=CrossSelectorLayout.Cell(group,2);
            Check(group==AttachmentGroup.Optic ? installed.Y>choice.Y : group==AttachmentGroup.Underbarrel ? installed.Y<choice.Y :
                group==AttachmentGroup.Muzzle ? installed.X>choice.X : installed.X<choice.X,"Installed item faces center of cross "+group);
        }
        bool disjoint=true;
        for(int a=0;a<targets.Count;a++) for(int b=a+1;b<targets.Count;b++)
        {
            var x=targets[a]; var y=targets[b];
            if(x.X<=y.X+y.Width && y.X<=x.X+x.Width && x.Y<=y.Y+y.Height && y.Y<=x.Y+x.Height) disjoint=false;
        }
        Check(disjoint,"Cross buttons and headers never share a clickable area");
        Check(!targets.Exists(r=>r.Contains(CrossSelectorLayout.Width/2,CrossSelectorLayout.Height/2)),"Open center of cross does not trigger any action");
        Check(!targets.Exists(r=>r.Contains(float.NaN,100) || r.Contains(100,float.PositiveInfinity)),"Nonfinite cross pointer coordinates fail closed");
        foreach(int invalid in new[] {-1,5})
        {
            bool rejected=false; try { CrossSelectorLayout.Cell(AttachmentGroup.Optic,invalid); }
            catch(ArgumentOutOfRangeException) { rejected=true; }
            Check(rejected,"Invalid cross choice rejected: "+invalid);
        }
        PanelProjection.TryCreate(new PanelPoint(0,0,1),new PanelPoint(432,0,1),new PanelPoint(0,190,1),out var flat);
        Check(CrossSelectorLayout.TryPlace(flat!,2560,1440,1280,720,out var full) &&
            Math.Abs(full!.Right-full.Left-CrossSelectorLayout.Width)<.001 && Math.Abs(full.Bottom-full.Top-CrossSelectorLayout.Height)<.001,
            "Cross plane preserves physical tile aspect at full size");
        Check(flat!.Left==0 && flat.Right==432 && flat.Bottom==190,"Cross conversion does not mutate existing card geometry");
        PanelProjection.TryCreate(new PanelPoint(100,150,1),new PanelPoint(520,125,.94f),new PanelPoint(115,346,1.04f),out var skew);
        foreach(float aspect in new[] {4f/3,16f/10,16f/9,21f/9,32f/9})
        {
            float width=(float)Math.Sqrt(1920*1080*aspect),height=width/aspect;
            Check(CrossSelectorLayout.TryPlace(skew!,width,height,width*.60f,height*.60f,out var plane),"Cross weapon plane fits aspect "+aspect);
            Check(plane!.Left>=23.9 && plane.Right<=width-23.9 && plane.Top>=111.9 && plane.Bottom<=height-23.9,
                "Cross remains below header and inside screen at aspect "+aspect);
            bool hits=true;
            foreach(var r in targets)
            {
                var p=plane.Map((r.X+r.Width/2)/CrossSelectorLayout.Width,(r.Y+r.Height/2)/CrossSelectorLayout.Height);
                hits &= plane.TryUnmap(p.X,p.Y,out float u,out float v) && r.Contains(u*CrossSelectorLayout.Width,v*CrossSelectorLayout.Height);
            }
            Check(hits,"Every cross choice/header hit matches its displayed skew at aspect "+aspect);
            CrossSelectorLayout.TryPlace(skew!,width,height,width*.60f,height*.60f,out var reopened);
            Check(Math.Abs(plane.Left-reopened!.Left)<.001 && Math.Abs(plane.Top-reopened.Top)<.001,"Cross reopening has deterministic placement at aspect "+aspect);
            var a=plane.Map(0,0); var b=plane.Map(1,0); var c=plane.Map(0,1);
            Check(Math.Abs(a.Depth-b.Depth)>.1 && Math.Abs(a.Depth-c.Depth)>.1 && Math.Abs(a.Y-b.Y)>1,
                "Cross retains depth and tilt across arms at aspect "+aspect);
            Check(CrossSelectorLayout.TryFlat(width,height,out var fallback) && fallback!.Top>=111.9 && fallback.Bottom<=height-23.9,
                "Missing weapon geometry has a bounded cross fallback at aspect "+aspect);
        }
        // Leave enough room for the enlarged plane so this tests translation, not edge clamping.
        CrossSelectorLayout.TryPlace(skew!,3200,2000,1600,1000,out var first);
        CrossSelectorLayout.TryPlace(skew!,3200,2000,1680,1040,out var moved);
        Check(Math.Abs(moved!.Left-first!.Left-80)<.001 && Math.Abs(moved.Top-first.Top-40)<.001,
            "Cross follows weapon translation rather than staying at fixed screen coordinates");
        PanelProjection.TryCreate(new PanelPoint(0,0,.1f),new PanelPoint(432,0,.04f),new PanelPoint(0,190,.1f),out var extreme);
        Check(!CrossSelectorLayout.TryPlace(extreme!,1920,1080,1000,600,out _),"Near-plane crossing rejects shared plane for flat fallback");
        Check(!CrossSelectorLayout.TryFlat(400,300,out _) && !CrossSelectorLayout.TryFlat(float.NaN,1080,out _) &&
            !CrossSelectorLayout.TryPlace(flat,1920,1080,float.PositiveInfinity,600,out _),"Invalid cross screen and anchor coordinates fail closed");
        var gesture=new PointerGesture();
        gesture.Press("cross0/0|Candidates",1);
        Check(!gesture.Release("cross0/4|Candidates",1),"Dragging between installed item and full list cannot open either");
        gesture.Press("cross0/3|Uninstall",1);
        Check(!gesture.Release("cross0/3|Uninstall",2),"Cross layout/slot refresh invalidates held removal click");
        gesture.Press("cross0/3|Uninstall",3); gesture.Cancel();
        Check(!gesture.Release("cross0/3|Uninstall",3),"Closing or disabling cross cancels pressed action");
        gesture.Press("cross0/1|ChooseItem",4);
        Check(gesture.Release("cross0/1|ChooseItem",4) && !gesture.Release("cross0/1|ChooseItem",4),"Cross install click is consumed once");
        TestCrossControlSizes();
    }

    private static void TestCrossControlSizes()
    {
        // Actual canvas/anchor from F10 20260919-063559. The old angled muzzle control was tiny.
        PanelProjection.TryCreate(new PanelPoint(0,0,.2f),new PanelPoint(432*.9f,0,.2f),new PanelPoint(0,190*.9f,.2f),out var cameraCard);
        Check(CrossSelectorLayout.TryPlace(cameraCard!,1993.983f,1039.929f,1285.1f,720.3f,out var cross,true),
            "Enlarged cross fits the user's recorded canvas with weapon sway reserved");
        for(int g=0;g<4;g++)
        {
            var group=(AttachmentGroup)g; var item=CrossSelectorLayout.Cell(group,0);
            var remove=CrossSelectorLayout.Cell(group,3); var list=CrossSelectorLayout.Cell(group,4);
            var a=cross!.Map(item.X/CrossSelectorLayout.Width,item.Y/CrossSelectorLayout.Height);
            var b=cross.Map((item.X+item.Width)/CrossSelectorLayout.Width,(item.Y+item.Height)/CrossSelectorLayout.Height);
            Check(b.X-a.X>=120 && b.Y-a.Y>=94,"Installed tile is visibly larger at recorded canvas: "+group);
            foreach(var r in new[] {remove,list})
            {
                var top=cross.Map(r.X/CrossSelectorLayout.Width,r.Y/CrossSelectorLayout.Height);
                var end=cross.Map((r.X+r.Width)/CrossSelectorLayout.Width,(r.Y+r.Height)/CrossSelectorLayout.Height);
                Check(end.X-top.X>=87 && end.Y-top.Y>=30,"Cross action has a usable visible target at recorded canvas: "+group);
                var point=cross.Map((r.X+r.Width*.08f)/CrossSelectorLayout.Width,(r.Y+r.Height*.92f)/CrossSelectorLayout.Height);
                Check(cross.TryUnmap(point.X,point.Y,out float u,out float v) && r.Contains(u*CrossSelectorLayout.Width,v*CrossSelectorLayout.Height),
                    "Enlarged action accepts clicks near its visible edge: "+group);
            }
            Check(remove.Y>=item.Y+item.Height && remove.Y-(item.Y+item.Height)<=8 &&
                list.Y==remove.Y && remove.X<=item.X+item.Width/2 && list.X+list.Width>=item.X+item.Width/2,
                "REMOVE and LIST stay directly below the installed item even without candidates: "+group);
            var caption=CrossSelectorLayout.Caption(group); var bounds=CrossSelectorLayout.Group(group);
            Check(bounds.Contains(caption.X,caption.Y) && bounds.Contains(caption.X+caption.Width,caption.Y+caption.Height),
                "Enlarged caption fits its group: "+group);
        }
    }
}
