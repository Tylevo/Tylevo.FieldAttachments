using System;

namespace Tylevo.FieldAttachments.Core
{
    public enum ReplacementPhase { None, Removing, WaitingForIdle, Installing, Succeeded, Stopped }

    // A single click owns at most two separately validated native moves. Never retry or roll back.
    public sealed class AttachmentReplacement
    {
        public AttachmentRequest? Request { get; private set; }
        public bool Pending => Request!=null;
        public ReplacementPhase Phase { get; private set; }
        public string Status { get; private set; }="";
        private int _revision;
        private double _deadline;
        private bool _removed;

        public bool Begin(AttachmentRequest request,InstallSession native)
        {
            if(Pending || native.Busy || native.Blocked || request.Action!=AttachmentAction.Replace || request.Removal==null) return false;
            Request=request; _revision=native.Revision; _removed=false;
            Phase=ReplacementPhase.Removing; Status="SWAPPING: removing "+request.Removal.ItemName; return true;
        }
        public void Observe(InstallSession native,double now)
        {
            if(!Pending) return;
            if(native.Blocked) { Stop("native outcome unknown; no further operation will be submitted"); return; }
            if(native.Busy) return;
            // One begin, one submission, one verified completion, with no intervening request.
            if(native.Revision!=_revision+3 || !native.Submitted || native.Phase!=InstallPhase.Succeeded)
            { Stop(native.Status); return; }
            if(Phase==ReplacementPhase.Removing)
            {
                _removed=true; _deadline=now+8; Phase=ReplacementPhase.WaitingForIdle;
                Status="SWAPPING: waiting to install "+Request!.ItemName;
            }
            else if(Phase==ReplacementPhase.WaitingForIdle && now>=_deadline) Stop("hands did not become ready within 8 seconds");
            else if(Phase==ReplacementPhase.Installing)
            { Status="REPLACED WITH "+Request!.ItemName; Phase=ReplacementPhase.Succeeded; Request=null; }
        }
        public string PrepareInstall(RaidSnapshot fresh,InstallSession native,double now,out AttachmentRequest? installation)
        {
            installation=null;
            if(!Pending || Phase!=ReplacementPhase.WaitingForIdle) return "No replacement continuation is pending.";
            Observe(native,now);
            if(!Pending) return Status;
            if(native.Busy) { Stop("another native operation started"); return Status; }
            string denial=Request!.AfterRemoval(fresh,out installation);
            if(denial.Length!=0) { Stop(denial); return Status; }
            // Consume continuation BEFORE the caller submits. Preparing a second time is forbidden.
            _revision=native.Revision; Phase=ReplacementPhase.Installing;
            Status="SWAPPING: installing "+Request.ItemName; return "";
        }
        public bool Stop(string reason)
        {
            if(!Pending) return false;
            Status="SWAP STOPPED: "+reason+(_removed ? " Removed attachment remains in carried storage; no automatic rollback." : "");
            Phase=ReplacementPhase.Stopped; Request=null; return true;
        }
    }
}
