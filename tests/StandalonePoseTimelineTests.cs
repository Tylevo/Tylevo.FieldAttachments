using System;
using Tylevo.FieldAttachments.Core;

internal static partial class Program
{
    private static void TestStandalonePoseTimeline()
    {
        var timeline = new StandalonePoseTimeline();
        Check(!timeline.Active && !timeline.Held && !timeline.Returning && timeline.Blend == 0 && timeline.Fault == "",
            "Standalone pose begins inactive with no visual weight");
        timeline.Start(0);
        Check(timeline.Active && !timeline.Held && !timeline.Returning && timeline.SampleTime == 0 && timeline.Blend == 0,
            "Standalone start owns a zero-weight entry without inspecting a native animator");
        timeline.Tick(.06f);
        Check(StandaloneNear(timeline.SampleTime, .06f) && StandaloneNear(timeline.Blend, .5f),
            "Standalone entry samples elapsed seconds with a 120ms blend-in");
        timeline.Tick(.12f);
        Check(StandaloneNear(timeline.SampleTime, .12f) && timeline.Blend == 1 && !timeline.Held,
            "Standalone blend reaches full weight before the authored hold");
        timeline.Tick(.734f);
        Check(timeline.Held && !timeline.Returning && StandaloneNear(timeline.SampleTime, .7333333f) && timeline.Blend == 1,
            "Standalone timeline holds the authored 0.22 frame independently of native inspection");
        timeline.Tick(40);
        Check(timeline.Held && StandaloneNear(timeline.SampleTime, .7333333f) && timeline.Blend == 1,
            "Standalone hold does not advance the clip while time continues");
        timeline.Close(40);
        Check(timeline.Active && timeline.Returning && !timeline.Held && StandaloneNear(timeline.SampleTime, .7333333f),
            "Normal close starts the authored exit at the held sample");
        timeline.Tick(40.6f);
        Check(timeline.Returning && StandaloneNear(timeline.SampleTime, 1.3333333f) && timeline.Blend == 1,
            "Normal close advances the authored exit before fading its last 150ms");
        timeline.Tick(40.691667f);
        Check(StandaloneNear(timeline.SampleTime, 1.425f) && StandaloneNear(timeline.Blend, .5f),
            "Standalone authored exit blends halfway back to native in its final 150ms");
        timeline.Tick(40.8f);
        Check(!timeline.Active && !timeline.Held && !timeline.Returning && timeline.Blend == 0 && StandaloneNear(timeline.SampleTime, 1.5f),
            "Standalone authored exit ends at 1.5 seconds with no remaining visual weight");

        timeline.Start(0);
        timeline.Close(.06f); // Close must observe the clock even without an earlier Tick.
        Check(timeline.Returning && StandaloneNear(timeline.SampleTime, .06f) && StandaloneNear(timeline.Blend, .5f),
            "Early close first updates entry then freezes the current sample and blend");
        timeline.Tick(.135f);
        Check(timeline.Returning && StandaloneNear(timeline.SampleTime, .06f) && StandaloneNear(timeline.Blend, .25f),
            "Early close fades the existing weight without jumping to the authored outro");
        timeline.Close(.15f);
        timeline.Tick(.22f);
        Check(!timeline.Active && timeline.Blend == 0 && StandaloneNear(timeline.SampleTime, .06f),
            "Repeated early close does not restart its original 150ms return");

        timeline.Start(0);
        timeline.Close(.5f);
        timeline.Tick(.575f);
        Check(StandaloneNear(timeline.SampleTime, .5f) && StandaloneNear(timeline.Blend, .5f),
            "Late entry close still freezes and fades instead of seeking the held pose");
        timeline.Tick(.66f);
        Check(!timeline.Active && timeline.Fault == "", "Early close completes without a native-ready event");
        timeline.Start(0);
        timeline.Close(0);
        timeline.Tick(.16f);
        Check(!timeline.Active && timeline.Blend == 0 && timeline.SampleTime == 0,
            "Same-frame open and close never introduces visual weight");

        timeline.Start(0);
        timeline.Close(1);
        timeline.Close(1.4f);
        timeline.Tick(1.8f);
        Check(!timeline.Active && timeline.Fault == "" && StandaloneNear(timeline.SampleTime, 1.5f),
            "Close observes a newly reached hold and repeated close does not extend the authored return");
        timeline.Tick(2);
        timeline.Close(2);
        timeline.Abort(); timeline.Abort();
        Check(!timeline.Active && !timeline.Returning && !timeline.Held && timeline.Blend == 0 && timeline.SampleTime == 0,
            "Repeated standalone cleanup is inactive and removes the sampled pose");

        foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1f })
        {
            timeline.Start(invalid);
            Check(!timeline.Active && timeline.Fault.Length != 0 && timeline.Blend == 0,
                "Standalone start rejects invalid clock " + invalid);
            timeline.Start(1); timeline.Tick(invalid);
            Check(!timeline.Active && timeline.Fault.Length != 0 && timeline.Blend == 0,
                "Standalone tick rejects invalid clock " + invalid);
            timeline.Start(1); timeline.Close(invalid);
            Check(!timeline.Active && timeline.Fault.Length != 0 && timeline.Blend == 0,
                "Standalone close rejects invalid clock before changing return state " + invalid);
        }
        timeline.Start(5); timeline.Tick(5.1f); timeline.Tick(5.05f);
        Check(!timeline.Active && timeline.Fault.Length != 0 && timeline.Blend == 0,
            "Standalone entry faults on a backwards clock");
        timeline.Start(5); timeline.Tick(6); timeline.Close(5.9f);
        Check(!timeline.Active && timeline.Fault.Length != 0, "Standalone held close faults on a backwards clock");
        timeline.Start(5); timeline.Close(6); timeline.Tick(6.1f); timeline.Tick(6.05f);
        Check(!timeline.Active && timeline.Fault.Length != 0, "Standalone return faults on a backwards clock");
        string fault = timeline.Fault;
        timeline.Abort();
        Check(timeline.Fault == fault, "Standalone cleanup retains the first fault for reporting");
        timeline.Start(3); timeline.Tick(3.1f); timeline.Tick(3.1f);
        Check(timeline.Active && timeline.Fault == "" && StandaloneNear(timeline.SampleTime, .1f),
            "Explicit standalone restart clears faults and identical clock samples do not fault");
        timeline.Start(4);
        Check(timeline.Active && timeline.SampleTime == 0 && timeline.Blend == 0 && !timeline.Returning,
            "Explicit standalone start resets a previous active timeline");
        timeline.Abort();
        Check(!timeline.Active && timeline.Blend == 0, "Gameplay abort releases entry immediately");
        timeline.Start(0); timeline.Tick(1); timeline.Abort();
        Check(!timeline.Active && timeline.Blend == 0, "Gameplay abort releases hold immediately");
        timeline.Start(0); timeline.Close(1); timeline.Abort();
        Check(!timeline.Active && timeline.Blend == 0, "Gameplay abort releases the authored return immediately");

        timeline.Start(0); timeline.Tick(119.99f);
        Check(timeline.Held && timeline.Fault == "", "Standalone hold remains available before the bounded session deadline");
        timeline.Tick(120);
        Check(!timeline.Active && timeline.Fault.Length != 0 && timeline.Blend == 0,
            "Standalone hold times out at 120 seconds without a native cancellation call");
        timeline.Start(0); timeline.Close(119.99f); timeline.Tick(120);
        Check(!timeline.Active && timeline.Fault.Length != 0 && timeline.Blend == 0,
            "Normal close cannot extend the total standalone session deadline");

        // Sweep both early and held close times at two absolute clock origins.
        foreach (float origin in new[] { 0f, 1000f })
        foreach (float closeAfter in new[] { .02f, .4f, .72f, .8f, 2f })
        {
            timeline.Start(origin);
            bool closed = false, bounded = true, wasReturning = false;
            float previousSample = 0, previousBlend = 1;
            for (int step = 0; step <= 140; step++)
            {
                float offset = step * .025f;
                timeline.Tick(origin + offset);
                if (!closed && offset >= closeAfter) { timeline.Close(origin + offset); closed = true; }
                bounded &= !float.IsNaN(timeline.SampleTime) && !float.IsInfinity(timeline.SampleTime) &&
                    timeline.SampleTime >= 0 && timeline.SampleTime <= 1.5001f &&
                    timeline.Blend >= 0 && timeline.Blend <= 1 && timeline.Fault == "";
                if (timeline.Returning && wasReturning)
                    bounded &= timeline.SampleTime >= previousSample && timeline.Blend <= previousBlend + .0001f;
                previousSample = timeline.SampleTime; previousBlend = timeline.Blend; wasReturning = timeline.Returning;
            }
            Check(bounded && !timeline.Active && timeline.Blend == 0,
                "Standalone entry/hold/return stay finite and bounded at origin " + origin + " close " + closeAfter);
        }

        foreach (string command in new[] { "ToggleAlternativeShooting", "ToggleShooting",
            "ReloadWeapon", "QuickReloadWeapon", "NextMagazine", "PreviousMagazine", "ExamineWeapon", "CheckAmmo",
            "CheckChamber", "ChamberUnload", "UnloadMagazine", "SelectKnife", "SelectFirstPrimaryWeapon",
            "SelectSecondPrimaryWeapon", "SelectSecondaryWeapon", "QuickSelectSecondaryWeapon", "QuickKnifeKick",
            "ThrowGrenade", "PressThrowGrenade", "NextGrenadeStage", "TryHighThrow", "TryLowThrow", "BeginInteracting",
            "BeginSpecialInteracting", "ToggleSprinting", "Jump", "Vaulting", "ToggleInventory", "Escape", "ShowConsole",
            "F12", "FoldStock", "ChangePointOfView", "LeftStanceToggle", "SetLeftStance", "ToggleAnimBlindFireAbove",
            "ToggleAnimBlindFireRight", "ToggleBlindAbove", "ToggleBlindRight", "WeaponMounting", "ToggleBipods",
            "ThrowItem", "DropBackpack" })
            Check(StandalonePoseInputPolicy.ShouldInterrupt(command), "Mapped gameplay command releases standalone pose: " + command);
        foreach (string slot in new[] { "0", "4", "5", "6", "7", "8", "9" })
            Check(StandalonePoseInputPolicy.ShouldInterrupt("SelectFastSlot" + slot) && StandalonePoseInputPolicy.ShouldInterrupt("PressSlot" + slot),
                "Exact mapped quick-slot commands release standalone pose: " + slot);
        foreach (string command in new[] { "None", "F10", "EndShooting", "EndAlternativeShooting", "ToggleTalk", "StopTalk", "ToggleVoip", "EndSprinting",
            "ToggleWalk", "EndWalk", "ToggleSpeed", "", "ToggleAim", "Aim", "R", "Mouse1", "togglealternativeshooting", "PressSlot1", "SelectFastSlot3" })
            Check(!StandalonePoseInputPolicy.ShouldInterrupt(command), "Benign or unverified command does not release standalone pose: " + command);

        foreach (string command in new[] { "ToggleAlternativeShooting", "ToggleShooting" })
        {
            timeline.Start(0); timeline.Tick(.1f);
            bool blocked = PointerInputPolicy.Block(command, true, true);
            if (!blocked && StandalonePoseInputPolicy.ShouldInterrupt(command)) timeline.Abort();
            Check(blocked && timeline.Active, "Pointer capture blocks shoot/ADS begin before pose interruption: " + command);
            timeline.Start(0); timeline.Tick(.1f);
            blocked = PointerInputPolicy.Block(command, false, false);
            if (!blocked && StandalonePoseInputPolicy.ShouldInterrupt(command)) timeline.Abort();
            Check(!blocked && !timeline.Active, "Uncaptured mapped shoot/ADS begin aborts immediately before native passthrough: " + command);
        }
        foreach (bool captured in new[] { false, true })
        {
            timeline.Start(0); timeline.Tick(1);
            bool blocked = PointerInputPolicy.Block("EndAlternativeShooting", captured, captured);
            if (!blocked && StandalonePoseInputPolicy.ShouldInterrupt("EndAlternativeShooting")) timeline.Abort();
            Check(!blocked && timeline.Held,
                "Native ADS release passes through without closing the Alt menu or standalone pose: captured=" + captured);
        }
        timeline.Start(0); timeline.Tick(1);
        bool mouseDrain = PointerInputPolicy.Block("ToggleAlternativeShooting", false, true);
        if (!mouseDrain && StandalonePoseInputPolicy.ShouldInterrupt("ToggleAlternativeShooting")) timeline.Abort();
        Check(mouseDrain && timeline.Held, "Held Alt-click drain cannot leak ADS or cancel the standalone pose");
    }

    private static bool StandaloneNear(float actual, float expected) => Math.Abs(actual - expected) < .0001f;
}
