using System;
using System.Collections.Generic;

namespace Tylevo.FieldAttachments.Core
{
    public static partial class GlobalMcxInspection
    {
        public const string DonorName="Tylevo.FieldAttachments.mcx_look";
        public const int MaximumEvents=7; // Largest directly audited native inspection table in this batch.
        public static PoseMarker? ReferenceMarker(IEnumerable<PoseMarker> markers)
        {
            PoseMarker? reference=null;
            foreach(var marker in markers)
            {
                if(marker.WeaponTemplate!=McxInspectionSegment.Template) continue;
                if(reference!=null || !marker.Controller.StartsWith(McxInspectionSegment.Controller+"/",StringComparison.Ordinal) ||
                    McxInspectionSegment.MarkerDenial(marker,marker.WeaponTemplate,marker.Controller).Length!=0) return null;
                reference=marker;
            }
            return reference;
        }
        // Never persist donor timing as a native marker for the receiving gun.
        public static PoseMarker Retarget(PoseMarker reference,string template,string controller) => new PoseMarker {
            WeaponTemplate=template,Controller=controller,Layer=1,State=PoseMarker.SharedState,Time=reference.Time
        };
        public static bool CompatibleEvents(IReadOnlyList<McxInspectionEvent> events,float hold) => EventDenial(events,hold).Length==0;
        public static string EventDenial(IReadOnlyList<McxInspectionEvent> events,float hold)
        {
            if(!PoseMarker.Finite(hold) || hold<.45f || hold>.8f) return "invalid MCX hold";
            return NativeEventDenial(events);
        }
        public static string NativeEventDenial(IReadOnlyList<McxInspectionEvent> events)
        {
            if(events.Count<3 || events.Count>MaximumEvents) return "inspection event count outside verified 3-7 range";
            float previous=-1;
            for(int i=0;i<events.Count;i++)
            {
                var e=events[i];
                if(e==null || !PoseMarker.Finite(e.Time) || e.Time<=previous || e.Time<0 || e.Time>1 ||
                    e.Conditions!=0 || e.Boolean || e.Integer!=0 || e.Float!=0)
                    return "invalid, conditional or unordered inspection event at "+i;
                previous=e.Time;
                if(i==0)
                {
                    if(!e.Enabled || e.Name!="StartUtilityOperation" || e.Hash!=1134400241 || e.ParameterType!=0 || e.Text!="" || e.Time!=0)
                        return "inspection must start with native StartUtilityOperation at zero";
                    continue;
                }
                if(e.Name!="Sound" || e.Hash!=1554795451 || e.ParameterType!=3)
                    return "unverified inspection event: "+e.Name;
                // Native HandOn/HandOff are audio labels, not operation/IK transitions.
                // Audited weapons repeat, disable and retime these sounds. Preserve that table;
                // lifecycle safety comes from the owned Ready operation and native idle checks.
                // Disabled rows still require a verified audio signature, never a hidden gameplay event.
                switch(e.Text)
                {
                    case "HandOff": case "HandOn": case "HandOnHard":
                    case "GunFlip": case "GunFlip1": case "GunFlip2": case "GunFlip3": case "GunRotate":
                        break;
                    default: return "unverified inspection sound: "+e.Text;
                }
            }
            return "";
        }
        public static string MissingRigPath(Func<string,bool> exists)
        {
            foreach(string path in RequiredRigPaths) if(!exists(path)) return path;
            return "";
        }
    }
}
