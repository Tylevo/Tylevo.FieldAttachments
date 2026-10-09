using System;
using System.Collections.Generic;

namespace Tylevo.FieldAttachments.Core
{
    // TSC-style virtual cursor: own look/clicks while held, then drain held mouse
    // buttons before returning shooting input. Never own the OS cursor or raid clock.
    public sealed class PointerCapture
    {
        public bool Active { get; private set; }
        public bool NeedsRelease { get; private set; }
        private bool _mouseOwned;
        private int _mouseFrame = -1, _frame;
        public bool SuppressMouse => Active || _mouseOwned || _mouseFrame == _frame;
        public void Sample(bool eligible, bool held, bool mouseHeld, bool focused, int frame, bool automatic = false)
        {
            _frame = frame;
            if (focused && !held) NeedsRelease = false;
            // Automatic activation owns a separate physical-key release latch.
            if (automatic && eligible && focused) NeedsRelease = false;
            if (held && (!eligible || !focused)) NeedsRelease = true;
            bool next = eligible && focused && held && !NeedsRelease;
            if (Active || next) { _mouseFrame = frame; _mouseOwned |= mouseHeld; }
            Active = next;
            if (!mouseHeld && _mouseOwned) { _mouseFrame = frame; _mouseOwned = false; }
        }
        public void Cancel() { if (Active) _mouseFrame = _frame; Active = false; NeedsRelease = true; }
    }

    public sealed class PointerGesture
    {
        private string? _target;
        private int _generation;
        public void Press(string? target, int generation) { _target = target; _generation = generation; }
        public bool Release(string? target, int generation)
        {
            bool accept = target != null && target == _target && generation == _generation;
            Cancel(); return accept;
        }
        public void Cancel() { _target = null; }
    }

    public static class PointerInputPolicy
    {
        // Keep movement, menus and native end/release commands flowing (as in TSC).
        private static readonly HashSet<string> Pass = new HashSet<string>(StringComparer.Ordinal) {
            "None", "ToggleSpeed", "ToggleDuck", "ToggleSprinting", "EndSprinting",
            "ToggleProne", "Jump", "Vaulting", "VaultingEnd", "ToggleWalk", "EndWalk", "RestorePose",
            "ToggleInventory", "Escape", "Enter", "ShowConsole", "MakeScreenshot", "F12",
            "EndShooting", "EndAlternativeShooting", "EndBreathing", "EndInteracting", "EndSpecialInteracting", "ResetLookDirection",
            "ReturnFromLeftStep", "ReturnFromRightStep", "EndLeanLeft", "EndLeanRight", "EndAnimBlindFireAbove", "EndAnimBlindFireRight",
            "BlindShootEnd", "FinishHighThrow", "FinishLowThrow", "ToggleTalk", "StopTalk", "ToggleVoip"
        };
        public static bool Block(string command, bool capture, bool suppressMouse) => capture ? !Pass.Contains(command) :
            suppressMouse && (command == "ToggleShooting" || command == "ToggleAlternativeShooting");
        public static void FilterAxes(float[] axes, bool capture)
        {
            if (!capture || axes == null) return;
            // Local 4.1.5 EAxis: MoveX/Y=0/1; TurnX/Y, LookX/Y, LeanX=2..6.
            for (int i = 2; i <= 6 && i < axes.Length; i++) axes[i] = 0;
        }
    }
}
