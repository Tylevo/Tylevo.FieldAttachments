using System;
using System.Collections.Generic;

namespace Tylevo.FieldAttachments.Core
{
    public enum CustomStmReturnResult { Waiting, BlendToIdle, YieldToNative, Fault }

    public static class CustomStmInspection
    {
        public const string ClipName = "Tylevo.FieldAttachments.stm_presentation";
        public const float Duration = 3.3333333f, Hold = .22f, ExitEnd = .45f, LatestReplacement = .06f;
        public const string M4Template = "5447a9cd4bdc2dbd208b4567";
        public const string M700Template = "5bfea6e90db834001b7347f3";
        public const string M870Template = "5a7828548dc32e5a9c28b516";
        public const string Glock17Template = "5a7ae0c351dfba0017554310";
        // Directly audited recipient catalog; the shared rig alone does not opt in a gun.
        public static string ControllerFor(string template)
        {
            template = InstalledWeaponAliases.Canonical(template);
            return template == StmPresentation.Template ? StmPresentation.Controller :
            template == M4Template ? "weapon_colt_m4a1_556x45" : template == McxInspectionSegment.Template ? McxInspectionSegment.Controller :
            template == M700Template ? "weapon_remington_model_700_762x51" : template == M870Template ? "weapon_remington_model_870_12g" :
            template == Glock17Template ? "weapon_glock_glock_17_gen3_9x19" : GeneratedPresentationFamilies.For(template)?.Controller ?? AuditedWeaponPresentations.For(template)?.Profiles[0].Controller ?? "";
        }
        public static string NativeClipFor(string template)
        {
            template = InstalledWeaponAliases.Canonical(template);
            return template == StmPresentation.Template ? "stm9_look" :
            template == M4Template ? "m4a1_look" : template == McxInspectionSegment.Template ? "mcx_look" :
            template == M700Template ? "m700_look" : template == M870Template ? "m870_look" : template == Glock17Template ? "glock_look" : GeneratedPresentationFamilies.For(template)?.NativeClip ?? AuditedWeaponPresentations.For(template)?.Profiles[0].Clip ?? "";
        }
        // Shared held angle/support arm, with an individually fitted firing grip for each recipient.
        public static string BundleFor(string template)
        {
            template = InstalledWeaponAliases.Canonical(template);
            return template == M4Template ? "m4a1_presentation" :
            template == McxInspectionSegment.Template ? "mcx_presentation" : template == M700Template ? "m700_presentation" :
            template == M870Template ? "m870_presentation" : template == Glock17Template ? "glock17_presentation" :
            template == StmPresentation.Template ? "stm_presentation" : GeneratedPresentationFamilies.For(template)?.Bundle ?? AuditedWeaponPresentations.For(template)?.Bundle ?? "";
        }
        public static string ClipFor(string template) => BundleFor(template).Length == 0 ? "" : "Tylevo.FieldAttachments." + BundleFor(template);
        public static bool IsAuthoredClip(string? name) => name == ClipName || name == ClipFor(M4Template) ||
            name == ClipFor(McxInspectionSegment.Template) || name == ClipFor(M700Template) ||
            name == ClipFor(M870Template) || name == ClipFor(Glock17Template) || GeneratedPresentationFamilies.IsAuthoredClip(name) || AuditedWeaponPresentations.IsAuthoredClip(name);
        public static bool ControllerMatches(string template,string controller)
        {
            template = InstalledWeaponAliases.Canonical(template);
            return AuditedWeaponPresentations.For(template)!=null ?
            AuditedWeaponPresentations.ControllerMatches(template,controller) : ControllerFor(template).Length!=0 && ControllerFor(template)==controller;
        }
        public static int EventLimit(string template,string clip)
        {
            template = InstalledWeaponAliases.Canonical(template);
            return AuditedWeaponPresentations.For(template)!=null ? AuditedWeaponPresentations.EventLimit(template,clip) :
            CustomMcxBackport.Matches(template,clip) ? CustomMcxBackport.EventCountFor(template) : GlobalMcxInspection.MaximumEvents;
        }
        public static bool Requested(bool enabled, string template, bool learn) => enabled && !learn && ControllerFor(template).Length != 0;
        public static bool NativeMcxRequested(bool nativeEnabled, bool customEnabled, string template, bool learn) =>
            nativeEnabled && template == McxInspectionSegment.Template && !Requested(customEnabled, template, learn);
        public static string RecipientDenial(string template, string controller, string clip, float duration, bool legacy, bool human, int events)
        {
            template = InstalledWeaponAliases.Canonical(template);
            if(AuditedWeaponPresentations.For(template)!=null)
                return !legacy && !human && events==0 && AuditedWeaponPresentations.MetadataMatches(template,controller,clip,duration) ? "" : "audited recipient controller/clip/type/duration/events changed";
            string expected = ControllerFor(template);
            if (expected.Length == 0 || controller != expected) return "custom animation recipient/controller outside audited recipient set";
            bool backport = CustomMcxBackport.Matches(template, clip);
            if ((!backport && clip != NativeClipFor(template)) || !PoseMarker.Finite(duration) ||
                Math.Abs(duration - (backport ? CustomMcxBackport.NativeDurationFor(template) : Duration)) > (backport ? .001f : .05f) || legacy || human || events != 0)
                return "custom animation native clip identity/type/duration/events changed";
            return "";
        }
        public static string EventDenial(string template, string nativeClip, IReadOnlyList<McxInspectionEvent> events)
        {
            template = InstalledWeaponAliases.Canonical(template);
            return AuditedWeaponPresentations.For(template)!=null ? AuditedWeaponPresentations.EventDenial(template,nativeClip,events) :
            CustomMcxBackport.Matches(template, nativeClip) ? CustomMcxBackport.EventDenial(template, events) : GlobalMcxInspection.NativeEventDenial(events);
        }
        public static bool ValidClip(string template, string name, float duration, bool legacy, bool human, int events) =>
            ClipFor(template).Length != 0 && name == ClipFor(template) && PoseMarker.Finite(duration) && Math.Abs(duration - Duration) < .001f && !legacy && !human && events == 0;
        public static string StateName(string template,string controller,int state)
        {
            template = InstalledWeaponAliases.Canonical(template);
            return AuditedWeaponPresentations.For(template)!=null ?
            AuditedWeaponPresentations.StateName(template,controller,state) : ControllerMatches(template,controller) && state==PoseMarker.SharedState ? "Hands.LOOK" : "";
        }
        public static bool StateMatches(string template,string controller,int state) => StateName(template,controller,state).Length!=0;
        public const string LeftHandMarker="Weapon_root/Weapon_root_anim/weapon/weapon_L_hand_marker";
        public static string MissingRigPath(string template,Func<string,bool> exists)
        {
            template = InstalledWeaponAliases.Canonical(template);
            foreach(string path in GlobalMcxInspection.RequiredRigPaths)
                if(!(template=="5b3b713c5acfc4330140bd8d" && path==LeftHandMarker) && !exists(path))return path;
            return "";
        }
        public static bool EarlyEnough(float time) => PoseMarker.Finite(time) && time >= 0 && time <= LatestReplacement;
    }

