using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using Tylevo.FieldAttachments.Core;

internal static partial class Program
{
    private static List<McxInspectionEvent> LongRifleEvents(JsonElement weapon)
    {
        var events=new List<McxInspectionEvent>();
        foreach(var e in weapon.GetProperty("eventProfiles")[0].GetProperty("_animationEvents").EnumerateArray())
        {
            var p=e.GetProperty("Parameter");
            events.Add(new McxInspectionEvent {Name=e.GetProperty("_functionName").GetString()!,
                Hash=e.GetProperty("_functionNameHash").GetInt32(),Time=e.GetProperty("_time").GetSingle(),
                Enabled=e.GetProperty("Enabled").GetInt32()==1,Conditions=e.GetProperty("EventConditions").GetArrayLength(),
                Text=p.GetProperty("StringParam").GetString()!,ParameterType=p.GetProperty("ParamType").GetInt32(),
                Boolean=p.GetProperty("BoolParam").GetInt32()!=0,Float=p.GetProperty("FloatParam").GetSingle(),Integer=p.GetProperty("IntParam").GetInt32()});
        }
        return events;
    }
    private static void TestLongRifleMcxInspection()
    {
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("long-rifle-mcx.json")!;
        using var fixture=JsonDocument.Parse(stream);
        var weapons=fixture.RootElement.GetProperty("weapons");
        foreach(var gun in weapons.EnumerateObject())
        {
            var data=gun.Value;
            Check(data.GetProperty("requiredRigPaths").GetInt32()==55 && data.GetProperty("missingRigPaths").GetArrayLength()==0,
                "Installed asset audit contains the 55 shared MCX presentation paths: "+gun.Name);
            var clip=data.GetProperty("clips")[0];
            Check(data.GetProperty("clips").GetArrayLength()==1 && clip.GetProperty("name").GetString()!.EndsWith("_look") &&
                !clip.GetProperty("legacy").GetBoolean() && clip.GetProperty("events").GetArrayLength()==0 &&
                Math.Abs(clip.GetProperty("duration").GetSingle()-3.300001144f)<.05f,
                "Audited rifle clip fits existing type/duration/event gates without widening them: "+gun.Name);
            var events=LongRifleEvents(data);
            Check(GlobalMcxInspection.CompatibleEvents(events,.5243187f),"Actual installed inspection event fixture supports shared MCX entry: "+gun.Name);
            Check(data.GetProperty("states").GetArrayLength()==1 && data.GetProperty("states")[0].GetProperty("FullNameHash").GetInt32()==PoseMarker.SharedState,
                "Audited controller has the shared native inspection state: "+gun.Name);
            var originalTimes=events.ConvertAll(e=>e.Time).ToArray();
            GlobalMcxInspection.EventDenial(events,.5243187f);
            bool unchanged=true;
            for(int i=0;i<events.Count;i++) unchanged &= events[i].Time==originalTimes[i];
            Check(unchanged,"Compatibility validation does not retime native rifle events: "+gun.Name);
            foreach(string bad in new[] {"extra-gameplay","changed-gameplay","unknown-sound","duplicate-start","duplicate-sound","missing-hand-off","missing-hand-on","condition","disabled","hash","parameter-type","integer","float","boolean","nan","negative","past-end","reordered","same-time","hand-off-late","hand-on-early"})
            {
                var altered=LongRifleEvents(data);
                int off=altered.FindIndex(e=>e.Text=="HandOff"),on=altered.FindIndex(e=>e.Text=="HandOn");
                switch(bad)
                {
                    case "extra-gameplay": altered.Add(new McxInspectionEvent {Name="IdleStart",Time=.99f,Enabled=true}); break;
                    case "changed-gameplay": altered[off].Name="WeaponOut"; break;
                    case "unknown-sound": altered[off].Text="BoltAction"; break;
                    case "duplicate-start": altered[off]=altered[0]; break;
                    case "duplicate-sound": altered[on].Text="HandOff"; break;
                    case "missing-hand-off": altered.RemoveAt(off); break;
                    case "missing-hand-on": altered.RemoveAt(on); break;
                    case "condition": altered[off].Conditions=1; break;
                    case "disabled": altered[off].Enabled=false; break;
                    case "hash": altered[off].Hash=0; break;
                    case "parameter-type": altered[off].ParameterType=0; break;
                    case "integer": altered[off].Integer=1; break;
                    case "float": altered[off].Float=float.NaN; break;
                    case "boolean": altered[off].Boolean=true; break;
                    case "nan": altered[off].Time=float.NaN; break;
                    case "negative": altered[off].Time=-1; break;
                    case "past-end": altered[on].Time=1.01f; break;
                    case "reordered": altered.Reverse(); break;
                    case "same-time": altered[off].Time=altered[off-1].Time; break;
                    case "hand-off-late": altered[off].Time=.48f; break;
                    case "hand-on-early": altered[on].Time=.5f; break;
                }
                bool audioOnly=bad=="duplicate-sound" || bad=="disabled" ||
                    (bad=="missing-hand-off" || bad=="missing-hand-on") && altered.Count>=3 ||
                    bad=="hand-on-early" && gun.Name!="sr25" && gun.Name!="m1a" ||
                    bad=="hand-off-late" && gun.Name!="sr25" && gun.Name!="m1a";
                Check(GlobalMcxInspection.CompatibleEvents(altered,.5243187f)==audioOnly,
                    (audioOnly ? "Known audio variation preserves lifecycle checks: " : "Shared rifle event guard rejects ")+bad+": "+gun.Name);
            }
        }
        foreach(string gun in new[] {"sr25","m1a"})
        {
            var events=LongRifleEvents(weapons.GetProperty(gun));
            Check(!McxInspectionSegment.EventsMatch(events),"Native MCX validation remains exact-three; extended profile is donor-only: "+gun);
            foreach(string sound in new[] {"GunFlip","GunFlip1","GunFlip2"})
            {
                foreach(string bad in new[] {"function","hash","unknown-name","conditions","disabled","parameter","hold-overlap","duplicate"})
                {
                    var altered=LongRifleEvents(weapons.GetProperty(gun));
                    var flip=altered.Find(e=>e.Text==sound)!;
                    switch(bad)
                    {
                        case "function": flip.Name="SetMagazine"; break;
                        case "hash": flip.Hash=1134400241; break;
                        case "unknown-name": flip.Text="GunFlipUnknown"; break;
                        case "conditions": flip.Conditions=1; break;
                        case "disabled": flip.Enabled=false; break;
                        case "parameter": flip.ParameterType=0; break;
                        case "hold-overlap": flip.Time=.5f; altered.Sort((a,b)=>a.Time.CompareTo(b.Time)); break;
                        case "duplicate": altered.Find(e=>e.Text!=sound && e.Text.StartsWith("GunFlip"))!.Text=sound; break;
                    }
                    bool audioOnly=bad=="disabled" || bad=="hold-overlap" || bad=="duplicate";
                    Check(GlobalMcxInspection.CompatibleEvents(altered,.5243187f)==audioOnly,
                        (audioOnly ? "Optional audio may repeat/pause without changing native operation: " : "Optional inspection audio remains guarded: ")+gun+" "+sound+" "+bad);
                }
            }
        }
    }
}
