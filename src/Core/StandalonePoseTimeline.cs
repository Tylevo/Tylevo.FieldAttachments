using System;
using System.Collections.Generic;

namespace Tylevo.FieldAttachments.Core
{
    // Owns only a sampled clip time and visual weight. The caller owns rendering
    // and restoration; no native animation or gameplay operation is involved.
    public sealed class StandalonePoseTimeline
    {
        private const float Duration = 3.333333f;
        private const float HoldSeconds = .22f * Duration, EndSeconds = .45f * Duration;
        private const float EntryBlendSeconds = .12f, ReturnBlendSeconds = .15f, MaximumSeconds = 120f;
        private enum Phase { None, Entering, Held, Returning, EarlyClose }
        private Phase _phase;
        private float _startedAt, _lastAt, _returnAt, _returnSample, _returnBlend;

        public bool Active => _phase != Phase.None;
        public bool Held => _phase == Phase.Held;
        public bool Returning => _phase == Phase.Returning || _phase == Phase.EarlyClose;
        public float SampleTime { get; private set; }
        public float Blend { get; private set; }
        public string Fault { get; private set; } = "";

        public void Start(float now)
        {
            Abort(); Fault = "";
            if (!ValidClock(now)) { Fail("invalid standalone clock"); return; }
            _startedAt = _lastAt = now;
            _phase = Phase.Entering;
        }

        public void Tick(float now)
        {
            if (!Active) return;
            if (!ValidClock(now) || now < _lastAt)
            { Fail("invalid or backwards standalone clock"); return; }
            _lastAt = now;
            double elapsed = (double)now - _startedAt;
            if (elapsed >= MaximumSeconds) { Fail("120-second standalone pose limit"); return; }
            if (_phase == Phase.Entering)
            {
                SampleTime = Math.Min((float)elapsed, HoldSeconds);
                Blend = Smooth((float)elapsed / EntryBlendSeconds);
                if (elapsed >= HoldSeconds) _phase = Phase.Held;
            }
            else if (_phase == Phase.Returning)
            {
                SampleTime = Math.Min(EndSeconds, _returnSample + (float)((double)now - _returnAt));
                Blend = Smooth((EndSeconds - SampleTime) / ReturnBlendSeconds);
                if (SampleTime >= EndSeconds) End();
            }
            else if (_phase == Phase.EarlyClose)
            {
                float returned = (float)((double)now - _returnAt);
                Blend = _returnBlend * (1 - Smooth(returned / ReturnBlendSeconds));
                if (returned >= ReturnBlendSeconds) End();
            }
        }

        public void Close(float now)
        {
            Tick(now);
            if (!Active || Returning) return;
            _returnAt = now; _returnSample = SampleTime; _returnBlend = Blend;
            _phase = Held ? Phase.Returning : Phase.EarlyClose;
        }

        public void Abort()
        {
            End(); SampleTime = 0;
            _startedAt = _lastAt = _returnAt = _returnSample = _returnBlend = 0;
        }

        private void End() { _phase = Phase.None; Blend = 0; }
        private void Fail(string reason) { Abort(); Fault = reason; }
        private static bool ValidClock(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0;
        private static float Smooth(float value)
        {
            float t = Math.Max(0, Math.Min(1, value));
            return t * t * (3 - 2 * t);
        }
    }

    public static class StandalonePoseInputPolicy
    {
        // Called only after pointer capture has allowed the native command.
        // Exact installed ECommand names; no physical-key or prefix guesses.
        private static readonly HashSet<string> Interrupting = new HashSet<string>(StringComparer.Ordinal) {
            "ToggleShooting", "ToggleAlternativeShooting",
            "ReloadWeapon", "QuickReloadWeapon", "NextMagazine", "PreviousMagazine",
            "ExamineWeapon", "CheckAmmo", "CheckChamber", "ChamberUnload", "UnloadMagazine",
            "SelectKnife", "SelectFirstPrimaryWeapon", "SelectSecondPrimaryWeapon", "SelectSecondaryWeapon",
            "QuickSelectSecondaryWeapon", "QuickKnifeKick", "ThrowGrenade", "PressThrowGrenade",
            "NextGrenadeStage", "TryHighThrow", "TryLowThrow", "BeginInteracting", "BeginSpecialInteracting",
            "ToggleSprinting", "Jump", "Vaulting", "ToggleInventory", "Escape", "ShowConsole", "F12",
            "FoldStock", "ChangePointOfView", "LeftStanceToggle", "SetLeftStance",
            "ToggleAnimBlindFireAbove", "ToggleAnimBlindFireRight", "ToggleBlindAbove", "ToggleBlindRight",
            "WeaponMounting", "ToggleBipods", "ThrowItem", "DropBackpack",
            "SelectFastSlot0", "SelectFastSlot4", "SelectFastSlot5", "SelectFastSlot6", "SelectFastSlot7", "SelectFastSlot8", "SelectFastSlot9",
            "PressSlot0", "PressSlot4", "PressSlot5", "PressSlot6", "PressSlot7", "PressSlot8", "PressSlot9"
        };

        public static bool ShouldInterrupt(string command) => Interrupting.Contains(command);
    }
}
