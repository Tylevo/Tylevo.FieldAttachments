using System;
using System.Collections.Generic;
using Tylevo.FieldAttachments.Core;

internal static partial class Program
{
    private static List<McxInspectionEvent> McxEvents() => new List<McxInspectionEvent> {
        new McxInspectionEvent { Name="StartUtilityOperation", Hash=1134400241, Enabled=true },
        new McxInspectionEvent { Name="Sound", Hash=1554795451, Text="HandOff", Time=0.0407191962f, Enabled=true, ParameterType=3 },
        new McxInspectionEvent { Name="Sound", Hash=1554795451, Text="HandOn", Time=0.9338974357f, Enabled=true, ParameterType=3 }
    };
    private static void TestMcxInspectionSegment()
    {
        const string controller="weapon_sig_mcx_gen1_762x35/weapon_sig_mcx_gen1_762x35_model.generated(Clone)";
        var saved = new PoseMarker { WeaponTemplate=McxInspectionSegment.Template, Controller=controller,
            Layer=1, State=1355507738, Time=0.5243187f };
        Check(McxInspectionSegment.MarkerDenial(saved,saved.WeaponTemplate,controller)=="", "MCX accepts the user's recorded late native marker");
        Check(McxInspectionSegment.MarkerDenial(null,saved.WeaponTemplate,controller)!="", "MCX refuses to guess a cut without a saved marker");
        Check(McxInspectionSegment.MarkerDenial(saved,"other weapon",controller)!="", "MCX segment cannot become an unverified donor on other guns");
        Check(McxInspectionSegment.MarkerDenial(saved,saved.WeaponTemplate,"other controller")!="", "MCX marker binds the actual controller identity");
        foreach (float time in new[] {0.27f,0.449f,0.801f,float.NaN,float.PositiveInfinity})
        { saved.Time=time; Check(McxInspectionSegment.MarkerDenial(saved,saved.WeaponTemplate,controller)!="", "MCX rejects non-late or invalid marker "+time); }
        saved.Time=0.5243187f; saved.Layer=0;
        Check(McxInspectionSegment.MarkerDenial(saved,saved.WeaponTemplate,controller)!="", "MCX requires the recorded Hands layer");
        saved.Layer=1; saved.State=42;
        Check(McxInspectionSegment.MarkerDenial(saved,saved.WeaponTemplate,controller)!="", "MCX rejects a different animation state");
        Check(McxInspectionSegment.EventsMatch(McxEvents()), "MCX native asset's exact three-event profile is accepted");
        foreach (string change in new[] {"extra","removed","name","time","hash","parameter","text","condition","disabled","boolean","integer","float","nan","order"})
        {
            var events=McxEvents();
            switch(change)
            {
                case "extra": events.Add(new McxInspectionEvent {Name="IdleStart"}); break;
                case "removed": events.RemoveAt(2); break;
                case "name": events[1].Name="Fire"; break;
                case "time": events[2].Time=0.5f; break;
                case "hash": events[0].Hash=0; break;
                case "parameter": events[1].ParameterType=0; break;
                case "text": events[1].Text="Other"; break;
                case "condition": events[0].Conditions=1; break;
                case "disabled": events[0].Enabled=false; break;
                case "boolean": events[0].Boolean=true; break;
                case "integer": events[0].Integer=1; break;
                case "float": events[0].Float=1; break;
                case "nan": events[0].Time=float.NaN; break;
                case "order": events.Reverse(); break;
            }
            Check(!McxInspectionSegment.EventsMatch(events), "MCX changed native events fail closed: "+change);
        }
        var request = new McxInspectionSegment();
        Check(request.TryIssue(0.01f,0.5243187f,100,1,out _) && request.Consumed && request.Pending && Math.Abs(request.Entry-0.4643187f)<0.00001f,
            "MCX consumes one entry request before seeking and keeps a short lead-in to the exact hold");
        Check(!request.TryIssue(0.02f,0.5243187f,100,1,out _), "Duplicate MCX entry cannot seek again");
        Check(request.Observe(100,1,0.4643187f,true)==McxSeekResult.Pending, "MCX cannot pause before next-frame evaluation");
        Check(request.Observe(101,1.02f,0.47f,true)==McxSeekResult.Confirmed && !request.Pending, "MCX requires matching post-seek state/time before hold");
        request.Cancel();
        Check(!request.TryIssue(0.01f,0.5243187f,102,1.04f,out _), "Cancel/completion never rearms the same MCX entry");
        foreach (float time in new[] {0f,-1f,0.16f,float.NaN})
        { request.Reset(); Check(!request.TryIssue(time,0.5243187f,200,2,out _) && !request.Consumed,"Late/invalid entry observation causes no seek: "+time); }
        foreach (string failure in new[] {"state","before","overshoot","nan","timeout","clock"})
        {
            request.Reset(); request.TryIssue(0.01f,0.5243187f,300,3,out _);
            float time=failure=="before"?0.01f:failure=="overshoot"?0.9f:failure=="nan"?float.NaN:0.47f;
            float now=failure=="timeout"?3.6f:failure=="clock"?2.9f:3.01f;
            Check(request.Observe(301,now,time,failure!="state")==McxSeekResult.Fault && request.Consumed && !request.Pending,
                "Uncertain MCX seek is consumed without retry: "+failure);
        }
    }
}
