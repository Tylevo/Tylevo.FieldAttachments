using System;
using Tylevo.FieldAttachments.Core;

internal static partial class Program
{
    private static void TestWeaponPanelAttachment()
    {
        var owner=new object();
        // Independent pinhole camera with a rigid weapon frame; exercise the production
        // three-corner attachment with lowering, rotation, raising and pointer inversion.
        const float focal=900, centerX=960, centerY=540;
        foreach(AttachmentGroup group in Enum.GetValues(typeof(AttachmentGroup)))
        {
            StudioCrossLayout.TryProject(group,1920,1080,0,0,false,out var exported);
            float depthScale=1/exported!.Map(.5f,.5f).Depth;
            CardVector Local(PanelPoint p) => new CardVector((p.X-centerX)*p.Depth*depthScale/focal,
                (p.Y-centerY)*p.Depth*depthScale/focal,p.Depth*depthScale);
            var attached=WeaponPanelAttachment.Capture(owner,exported,Local)!;
            foreach(var motion in new[] {(0f,0f,0f),(0f,.55f,0f),(.18f,.55f,.35f),(0f,0f,0f)})
            {
                PanelPoint Screen(CardVector p)
                {
                    float c=(float)Math.Cos(motion.Item3),s=(float)Math.Sin(motion.Item3);
                    float x=p.X*c-p.Y*s+motion.Item1,y=p.X*s+p.Y*c+motion.Item2;
                    return new PanelPoint(centerX+focal*x/p.Z,centerY+focal*y/p.Z,p.Z);
                }
                Check(attached.Project(owner,Screen,out var moved),group+" follows live rigid weapon frame");
                var expected=Screen(Local(exported.Map(.5f,.5f))); var actual=moved!.Map(.5f,.5f);
                Check(Math.Abs(actual.X-expected.X)<.002 && Math.Abs(actual.Y-expected.Y)<.002,
                    "Card center tracks gun lowering/rotation without the old 32-pixel sway cap");
                foreach(int index in new[] {0,3,4})
                {
                    var r=StudioCrossLayout.Cell(index);
                    var p=moved.Map((r.X+r.Width/2)/StudioCrossLayout.Width,(r.Y+r.Height/2)/StudioCrossLayout.Height);
                    Check(moved.TryUnmap(p.X,p.Y,out float u,out float v) && StudioCrossLayout.Hit(u*216,v*187)==index,
                        "Moving card hit surface matches displayed item/REMOVE/LIST");
                }
            }
            Check(!attached.Project(new object(),p=>new PanelPoint(1,1,1),out _),"Changed weapon frame cannot reuse old corners");
            Check(!attached.Project(owner,p=>new PanelPoint(p.X,p.Y,-1),out _),"Behind-camera cards hide rather than float at a fallback home");
            Check(WeaponPanelAttachment.Capture(owner,exported,p=>new CardVector(float.NaN,0,0))==null,
                "Invalid local geometry is not attached");
        }
    }
}
