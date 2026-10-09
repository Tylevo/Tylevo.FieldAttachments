using System;
using Tylevo.FieldAttachments.Core;

internal static partial class Program
{
    private static void TestCameraFacingCards()
    {
        WeaponCardBasis.TryCreate(new CardVector(-.4867f,-.055f,.282f),new CardVector(-.0668f,.9947f,.0787f),out var nativeRight,out var nativeUp);
        Check(WeaponCardBasis.TryTune(nativeRight,nativeUp,1,15,out var right,out var up,true) &&
            right.X==1 && right.Y==0 && right.Z==0 && up.X==0 && up.Y==1 && up.Z==0,
            "Camera-facing surfaces ignore native skew, roll and saved clockwise trim");
        Check(WeaponCardBasis.TryTune(default,default,.55f,5,out var endRight,out var endUp,true) &&
            endRight.X==1 && endUp.Y==1,"Camera-facing surfaces do not require a valid barrel/top basis");
        Check(!WeaponCardBasis.TryTune(default,default,.55f,5,out _,out _),"Original perspective mode retains degenerate-basis rejection");
        WeaponCardBasis.TryTune(nativeRight,nativeUp,.55f,5,out var angledRight,out var angledUp);
        Check(Math.Abs(angledRight.Z)>.1 && CardVector.Distance(angledRight,right)>.1 && CardVector.Distance(angledUp,up)>.01,
            "Disabling camera-facing restores existing perspective and trim");

        PanelProjection Surface(float depth)
        {
            // Same central-ray/viewport-height sizing as the adapter; camera-space fixture, no Unity claim.
            float unit=depth/900, scale=.9f; var center=new CardVector(0,0,depth);
            var a=center-right*(432*.5f*scale*unit)+up*(190*.5f*scale*unit);
            PanelPoint Project(CardVector v)=>new PanelPoint(960+900*v.X/v.Z,540-900*v.Y/v.Z,v.Z);
            if(!PanelProjection.TryCreate(Project(a),Project(a+right*(432*scale*unit)),Project(a-up*(190*scale*unit)),out var p))
                throw new Exception("Invalid billboard fixture");
            return p!;
        }
        var near=Surface(.25f); var far=Surface(2.5f);
        bool Square(PanelProjection p)
        {
            var a=p.Map(0,0); var b=p.Map(1,0); var c=p.Map(0,1); var d=p.Map(1,1);
            return Math.Abs(a.Y-b.Y)<.001 && Math.Abs(c.Y-d.Y)<.001 && Math.Abs(a.X-c.X)<.001 &&
                Math.Abs(b.X-d.X)<.001 && Math.Abs(a.Depth-d.Depth)<.00001;
        }
        Check(Square(near) && Square(far) && near.Map(0,0).Depth==.25f && far.Map(0,0).Depth==2.5f,
            "Billboards remain square while retaining the observed weapon depth");
        Check(Math.Abs(near.Right-far.Right)<.001 && Math.Abs(near.Top-far.Top)<.001,
            "Native depth changes preserve readable card size");
        foreach(float aspect in new[] {4f/3,16f/10,16f/9,21f/9,32f/9})
        {
            float width=(float)Math.Sqrt(1920*1080*aspect),height=width/aspect;
            for(int mode=0;mode<3;mode++)
            {
                var source=near;
                if(mode==1 && HexSelectorLayout.TryPlane(near,out var hex)) source=hex!;
                bool valid=true,bounds=true,square=true,hits=true,sway=true,stable=true,separate=true;
                var homes=new PanelProjection[4];
                for(int g=0;g<(mode==2 ? 1 : 4);g++)
                {
                    PanelProjection Place(float x,float y)
                    {
                        PanelProjection? p;
                        bool ok=mode==2 ? CrossSelectorLayout.TryPlace(source,width,height,x,y,out p,true) :
                            DedicatedCardLayout.TryPlace(source,(AttachmentGroup)g,width,height,x,y,out p,true);
                        if(!ok) { valid=false; return near; } return p!;
                    }
                    // Beyond the old hard-clamp limits: small movements must still move cards in both axes.
                    float x=width*.60f-240,y=height*.72f+150;
                    var initial=Place(x,y); homes[g]=initial;
                    var moved=Place(x+30,y-30);
                    sway &= moved.Left>initial.Left+.1f && moved.Top<initial.Top-.1f;
                    var reopened=Place(x,y);
                    stable &= Math.Abs(reopened.Left-initial.Left)<.001 && Math.Abs(reopened.Top-initial.Top)<.001;
                    foreach(float extreme in new[] {-10000f,10000f})
                    {
                        var p=Place(extreme,extreme);
                        bounds &= p.Left>=23.9 && p.Right<=width-23.9 && p.Top>=111.9 && p.Bottom<=height-23.9;
                        square &= Square(p);
                        var point=p.Map(.23f,.72f);
                        hits &= p.TryUnmap(point.X,point.Y,out float u,out float v) && Math.Abs(u-.23f)<.0001 && Math.Abs(v-.72f)<.0001;
                    }
                    for(int j=0;j<g;j++) separate &= !initial.Overlaps(homes[j]);
                }
                Check(valid && bounds && separate,"Camera-facing layout stays separated and screen-bounded: mode="+mode+" aspect="+aspect);
                Check(square && hits,"Camera-facing faces and inverse pointer mapping agree: mode="+mode+" aspect="+aspect);
                Check(sway && stable,"Camera-facing motion remains live beyond old limits and reopens consistently: mode="+mode+" aspect="+aspect);
            }
        }
        Check(!DedicatedCardLayout.TryPlace(near,AttachmentGroup.Optic,1920,1080,float.NaN,700,out _,true) &&
            !CrossSelectorLayout.TryPlace(near,1920,1080,1100,float.PositiveInfinity,out _,true),
            "Camera-facing motion still rejects invalid anchor coordinates");
    }
}
