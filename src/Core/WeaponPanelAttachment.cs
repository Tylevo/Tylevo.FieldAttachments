using System;

namespace Tylevo.FieldAttachments.Core
{
    // Three corners in the animated weapon's local space. Snapshot/slot refreshes
    // do not redefine this frame; mesh and pointer use the same reprojected plane.
    public sealed class WeaponPanelAttachment
    {
        public object Owner { get; }
        private readonly CardVector _a, _b, _c;
        private WeaponPanelAttachment(object owner, CardVector a, CardVector b, CardVector c)
        { Owner=owner; _a=a; _b=b; _c=c; }
        public static WeaponPanelAttachment? Capture(object owner, PanelProjection plane, Func<PanelPoint,CardVector> toLocal)
        {
            var a=toLocal(plane.Map(0,0)); var b=toLocal(plane.Map(1,0)); var c=toLocal(plane.Map(0,1));
            return Valid(a) && Valid(b) && Valid(c) ? new WeaponPanelAttachment(owner,a,b,c) : null;
        }
        public bool Project(object owner, Func<CardVector,PanelPoint> toScreen, out PanelProjection? plane)
        {
            plane=null;
            return ReferenceEquals(Owner,owner) && PanelProjection.TryCreate(toScreen(_a),toScreen(_b),toScreen(_c),out plane);
        }
        private static bool Valid(CardVector v) => PoseMarker.Finite(v.X) && PoseMarker.Finite(v.Y) && PoseMarker.Finite(v.Z);
    }
}
