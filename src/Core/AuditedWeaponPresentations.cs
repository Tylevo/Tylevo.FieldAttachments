using System;
using System.Collections.Generic;
using System.Linq;

namespace Tylevo.FieldAttachments.Core
{
    public sealed class NativePresentationProfile
    {
        public readonly string Controller, Clip, BaseController, BaseClip;
        public readonly float Duration;
        public readonly int State;
        public readonly string StateName;
        public readonly bool NativeOverride;
        public readonly KeyValuePair<string,string>[] Overrides;
        public readonly McxInspectionEvent[] Events;
        public NativePresentationProfile(string controller, string clip, float duration, string baseController, string baseClip,
            bool nativeOverride, KeyValuePair<string,string>[] overrides, McxInspectionEvent[] events, int state=PoseMarker.SharedState, string stateName="Hands.LOOK")
        { Controller=controller; Clip=clip; Duration=duration; BaseController=baseController; BaseClip=baseClip; NativeOverride=nativeOverride; Overrides=overrides; Events=events; State=state; StateName=stateName; }
    }
    public sealed class WeaponPresentationEntry
    {
        public readonly string Template, Bundle, Sha256;
        public readonly NativePresentationProfile[] Profiles;
        public WeaponPresentationEntry(string template,string bundle,string sha256,NativePresentationProfile[] profiles)
        { Template=template; Bundle=bundle; Sha256=sha256; Profiles=profiles; }
    }
    public static class AuditedWeaponPresentations
    {
        public static WeaponPresentationEntry? For(string template)
        {
            string canonical = InstalledWeaponAliases.Canonical(template);
            return GeneratedWeaponPresentations.Entries.FirstOrDefault(e=>e.Template==canonical) ??
                AdditionalWeaponPresentations.Entries.FirstOrDefault(e=>e.Template==canonical);
        }
        public static NativePresentationProfile[] StateProfiles(string template,string controller,int state) =>
            For(template)?.Profiles.Where(p=>p.Controller==controller && p.State==state).ToArray() ?? Array.Empty<NativePresentationProfile>();
        public static string StateName(string template,string controller,int state) => StateProfiles(template,controller,state).FirstOrDefault()?.StateName ?? "";
        public static bool IsAuthoredClip(string? name) => name!=null && (GeneratedWeaponPresentations.Entries.Any(e=>name=="Tylevo.FieldAttachments."+e.Bundle) || AdditionalWeaponPresentations.Entries.Any(e=>name=="Tylevo.FieldAttachments."+e.Bundle));
        public static bool ControllerMatches(string template,string controller) => For(template)?.Profiles.Any(p=>p.Controller==controller)==true;
        public static bool MetadataMatches(string template,string controller,string clip,float duration) => PoseMarker.Finite(duration) &&
            For(template)?.Profiles.Any(p=>p.Controller==controller && p.Clip==clip && Math.Abs(p.Duration-duration)<.001f)==true;
        public static int EventLimit(string template,string clip) => For(template)?.Profiles.Where(p=>p.Clip==clip).Select(p=>p.Events.Length).DefaultIfEmpty(0).Max() ?? 0;
        public static string EventDenial(string template,string clip,IReadOnlyList<McxInspectionEvent> events)
        {
            var entry=For(template);
            return entry!=null && entry.Profiles.Any(p=>p.Clip==clip && EventsMatch(p.Events,events)) ? "" : "audited native inspection event signature changed";
        }
        public static bool EventsMatch(IReadOnlyList<McxInspectionEvent> expected,IReadOnlyList<McxInspectionEvent> actual)
        {
            if(expected.Count!=actual.Count || expected.Count<3 || expected.Count>32) return false;
            for(int i=0;i<expected.Count;i++)
            {
                var a=actual[i];var e=expected[i];
                if(a==null || !PoseMarker.Finite(a.Time) || !PoseMarker.Finite(a.Float) || Math.Abs(a.Time-e.Time)>.000001f ||
                    a.Name!=e.Name || a.Hash!=e.Hash || a.Enabled!=e.Enabled || a.ParameterType!=e.ParameterType || a.Text!=e.Text ||
                    a.Boolean!=e.Boolean || a.Integer!=e.Integer || a.Float!=e.Float || a.Conditions!=e.Conditions ||
                    a.ConditionData==null || a.ConditionData.Length!=e.ConditionData.Length || a.Conditions!=a.ConditionData.Length) return false;
                for(int j=0;j<e.ConditionData.Length;j++)
                {
                    var x=a.ConditionData[j];var y=e.ConditionData[j];
                    if(x==null || !PoseMarker.Finite(x.Float) || x.Parameter!=y.Parameter || x.Type!=y.Type || x.Mode!=y.Mode ||
                        x.Integer!=y.Integer || x.Float!=y.Float || x.Boolean!=y.Boolean) return false;
                }
            }
            return true;
        }
        // Only exact built-in variant overrides may be flattened into our neutral wrapper.
        // Empty mappings mean "use the base clip" and are normalized by the caller.
        public static bool OverrideMatches(string template,string controller,string baseController,string clip,
            IReadOnlyList<KeyValuePair<string,string>> changes,out string key)
        {
            key="";
            var entry=For(template);
            if(entry==null || changes.Count>2048 || changes.Select(p=>p.Key).Distinct(StringComparer.Ordinal).Count()!=changes.Count) return false;
            foreach(var p in entry.Profiles)
            {
                if(!p.NativeOverride || p.Controller!=controller || p.BaseController!=baseController || p.Clip!=clip || p.Overrides.Length!=changes.Count)continue;
                if(p.Overrides.All(pair=>changes.Any(c=>c.Key==pair.Key && c.Value==pair.Value))) { key=p.BaseClip; return true; }
            }
            return false;
        }
    }
}
