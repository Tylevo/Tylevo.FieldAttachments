using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Tylevo.FieldAttachments.Core;

internal static partial class Program
{
    private static void TestCustomM4M870Backport()
    {
        // Fixtures read from the installed WTT containers' actual Hands.LOOK bindings.
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("m4-m870-backport.json")!;
        using var fixture=JsonDocument.Parse(stream);
        foreach(var item in fixture.RootElement.EnumerateObject())
        {
            var data=item.Value; var clip=data.GetProperty("clip");
            string template=data.GetProperty("template").GetString()!, controller=data.GetProperty("controller").GetString()!;
            string name=clip.GetProperty("name").GetString()!;
            float duration=clip.GetProperty("stop").GetSingle()-clip.GetProperty("start").GetSingle();
            var events=LongRifleEvents(data);
            Check(data.GetProperty("actualState").GetInt32()==PoseMarker.SharedState && data.GetProperty("layer").GetInt32()==1 &&
                data.GetProperty("storedBehaviourHash").GetInt32()!=PoseMarker.SharedState && data.GetProperty("behaviourBindingVerified").GetBoolean(),
                item.Name+": actual LOOK state binds the audited behaviour despite stale serialized hash");
            Check(CustomMcxBackport.Matches(template,name) && CustomMcxBackport.EventCountFor(template)==events.Count,
                item.Name+": larger event read is allowed only for the exact template/clip");
            Check(CustomStmInspection.RecipientDenial(template,controller,name,duration,clip.GetProperty("legacy").GetBoolean(),false,
                clip.GetProperty("events").GetArrayLength())=="" && CustomStmInspection.EventDenial(template,name,events)=="",
                item.Name+": actual clip metadata and complete native event profile accept authored motion");
            Check(GlobalMcxInspection.NativeEventDenial(events).Length!=0 && !McxInspectionSegment.EventsMatch(events),
                item.Name+": authored compatibility does not authorize native/global seek");
            Check(CustomStmInspection.ValidClip(template,CustomStmInspection.ClipFor(template),CustomStmInspection.Duration,false,false,0),
                item.Name+": the existing pinned destination clip and hold timeline remain valid");
            foreach(string bad in new[] {"template","controller","name","duration","legacy","human","events","nan"})
                Check(CustomStmInspection.RecipientDenial(bad=="template" ? CustomStmInspection.Glock17Template : template,
                    bad=="controller" ? McxInspectionSegment.Controller : controller,bad=="name" ? name+"_copy" : name,
                    bad=="duration" ? duration+.01f : bad=="nan" ? float.NaN : duration,bad=="legacy",bad=="human",bad=="events" ? 1 : 0).Length!=0,
                    item.Name+": compatibility does not waive "+bad);
            Check(CustomStmInspection.RecipientDenial(template,controller,name,CustomStmInspection.Duration,false,false,0).Length!=0 &&
                CustomStmInspection.RecipientDenial(template,controller,CustomStmInspection.NativeClipFor(template),duration,false,false,0).Length!=0,
                item.Name+": stock and WTT clip names cannot borrow each other's durations");
            Check(CustomStmInspection.RecipientDenial(template,controller,CustomStmInspection.NativeClipFor(template),CustomStmInspection.Duration,false,false,0)=="",
                item.Name+": original stock clip remains supported");
            foreach(var other in data.GetProperty("unboundLookClips").EnumerateArray())
                Check(!CustomMcxBackport.Matches(template,other.GetString()!) &&
                    CustomStmInspection.RecipientDenial(template,controller,other.GetString()!,duration,false,false,0).Length!=0,
                    item.Name+": unbound alternate/jam look clip stays excluded");
            foreach(string otherTemplate in new[] {StmPresentation.Template,McxInspectionSegment.Template,CustomStmInspection.M4Template,
                CustomStmInspection.M700Template,CustomStmInspection.M870Template,CustomStmInspection.Glock17Template,"unknown"})
            {
                if(otherTemplate==template) continue;
                Check(!CustomMcxBackport.Matches(otherTemplate,name) && CustomStmInspection.EventDenial(otherTemplate,name,events).Length!=0,
                    item.Name+": event exception cannot leak to another recipient "+otherTemplate);
            }
            for(int i=0;i<events.Count;i++)
            foreach(string bad in new[] {"name","hash","time","nan","text","type","disabled","conditional","bool","int","float"})
            {
                var changed=LongRifleEvents(data); var e=changed[i];
                switch(bad)
                {
                    case "name": e.Name="AddAmmoInChamber"; break;
                    case "hash": e.Hash++; break;
                    case "time": e.Time+=.001f; break;
                    case "nan": e.Time=float.NaN; break;
                    case "text": e.Text="BoltRelease"; break;
                    case "type": e.ParameterType++; break;
                    case "disabled": e.Enabled=false; break;
                    case "conditional": e.Conditions=1; break;
                    case "bool": e.Boolean=true; break;
                    case "int": e.Integer=1; break;
                    case "float": e.Float=float.NaN; break;
                }
                Check(CustomStmInspection.EventDenial(template,name,changed).Length!=0,item.Name+": changed event rejected row "+i+" "+bad);
            }
            var before=events.Select(e=>(e.Name,e.Hash,e.Time,e.Text,e.ParameterType,e.Enabled,e.Conditions,e.Boolean,e.Integer,e.Float)).ToArray();
            CustomStmInspection.EventDenial(template,name,events);
            Check(before.SequenceEqual(events.Select(e=>(e.Name,e.Hash,e.Time,e.Text,e.ParameterType,e.Enabled,e.Conditions,e.Boolean,e.Integer,e.Float))),
                item.Name+": compatibility does not rewrite native events");
            events.RemoveAt(0);
            Check(CustomStmInspection.EventDenial(template,name,events).Length!=0,item.Name+": missing native start rejected");
            events=LongRifleEvents(data); events.Add(Sound(.98f,"HandOn"));
            Check(CustomStmInspection.EventDenial(template,name,events).Length!=0,item.Name+": additional event rejected");
            events=LongRifleEvents(data); events.Reverse();
            Check(CustomStmInspection.EventDenial(template,name,events).Length!=0,item.Name+": reordered events rejected");
            var otherProfile=fixture.RootElement.GetProperty(item.Name=="m4a1" ? "m870" : "m4a1");
            Check(CustomStmInspection.EventDenial(template,name,LongRifleEvents(otherProfile)).Length!=0,item.Name+": profiles cannot be mixed");
        }
    }
}