    // One authored exit, followed by the existing verified idle/cancel path. Interruptions
    // yield to native playback; uncertain playback never causes another blend attempt.
    public sealed class CustomStmReturn
    {
        private float _started;
        private bool _earlyClose;
        public bool Pending { get; private set; }
        public bool Begin(float now, bool earlyClose = false)
        {
            if (Pending || !PoseMarker.Finite(now)) return false;
            Pending = true; _started = now; _earlyClose = earlyClose; return true;
        }
        public CustomStmReturnResult Observe(float now, float time, bool safe, bool owned, bool clipMatches, bool ready = true)
        {
            if (!Pending) return CustomStmReturnResult.YieldToNative;
            if (!safe || !owned) { Cancel(); return CustomStmReturnResult.YieldToNative; }
            if (!PoseMarker.Finite(now) || now < _started || now - _started > 4 || (!_earlyClose && !clipMatches))
            { Cancel(); return CustomStmReturnResult.Fault; }
            // Before our inspection is ready, time belongs to the preceding animator
            // state (possibly a looping idle), not to the inspection being closed.
            // Keep ownership and the bounded wait above; validate its timeline only
            // once the caller confirms the owned inspection state is ready.
            if (_earlyClose && !ready) return CustomStmReturnResult.Waiting;
            if (!PoseMarker.Finite(time) || time < 0 || time > 1.1f)
            { Cancel(); return CustomStmReturnResult.Fault; }
            // A close during entrance waits for the native start event, then blends directly
            // to the captured idle. It must not finish raising the gun after O was closed.
            if (!ready || (!_earlyClose && time < CustomStmInspection.ExitEnd)) return CustomStmReturnResult.Waiting;
            Cancel(); return CustomStmReturnResult.BlendToIdle;
        }
        public void Cancel() { Pending = false; }
    }
}
