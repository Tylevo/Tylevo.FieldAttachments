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
        private const float EntrySeconds = .48f;
        private const float FastReturnSeconds = .36f;
        private const float EntryBlendSeconds = .12f, ReturnBlendSeconds = .15f, MaximumSeconds = 120f;
        private const float SynchronizedEntryLimit = 3f;
        private enum Phase { None, Entering, Held, Returning, EarlyClose }
        private Phase _phase;
        private float _startedAt, _lastAt, _returnAt, _returnSample, _returnBlend;
        private bool _fastReturn;
        private bool _synchronized;
        private float _entrySeconds, _synchronizedReturnSeconds, _earlyReturnSeconds;

        public bool FasterReturn { get; set; }
        public bool Synchronized => _synchronized;
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

        public void StartSynchronized(float now, float entrySeconds, float returnSeconds)
        {
            Start(now);
            if (!Active) return;
            if (!ValidDuration(entrySeconds) || !ValidDuration(returnSeconds))
            { Fail("invalid synchronized pose duration"); return; }
            _synchronized = true;
            _entrySeconds = entrySeconds;
            _synchronizedReturnSeconds = returnSeconds;
        }

        // A clock sample alone cannot certify that a foreign stance has settled.
        public void Tick(float now) => Tick(now, false);

        public void Tick(float now, bool entryReady)
        {
            if (!Active) return;
            if (!ValidClock(now) || now < _lastAt)
            { Fail("invalid or backwards standalone clock"); return; }
            _lastAt = now;
            double elapsed = (double)now - _startedAt;
            if (elapsed >= MaximumSeconds) { Fail("120-second standalone pose limit"); return; }
            if (_phase == Phase.Entering)
            {
                if (_synchronized)
                {
                    if (elapsed > SynchronizedEntryLimit || elapsed >= SynchronizedEntryLimit && !entryReady)
                    { Fail("stance did not settle within 3 seconds"); return; }
                    float progress = Math.Min((float)elapsed / _entrySeconds, 1);
                    SampleTime = HoldSeconds * progress;
                    Blend = Smooth(progress);
                    if (elapsed >= _entrySeconds && entryReady) _phase = Phase.Held;
                    return;
                }
                // Retain the authored motion and exact hold pose; shorten only its entry clock.
                SampleTime = HoldSeconds * Math.Min((float)elapsed / EntrySeconds, 1);
                Blend = Smooth((float)elapsed / EntryBlendSeconds);
                if (elapsed >= EntrySeconds) _phase = Phase.Held;
            }
            else if (_phase == Phase.Returning)
            {
                float returned = (float)((double)now - _returnAt);
                if (_synchronized)
                {
                    float t = Math.Min(returned / _synchronizedReturnSeconds, 1);
                    SampleTime = t >= 1 ? EndSeconds : _returnSample + (EndSeconds - _returnSample) * t;
                    Blend = _returnBlend * (1 - Smooth(t));
                    if (t >= 1) End();
                    return;
                }
                if (_fastReturn)
                {
                    float t = Math.Min(returned / FastReturnSeconds, 1);
                    // The clip already eases the lowering motion. Retain its pose/blend relationship.
                    SampleTime = t >= 1 ? EndSeconds : _returnSample + (EndSeconds - _returnSample) * t;
                }
                else SampleTime = Math.Min(EndSeconds, _returnSample + returned);
                Blend = Smooth((EndSeconds - SampleTime) / ReturnBlendSeconds);
                if (SampleTime >= EndSeconds) End();
            }
            else if (_phase == Phase.EarlyClose)
            {
                float returned = (float)((double)now - _returnAt);
                float duration = _synchronized ? _earlyReturnSeconds : ReturnBlendSeconds;
                Blend = _returnBlend * (1 - Smooth(returned / duration));
                if (returned >= duration) End();
            }
        }

        public void Close(float now) => Close(now, null);

        public void Close(float now, float returnSeconds) => Close(now, (float?)returnSeconds);

        private void Close(float now, float? returnSeconds)
        {
            Tick(now);
            if (!Active || Returning) return;
            if (_synchronized && returnSeconds.HasValue)
            {
                if (!ValidDuration(returnSeconds.Value)) { Fail("invalid synchronized return duration"); return; }
                _synchronizedReturnSeconds = returnSeconds.Value;
            }
            _returnAt = now; _returnSample = SampleTime; _returnBlend = Blend;
            _fastReturn = Held && FasterReturn; // A setting change cannot retime an exit already underway.
            if (_synchronized) _earlyReturnSeconds = Math.Max(ReturnBlendSeconds, _synchronizedReturnSeconds * Blend);
            _phase = Held ? Phase.Returning : Phase.EarlyClose;
        }

        public void Abort()
        {
            End(); SampleTime = 0;
            _fastReturn = false;
            _synchronized = false;
            _entrySeconds = _synchronizedReturnSeconds = _earlyReturnSeconds = 0;
            _startedAt = _lastAt = _returnAt = _returnSample = _returnBlend = 0;
        }

        private void End() { _phase = Phase.None; Blend = 0; }
        private void Fail(string reason) { Abort(); Fault = reason; }
        private static bool ValidClock(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0;
        private static bool ValidDuration(float value) => ValidClock(value) && value >= .1f && value <= 3f;
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
