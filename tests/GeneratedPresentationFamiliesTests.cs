using System;
using System.Reflection;
using System.Text.Json;
using Tylevo.FieldAttachments.Core;

internal static partial class Program
{
    private static void TestGeneratedPresentationFamilies()
    {
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("family-presentations.json")!;
        using var fixture=JsonDocument.Parse(stream);
        var rows=fixture.RootElement.GetProperty("recipients");
        Check(rows.GetArrayLength()==3,"First generated trial contains exactly three audited recipients");
        foreach(var row in rows.EnumerateArray())
        {
            string template=row.GetProperty("template").GetString()!;
            string controller=row.GetProperty("controller").GetString()!, native=row.GetProperty("nativeClip").GetString()!;
            string bundle=row.GetProperty("bundle").GetString()!, clip="Tylevo.FieldAttachments."+bundle;
            var entry=GeneratedPresentationFamilies.For(template);
            Check(entry!=null && entry.Controller==controller && entry.NativeClip==native && entry.Bundle==bundle &&
                entry.Sha256==row.GetProperty("sha256").GetString() && entry.Family==row.GetProperty("family").GetString(),
                "Compiled family routing/hash matches verified import: "+bundle);
            Check(CustomStmInspection.Requested(true,template,false) && !CustomStmInspection.Requested(false,template,false) &&
                !CustomStmInspection.Requested(true,template,true),"Generated recipients respect opt-in and native calibration");
            Check(CustomStmInspection.ControllerFor(template)==controller && CustomStmInspection.NativeClipFor(template)==native &&
                CustomStmInspection.BundleFor(template)==bundle && CustomStmInspection.IsAuthoredClip(clip),"Recipient resolves to its generated clip only");
            var verification=row.GetProperty("verification"); var source=verification.GetProperty("source");
            Check(source.GetProperty("useIdleGrip").GetBoolean() && source.GetProperty("nativeTime").GetSingle()==0 &&
                verification.GetProperty("samples").GetInt32()==401 && verification.GetProperty("nativeEndpointsMatch").GetBoolean() &&
                verification.GetProperty("maxGripError").GetSingle()<.002f && verification.GetProperty("heldAngleError").GetSingle()<.15f,
                "Editor receipt verifies automatic grip fit, common angle and native endpoints: "+bundle);
            var metadata=row.GetProperty("native");
            Check(metadata.GetProperty("states").GetArrayLength()==1 &&
                metadata.GetProperty("states")[0].GetProperty("FullNameHash").GetInt32()==PoseMarker.SharedState,
                "Audited native controller uses the required inspection state: "+bundle);
            foreach(var c in metadata.GetProperty("clips").EnumerateArray())
            {
                string name=c.GetProperty("name").GetString()!;
                Check((CustomStmInspection.RecipientDenial(template,controller,name,c.GetProperty("duration").GetSingle(),
                    c.GetProperty("legacy").GetBoolean(),false,c.GetProperty("events").GetArrayLength()).Length==0)==(name==native),
                    "Only audited native inspect selected, excluding alternate/jam clips: "+name);
            }
            var events=LongRifleEvents(metadata);
            Check(CustomStmInspection.EventDenial(template,native,events)=="","Actual local native start/audio events accepted: "+bundle);
            foreach(string bad in new[] {"start","gameplay","condition","nan","unknownSound"})
            {
                var changed=LongRifleEvents(metadata);
                if(bad=="start") changed.RemoveAt(0);
                if(bad=="gameplay") changed[1].Name="AddAmmoInChamber";
                if(bad=="condition") changed[1].Conditions=1;
                if(bad=="nan") changed[1].Time=float.NaN;
                if(bad=="unknownSound") changed[1].Text="unverified sound";
                Check(CustomStmInspection.EventDenial(template,native,changed).Length!=0,"Generated recipient preserves event gate: "+bad);
            }
            foreach(string bad in new[] {"controller","clip","duration","legacy","human","events","nan"})
                Check(CustomStmInspection.RecipientDenial(template,bad=="controller" ? controller+"(Clone)" : controller,
                    bad=="clip" ? native+"_copy" : native,bad=="duration" ? 4 : bad=="nan" ? float.NaN : CustomStmInspection.Duration,
                    bad=="legacy",bad=="human",bad=="events" ? 1 : 0).Length!=0,"Generated recipient preserves metadata gate: "+bad);
            Check(CustomStmInspection.ValidClip(template,clip,CustomStmInspection.Duration,false,false,0) &&
                !CustomStmInspection.ValidClip(template,clip,CustomStmInspection.Duration,true,false,0) &&
                !CustomStmInspection.ValidClip(template,clip,float.NaN,false,false,0) &&
                !CustomStmInspection.ValidClip(template,clip,CustomStmInspection.Duration,false,false,1),"Generated destination retains clip type/duration/event guards");
            foreach(var other in rows.EnumerateArray())
                Check(CustomStmInspection.ValidClip(template,"Tylevo.FieldAttachments."+other.GetProperty("bundle").GetString(),
                    CustomStmInspection.Duration,false,false,0)==(template==other.GetProperty("template").GetString()),"Cannot substitute another generated grip");
            foreach(string old in new[] {StmPresentation.Template,CustomStmInspection.M4Template,McxInspectionSegment.Template,
                CustomStmInspection.M700Template,CustomStmInspection.M870Template,CustomStmInspection.Glock17Template})
                Check(!CustomStmInspection.ValidClip(old,clip,CustomStmInspection.Duration,false,false,0) &&
                    !CustomStmInspection.ValidClip(template,CustomStmInspection.ClipFor(old),CustomStmInspection.Duration,false,false,0),"Existing/generated assets cannot cross recipients");
        }
        foreach(string unsupported in new[] {"unknown","5926bb2186f7744b1c6c6e61","5df8ce05b11454561e39243c","5cadc190ae921500103bb3b7"})
            Check(GeneratedPresentationFamilies.For(unsupported)==null && !CustomStmInspection.Requested(true,unsupported,false),
                "Category similarity never enables an unverified weapon: "+unsupported);
        Check(!GeneratedPresentationFamilies.IsAuthoredClip(null) && !GeneratedPresentationFamilies.IsAuthoredClip("") &&
            !GeneratedPresentationFamilies.IsAuthoredClip("mp5_look"),"Native/unknown clip cannot claim generated ownership");
    }
}
