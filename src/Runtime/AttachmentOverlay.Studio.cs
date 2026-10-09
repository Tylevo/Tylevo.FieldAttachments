using Tylevo.FieldAttachments.Core;
using UnityEngine;

namespace Tylevo.FieldAttachments.Runtime
{
    public sealed partial class AttachmentOverlay
    {
        private void UpdateStudioCrossLayout(float width,float height,bool current)
        {
            Vector2 weaponPoint=default;
            // Read the shared native midpoint; saved axes replace automatic readability/angle tuning.
            bool following=FollowWeapon && current && _anchors.ProjectCardPlane(width,height,0,0,out _,out weaponPoint,true);
            _crossProjection=null; _lastFollowing=following; _lastProjected=0;
            _layoutReason="Studio MCX gentle export; "+(following ? "bounded native weapon sway" : "authored homes (native sway unavailable/off)");
            bool visible=false;
            for(int g=0;g<4;g++)
            {
                var panel=_crossGroups[g]; var group=_groups[g];
                StudioCrossLayout.TryProject(group.Group,width,height,weaponPoint.x,weaponPoint.y,following,out panel.Projection);
                // TryProject already applies shared bounded sway to the exported perspective.
                // A second weapon-local projection magnifies bob/depth changes and captures
                // a different rest frame on each opening. Transitions hide this layout instead.
                group.Projection=panel.Projection;
                panel.Root.gameObject.SetActive(panel.Projection!=null); visible|=panel.Projection!=null;
                foreach(var mesh in panel.Meshes) mesh.SetProjection(panel.Projection,panel.Root,_canvasRect,StudioCrossLayout.Width,StudioCrossLayout.Height);
                Vector2 end=default;
                bool valid=panel.Projection!=null && panel.Visible && current && _anchors.Project(PreviewSlot(group.Group,group.Slot),width,height,out end);
                group.Leader.Show(valid); group.Leader.Second.enabled=false;
                if(!valid) continue;
                // Studio leaders start at the group's lower edge. Endpoint is always the real slot.
                var start=panel.Projection!.Map(.5f,1);
                Line(group.Leader.First,new Vector2(start.X,start.Y),end);
                Rect(group.Leader.Dot.rectTransform,end.x-2,end.y-2,4,4); _lastProjected++;
            }
            _crossRoot.gameObject.SetActive(visible); PlacePointerUi();
        }
    }
}
