using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Tylevo.FieldAttachments.Core;

internal static partial class Program
{
    private static void TestCategoryMcxInspection()
    {
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("category-mcx.json")!;
        using var fixture=JsonDocument.Parse(stream);
        var weapons=fixture.RootElement.GetProperty("weapons");
        foreach(var gun in weapons.EnumerateObject())
        {
            var data=gun.Value;
            Check(data.GetProperty("requiredRigPaths").GetInt32()==55 && data.GetProperty("missingRigPaths").GetArrayLength()==0,
                "Audited category model contains shared presentation rig: "+gun.Name);
            var clip=data.GetProperty("clips").EnumerateArray().Single(c=>c.GetProperty("name").GetString()==data.GetProperty("selectedClip").GetString());
            Check(!clip.GetProperty("legacy").GetBoolean() && clip.GetProperty("events").GetArrayLength()==0 &&
                Math.Abs(clip.GetProperty("duration").GetSingle()-3.300001144f)<.05f,
                "Selected normal inspection clip fits unchanged MCX duration gate: "+gun.Name);
            foreach(var jam in data.GetProperty("clips").EnumerateArray().Where(c=>c.GetProperty("name").GetString()!.Contains("_jam_")))
                Check(Math.Abs(jam.GetProperty("duration").GetSingle()-3.300001144f)>.05f,
                    "Unrelated malfunction-look clip cannot pass the duration gate: "+gun.Name);
            Check(data.GetProperty("states").GetArrayLength()==1 && data.GetProperty("states")[0].GetProperty("FullNameHash").GetInt32()==PoseMarker.SharedState,
                "Category native controller uses the same Hands inspection state: "+gun.Name);
            var events=LongRifleEvents(data);
            Check(GlobalMcxInspection.EventDenial(events,.5243187f)=="","Actual category event profile permits shared MCX presentation: "+gun.Name);
            var before=events.Select(e=>(e.Name,e.Hash,e.Text,e.Time,e.Enabled,e.ParameterType,e.Conditions,e.Boolean,e.Integer,e.Float)).ToArray();
            GlobalMcxInspection.CompatibleEvents(events,.5243187f);
            Check(before.SequenceEqual(events.Select(e=>(e.Name,e.Hash,e.Text,e.Time,e.Enabled,e.ParameterType,e.Conditions,e.Boolean,e.Integer,e.Float))),
                "Validation preserves every native event value including disabled/repeated sounds: "+gun.Name);
            foreach(string bad in new[] {"disabled-start","late-start","wrong-start-hash","extra-start","unknown-sound","disabled-gameplay","disabled-unknown-sound","conditional","sound-hash","sound-parameter","extra-number","nan","outside-time","same-time","reversed","over-bound"})
            {
                var altered=LongRifleEvents(data);
                switch(bad)
                {
                    case "disabled-start": altered[0].Enabled=false; break;
                    case "late-start": altered[0].Time=.001f; break;
                    case "wrong-start-hash": altered[0].Hash=1554795451; break;
                    case "extra-start": altered[1].Name="StartUtilityOperation"; altered[1].Hash=1134400241; altered[1].ParameterType=0; break;
                    case "unknown-sound": altered[1].Text="BoltRelease"; break;
                    case "disabled-gameplay": altered[1].Enabled=false; altered[1].Name="SetMagazine"; break;
                    case "disabled-unknown-sound": altered[1].Enabled=false; altered[1].Text="BoltRelease"; break;
                    case "conditional": altered[1].Conditions=1; break;
                    case "sound-hash": altered[1].Hash=0; break;
                    case "sound-parameter": altered[1].ParameterType=0; break;
                    case "extra-number": altered[1].Integer=1; break;
                    case "nan": altered[1].Time=float.NaN; break;
                    case "outside-time": altered[altered.Count-1].Time=1.01f; break;
                    case "same-time": altered[2].Time=altered[1].Time; break;
                    case "reversed": altered.Reverse(); break;
                    case "over-bound": while(altered.Count<=GlobalMcxInspection.MaximumEvents) altered.Add(altered[altered.Count-1]); break;
                }
                Check(!GlobalMcxInspection.CompatibleEvents(altered,.5243187f),"Category profile rejects "+bad+": "+gun.Name);
            }
        }
        var pistol=LongRifleEvents(weapons.GetProperty("glock17"));
        Check(pistol.Count(e=>e.Text=="HandOn")>1 && pistol.Any(e=>!e.Enabled) && GlobalMcxInspection.CompatibleEvents(pistol,.5243187f),
            "Glock's repeated and disabled hand sounds are audio, not native completion events");
        Check(!McxInspectionSegment.EventsMatch(pistol),"Native MCX exact event signature is not widened for category profiles");
        var shotgun=LongRifleEvents(weapons.GetProperty("m870"));
        Check(shotgun.Any(e=>e.Text=="HandOff" && e.Time>.4643187f) && GlobalMcxInspection.CompatibleEvents(shotgun,.5243187f),
            "Shotgun HandOff after entry does not misclassify an audio cue as a hands-state change");
        var bullpup=LongRifleEvents(weapons.GetProperty("aug"));
        Check(bullpup.All(e=>e.Text!="HandOff") && GlobalMcxInspection.CompatibleEvents(bullpup,.5243187f),
            "AUG need not invent an absent HandOff sound to use the same MCX presentation");
        var rpk=LongRifleEvents(weapons.GetProperty("rpk16"));
        Check(rpk.Any(e=>e.Text=="HandOnHard" && e.Time>.4643187f && e.Time<.5243187f) && GlobalMcxInspection.CompatibleEvents(rpk,.5243187f),
            "RPK inspection audio can occur during lead-in without changing the saved pose or native completion");
    }
}
