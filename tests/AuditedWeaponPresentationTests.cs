using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Tylevo.FieldAttachments.Core;

internal static partial class Program
{
    private static McxInspectionEvent[] AuditedEvents(JsonElement profile) => profile.GetProperty("events").EnumerateArray().Select(e=>
    {
        var p=e.GetProperty("Parameter");
        var conditions=e.GetProperty("EventConditions").EnumerateArray().Select(c=>new McxInspectionCondition {
            Parameter=c.GetProperty("ParameterName").GetString()!,Type=c.GetProperty("ConditionParamType").GetInt32(),
            Mode=c.GetProperty("ConditionMode").GetInt32(),Integer=c.GetProperty("IntValue").GetInt32(),
            Float=c.GetProperty("FloatValue").GetSingle(),Boolean=c.GetProperty("BoolValue").GetInt32()!=0
        }).ToArray();
        return new McxInspectionEvent { Name=e.GetProperty("_functionName").GetString()!,Hash=e.GetProperty("_functionNameHash").GetInt32(),
            Time=e.GetProperty("_time").GetSingle(),Enabled=e.GetProperty("Enabled").GetInt32()!=0,
            ParameterType=p.GetProperty("ParamType").GetInt32(),Text=p.GetProperty("StringParam").GetString()!,
            Integer=p.GetProperty("IntParam").GetInt32(),Float=p.GetProperty("FloatParam").GetSingle(),Boolean=p.GetProperty("BoolParam").GetInt32()!=0,
            Conditions=conditions.Length,ConditionData=conditions };
    }).ToArray();

