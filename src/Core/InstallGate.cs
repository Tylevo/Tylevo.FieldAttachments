using System;

namespace Tylevo.FieldAttachments.Core
{
    // A deliberately narrower policy than the game supports. These facts are checked
    // again against live objects by the runtime adapter. None is a substitute for
    // native validation, and unknown (null) safety facts deny the experiment.
    public sealed class InstallFacts
    {
        public bool Enabled;
        public bool Removing;
        public bool InstalledItemMatches;
        public bool KnownBuild;
        public bool SinglePlayer;
        public bool CurrentSelection;
        public bool SnapshotComplete;
        public bool SourceIsCarried;
        public bool SupportedLeafAttachment;
        public bool SupportedContext;
        public bool? YourPlayer;
        public bool? Alive;
        public bool? InventoryOpened;
        public bool? Aiming;
        public bool? TriggerPressed;
        public bool? InventoryLocked;
        public bool? HasActiveEvents;
        public bool Idle;
        public bool? Required;
        public bool? Locked;
        public bool? Deleted;
        public bool? Empty;
        public bool? Examined;
        public bool? RaidModdable;
        public bool? FilterPass;
        public bool NeutralPinState;
    }

    public static class InstallGate
    {
        public static string Denial(InstallFacts f)
        {
            if (f == null) throw new ArgumentNullException(nameof(f));
            if (!f.Enabled) return "Attachment changes are disabled. Enable F12 / General / Allow attachment changes.";
            if (!f.KnownBuild) return "Game assembly differs from the inspected build; live install blocked.";
            if (!f.SinglePlayer) return "This probe is single-player only; Fika/custom player controllers are not supported.";
            if (!f.SupportedContext || f.YourPlayer != true || f.Alive != true) return "A live local raid or active hideout shooting range could not be verified.";
            if (!f.CurrentSelection) return "Selection changed or expired. Rescan and make a fresh request.";
            if (!f.SnapshotComplete) return "Inventory scan was incomplete; live install blocked.";
            if (!f.Removing && !f.SourceIsCarried) return "Install accepts accessible carried storage only.";
            if (!f.SupportedLeafAttachment) return "Only supported attachments or intact verified optic assemblies can be moved.";
            if (f.InventoryOpened != false || f.Aiming != false || f.TriggerPressed != false ||
                f.InventoryLocked != false || f.HasActiveEvents != false || !f.Idle)
                return "Hands/inventory are busy or readiness is unknown. Close inventory, stop aiming/firing, and wait for idle.";
            if (f.Required != false || f.Locked != false || f.Deleted != false) return "Target slot is restricted or its state is unknown.";
            if (f.Removing && (!f.InstalledItemMatches || f.Empty != false)) return "Installed attachment identity changed or is unknown.";
            if (!f.Removing && f.Empty != true) return "Target slot is occupied; a native install requires verified removal first.";
            if (f.Examined != true || f.RaidModdable != true || !f.NeutralPinState) return "Item is unexamined, pinned/locked, not raid-moddable, or one of those checks is unknown.";
            if (f.FilterPass != true) return "Native slot filter did not accept this item (or filter binding is unavailable).";
            return "";
        }
    }

    // Session-scoped guard. In particular, closing the UI does not reset this state.
    // A timeout is UNKNOWN, not rejection, and never authorizes a retry.
    public enum InstallPhase { None, Validating, Pending, Succeeded, Rejected, Unknown }

    public sealed class InstallSession
    {
        public bool Busy { get; private set; }
        public bool Blocked { get; private set; }
        public bool Submitted { get; private set; }
        public InstallPhase Phase { get; private set; }
        public string Status { get; private set; } = "No live install attempted.";
        public int Revision { get; private set; }
        public bool TryBegin()
        {
            if (Busy || Blocked) return false;
            Busy = true; Submitted = false; Phase = InstallPhase.Validating; Set("Validating install request..."); return true;
        }
        public void MarkSubmitted()
        {
            if (!Busy || Blocked || Submitted) throw new InvalidOperationException("No unsubmitted validation to submit.");
            Submitted = true; Phase = InstallPhase.Pending; Set("Native install submitted; waiting for completion. Do not reload, fire, or switch weapons.");
        }
        public void RejectRequest(string message)
        {
            if (Busy || Blocked) return; // A denied duplicate must not replace the original outcome.
            Phase = InstallPhase.Rejected; Set("REJECTED before native probe: " + message);
        }
        public void Finish(string message, bool uncertain, bool succeeded = false)
        {
            Busy = false; Blocked |= uncertain;
            Phase = Blocked ? InstallPhase.Unknown : succeeded ? InstallPhase.Succeeded : InstallPhase.Rejected;
            Set(message + (Blocked && !uncertain ? " Live install remains blocked until game restart." : ""));
        }
        public void TimedOut()
        {
            if (!Busy || !Submitted || Blocked) return;
            Blocked = true; Phase = InstallPhase.Unknown;
            Set("Install completion is unknown. No retry. Wait, export report, and restart the game before another live test.");
            // Continue watching the original task; do not dispose, roll back, or resubmit it.
        }
        private void Set(string message) { Status = message; Revision++; }
    }
}
