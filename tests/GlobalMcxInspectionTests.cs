using System;
using System.Collections.Generic;
using Tylevo.FieldAttachments.Core;

internal static partial class Program
{
    private static void TestGlobalMcxInspection()
    {
        var mcx=new PoseMarker { WeaponTemplate=McxInspectionSegment.Template,Controller=McxInspectionSegment.Controller+"/native-model",
            Layer=1,State=PoseMarker.SharedState,Time=.5243187f };
        var native=new PoseMarker { WeaponTemplate="m4",Controller="m4-controller",Layer=1,State=PoseMarker.SharedState,Time=.263399631f };
        string before=mcx.Encode(),nativeBefore=native.Encode();
        Check(ReferenceEquals(GlobalMcxInspection.ReferenceMarker(new[] {native,mcx}),mcx),"Global mode finds the real MCX calibration among other native markers");
        Check(GlobalMcxInspection.ReferenceMarker(new[] {native})==null,"No MCX marker means native fallback, never guessed donor timing");
        Check(GlobalMcxInspection.ReferenceMarker(new[] {mcx,mcx})==null,"Ambiguous donor calibration fails closed");
        foreach(string bad in new[] {"time","state","controller","layer"})
        {
            var marker=PoseMarker.Decode(before)!;
            if(bad=="time") marker.Time=.27f;
            if(bad=="state") marker.State=42;
            if(bad=="controller") marker.Controller="different/animator";
            if(bad=="layer") marker.Layer=0;
            Check(GlobalMcxInspection.ReferenceMarker(new[] {marker})==null,"Reject wrong donor calibration: "+bad);
        }
        foreach(string gun in new[] {"m4","rd704","mdr","ak","pistol","unknown-firearm"})
        {
            var marker=GlobalMcxInspection.Retarget(mcx,gun,gun+"-controller");
            Check(marker.Matches(gun,gun+"-controller",1,PoseMarker.SharedState) && marker.Time==mcx.Time,
                "Receiving marker has its own identity and exact MCX time without a template allowlist: "+gun);
        }
        Check(mcx.Encode()==before && native.Encode()==nativeBefore,"Retargeting never mutates MCX or receiving-gun native calibration");
        var rd=McxEvents(); rd[1].Time=.09509202093f; rd[2].Time=.8555377126f;
        Check(GlobalMcxInspection.CompatibleEvents(McxEvents(),mcx.Time),"Audited M4/MCX event profile permits the same forward entry");
        Check(GlobalMcxInspection.CompatibleEvents(rd,mcx.Time),"Audited RD sounds bracket the held MCX segment without gameplay event changes");
        Check(rd[1].Time==.09509202093f && rd[2].Time==.8555377126f,"Event validation does not rewrite target sound timings");
        foreach(string change in new[] {"extra","missing","order","start","off-after-seek","on-before-hold","on-at-hold","on-past-end","off-zero","nan","hash","conditions","disabled","function","text","parameter","boolean","integer","float"})
        {
            var events=McxEvents();
            switch(change)
            {
                case "extra": events.Add(new McxInspectionEvent {Name="WeaponOut"}); break;
                case "missing": events.RemoveAt(1); break;
                case "order": events.Reverse(); break;
                case "start": events[0].Time=.001f; break;
                case "off-after-seek": events[1].Time=.5f; break;
                case "on-before-hold": events[2].Time=.4f; break;
                case "on-at-hold": events[2].Time=mcx.Time; break;
                case "on-past-end": events[2].Time=1.1f; break;
                case "off-zero": events[1].Time=0; break;
                case "nan": events[1].Time=float.NaN; break;
                case "hash": events[0].Hash=0; break;
                case "conditions": events[2].Conditions=1; break;
                case "disabled": events[0].Enabled=false; break;
                case "function": events[1].Name="IdleStart"; break;
                case "text": events[1].Text="Fire"; break;
                case "parameter": events[1].ParameterType=0; break;
                case "boolean": events[0].Boolean=true; break;
                case "integer": events[0].Integer=1; break;
                case "float": events[0].Float=1; break;
            }
            bool cosmeticTiming=change=="off-after-seek" || change=="on-before-hold" || change=="on-at-hold";
            Check(GlobalMcxInspection.CompatibleEvents(events,mcx.Time)==cosmeticTiming,
                (cosmeticTiming ? "Verified audio timing is not an operation gate: " : "Unsafe target events refuse override/seek: ")+change);
        }
        foreach(float invalid in new[] {float.NaN,float.PositiveInfinity,.27f,.81f})
            Check(!GlobalMcxInspection.CompatibleEvents(McxEvents(),invalid),"Invalid donor hold cannot authorize target events: "+invalid);
        var rig=new HashSet<string>(GlobalMcxInspection.RequiredRigPaths);
        Check(rig.Count>=50 && GlobalMcxInspection.MissingRigPath(rig.Contains)=="","Audited shared presentation rig passes without gun-specific mechanism assumptions");
        foreach(string missing in GlobalMcxInspection.RequiredRigPaths)
        {
            rig.Remove(missing);
            Check(GlobalMcxInspection.MissingRigPath(rig.Contains)==missing,"Missing animated presentation bone rejects before mutation: "+missing);
            rig.Add(missing);
        }
        // Same controller survives MCX, native, MCX cycles; each request consumes one seek and restores its clip.
        var controller=new object(); var original=new object(); var donor=new object(); object current=original;
        var lease=new PoseBindingLease<object,object>(); int writes=0;
        for(int cycle=0;cycle<3;cycle++)
        {
            var entry=new McxInspectionSegment();
            lease.Acquire(controller,original,donor,()=>controller,()=>current,v=>{current=v;writes++;});
            Check(entry.TryIssue(.02f,mcx.Time,100,1,out _) && !entry.TryIssue(.03f,mcx.Time,101,1.01f,out _),"Global request seeks at most once: "+cycle);
            Check(entry.Observe(101,1.02f,.47f,ReferenceEquals(current,donor))==McxSeekResult.Confirmed,"Global hold follows confirmed donor state/time: "+cycle);
            Check(lease.TryRestore() && ReferenceEquals(current,original) && writes==(cycle+1)*2,"Return restores native clip once without swapping controller: "+cycle);
            var cleanup=new PoseCleanupCheck(); cleanup.Begin(200,42,true,2);
            Check(cleanup.Observe(201,true,true,42,false,false,2.01f)==PoseCleanupResult.Confirmed,"Native visible idle survives donor cleanup: "+cycle);
        }
    }
}