    private static void TestAuditedWeaponPresentations()
    {
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("all-presentations.json")!;
        using var document=JsonDocument.Parse(stream);
        var catalog=document.RootElement;
        var rows=catalog.GetProperty("recipients").EnumerateArray().ToArray();
        Check(rows.Length==153 && GeneratedWeaponPresentations.Entries.Length==rows.Length,"Broad batch includes exactly the 153 verified catalog recipients");
        Check(rows.Select(r=>r.GetProperty("template").GetString()).Distinct().Count()==rows.Length,"All catalog template identities are unique");
        foreach(var row in rows)
        {
            string template=row.GetProperty("template").GetString()!,bundle=row.GetProperty("bundle").GetString()!;
            var entry=AuditedWeaponPresentations.For(template)!;
            string clip="Tylevo.FieldAttachments."+bundle;
            Check(entry!=null && entry.Bundle==bundle && entry.Sha256==row.GetProperty("sha256").GetString(),"Compiled broad asset/hash matches export: "+template);
            Check(CustomStmInspection.Requested(true,template,false) && !CustomStmInspection.Requested(false,template,false) && !CustomStmInspection.Requested(true,template,true),"Broad recipient respects opt-in and native calibration: "+template);
            Check(CustomStmInspection.ValidClip(template,clip,CustomStmInspection.Duration,false,false,0) && CustomStmInspection.IsAuthoredClip(clip),"Owned fitted clip accepted: "+template);
            var foreign=rows.First(r=>r.GetProperty("bundle").GetString()!=bundle).GetProperty("bundle").GetString();
            Check(!CustomStmInspection.ValidClip(template,"Tylevo.FieldAttachments."+foreign,CustomStmInspection.Duration,false,false,0),"Different rig fit cannot substitute: "+template);
            var profiles=row.GetProperty("profiles").EnumerateArray().ToArray();
            Check(entry!.Profiles.Length==profiles.Length,"All native and replacement profiles retained: "+template);
            foreach(var p in profiles)
            {
                string controller=p.GetProperty("controller").GetString()!,native=p.GetProperty("clip").GetString()!;
                int state=p.TryGetProperty("state",out var stateValue)?stateValue.GetInt32():PoseMarker.SharedState;
                string stateName=p.TryGetProperty("stateName",out var nameValue)?nameValue.GetString()!:"Hands.LOOK";
                Check(CustomStmInspection.StateMatches(template,controller,state) && CustomStmInspection.StateName(template,controller,state)==stateName,"Exact native state accepted: "+template+" / "+stateName);
                Check(!CustomStmInspection.StateMatches(template,controller,1767024951) && !CustomStmInspection.StateMatches(template,controller+"changed",state),"Malfunction and foreign-controller states rejected: "+template);
                float duration=p.GetProperty("duration").GetSingle();
                Check(CustomStmInspection.ControllerMatches(template,controller) && CustomStmInspection.RecipientDenial(template,controller,native,duration,false,false,0)=="","Exact audited source metadata accepted: "+controller+" / "+native);
                foreach(string bad in new[]{"controller","clip","duration","nan","legacy","human","events"})
                    Check(CustomStmInspection.RecipientDenial(template,bad=="controller"?controller+" changed":controller,bad=="clip"?native+" changed":native,
                        bad=="duration"?duration+.1f:bad=="nan"?float.NaN:duration,bad=="legacy",bad=="human",bad=="events"?1:0)!="","Source guard rejects "+bad+": "+template);
                var events=AuditedEvents(p);
                Check(CustomStmInspection.EventDenial(template,native,events)=="" && CustomStmInspection.EventLimit(template,native)>=events.Length,"Native start and exact conditional audio signature accepted: "+template);
                foreach(string bad in new[]{"start","gameplay","time","hash","payload","enabled","conditionCount"})
                {
                    var changed=AuditedEvents(p);
                    if(bad=="start") changed=changed.Skip(1).ToArray();
                    if(bad=="gameplay") changed[1].Name="AddAmmoInChamber";
                    if(bad=="time") changed[1].Time=float.NaN;
                    if(bad=="hash") changed[1].Hash++;
                    if(bad=="payload") changed[1].Text+=" changed";
                    if(bad=="enabled") changed[0].Enabled=false;
                    if(bad=="conditionCount") changed[1].Conditions++;
                    Check(CustomStmInspection.EventDenial(template,native,changed)!="","Changed event rejected: "+bad+" / "+template);
                }
                if(events.Any(e=>e.Conditions!=0))
                    foreach(string bad in new[]{"parameter","type","mode","integer","float","boolean"})
                    {
                        var changed=AuditedEvents(p);var condition=changed.First(e=>e.Conditions>0).ConditionData[0];
                        if(bad=="parameter")condition.Parameter+=" changed";
                        if(bad=="type")condition.Type++;
                        if(bad=="mode")condition.Mode++;
                        if(bad=="integer")condition.Integer++;
                        if(bad=="float")condition.Float=float.NaN;
                        if(bad=="boolean")condition.Boolean=!condition.Boolean;
                        Check(CustomStmInspection.EventDenial(template,native,changed)!="","Conditional audio rejects changed "+bad+": "+template);
                    }
                string baseController=p.GetProperty("baseController").GetString()!;
                var pairs=p.GetProperty("overrides").EnumerateArray().Select(x=>new KeyValuePair<string,string>(x.GetProperty("key").GetString()!,x.GetProperty("value").GetString()!)).ToArray();
                bool isOverride=p.GetProperty("nativeOverride").GetBoolean();
                Check(AuditedWeaponPresentations.OverrideMatches(template,controller,baseController,native,pairs,out var key)==isOverride && (!isOverride || key==p.GetProperty("baseClip").GetString()),"Native override map and original inspect key pinned: "+template);
                if(isOverride)
                {
                    foreach(string bad in new[]{"base","controller","clip","missing","extra","duplicate","changed"})
                    {
                        if(pairs.Length==0 && (bad=="missing" || bad=="duplicate" || bad=="changed")) continue;
                        var changed=pairs.ToList();
                        if(bad=="missing")changed.RemoveAt(0);
                        if(bad=="extra")changed.Add(new KeyValuePair<string,string>("unknown","unknown"));
                        if(bad=="duplicate")changed.Add(changed[0]);
                        if(bad=="changed")changed[0]=new KeyValuePair<string,string>(changed[0].Key,changed[0].Value+" changed");
                        Check(!AuditedWeaponPresentations.OverrideMatches(template,bad=="controller"?controller+" changed":controller,bad=="base"?baseController+" changed":baseController,
                            bad=="clip"?native+" changed":native,changed,out _),"Variant override rejects "+bad+": "+template);
                    }
                }
            }
        }
        // The original manifest is historical evidence. Only these subsequently
        // verified handheld entries graduate from its fallback list in 0.25.2.
        var newlyVerified = new HashSet<string>(StringComparer.Ordinal) {
            "620109578d82e67e7911abf2", "676bf44c5539167c3603e869", "657857faeff4c850222dff1b",
            "624c0b3340357b5f566e8766", "62178be9d0050232da3485d9", "66d98233302686954b0c6f81",
            "675ea3d6312c0a5c4e04e317", "6217726288ed9f0845317459", "62178c4d4ecf221597654e3d",
            "66d9f1abb16d9aacf5068468"
        };
        foreach(var fallback in catalog.GetProperty("fallbacks").EnumerateObject())
        {
            bool verified = newlyVerified.Contains(fallback.Name);
            Check((AuditedWeaponPresentations.For(fallback.Name)!=null)==verified &&
                CustomStmInspection.Requested(true,fallback.Name,false)==verified,
                "Historical fallback changes only with explicit new coverage evidence: "+fallback.Name);
        }
        foreach(var preserved in catalog.GetProperty("preserved").EnumerateArray())
            Check(AuditedWeaponPresentations.For(preserved.GetString()!)==null && CustomStmInspection.Requested(true,preserved.GetString()!,false),"Accepted prior recipient preserved: "+preserved.GetString());
        foreach(string path in GlobalMcxInspection.RequiredRigPaths) {
            bool optional=path==CustomStmInspection.LeftHandMarker;
            Check((CustomStmInspection.MissingRigPath("5b3b713c5acfc4330140bd8d",p=>p!=path)=="")==optional,"TT Gold permits only its absent left marker: "+path);
            Check(CustomStmInspection.MissingRigPath("5447a9cd4bdc2dbd208b4567",p=>p!=path)!="","M4 rig still requires every original path: "+path);
        }
        Check(!CustomStmInspection.Requested(true,"unknown",false) && !AuditedWeaponPresentations.IsAuthoredClip(null),"No implicit global fallback enables unknown guns");
    }
}
