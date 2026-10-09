using System;

namespace Tylevo.FieldAttachments.Core
{
    // One presentation request tied to one explicit native transaction. Never retries inventory or inspection.
    public sealed class PoseResume
    {
        private object? _player, _weapon, _controller, _hands;
        private string _weaponId="";
        private int _revision, _idleSamples;
        private double? _deadline;
        public bool Pending => _player!=null;
        public string Status { get; private set; } = "none";

        public void Track(RaidSnapshot snapshot, object? hands, int revision, bool modeOpen)
        {
            Cancel("superseded by an explicit request");
            if (!modeOpen || snapshot.Player==null || snapshot.Weapon==null || snapshot.Controller==null ||
                snapshot.WeaponId.Length==0 || hands==null) return;
            _player=snapshot.Player; _weapon=snapshot.Weapon; _controller=snapshot.Controller; _hands=hands;
            _weaponId=snapshot.WeaponId; _revision=revision; _deadline=null; _idleSamples=0;
            Status="waiting for verified native success";
        }
        public bool Cancel(string reason)
        {
            if (!Pending) return false;
            _player=_weapon=_controller=_hands=null; _weaponId=""; _deadline=null; _idleSamples=0;
            Status="cancelled: "+reason; return true;
        }
        public bool Poll(InstallSession native, RaidSnapshot snapshot, object? hands, bool modeOpen,
            bool interrupted, bool ready, bool visibleIdle, bool defer, double now)
        {
            if (!Pending) return false;
            string denial=!modeOpen ? "attachment pose mode closed or unavailable" :
                interrupted ? "gameplay/context interrupted" :
                !ReferenceEquals(_player,snapshot.Player) || !ReferenceEquals(_weapon,snapshot.Weapon) ||
                !ReferenceEquals(_controller,snapshot.Controller) || !ReferenceEquals(_hands,hands) || _weaponId!=snapshot.WeaponId ?
                    "player/weapon/controller changed" :
                native.Blocked ? "native outcome unknown" :
                native.Revision<=_revision ? "no new native request" : "";
            if (denial.Length!=0) { Cancel(denial); return false; }
            if (native.Busy) return false;
            if (!native.Submitted || native.Phase!=InstallPhase.Succeeded)
            { Cancel("native request did not succeed"); return false; }
            if (!_deadline.HasValue) { _deadline=now+8; Status="success verified; waiting for stable hands idle"; }
            if (now>=_deadline.Value) { Cancel("hands did not become ready within 8 seconds"); return false; }
            if (!ready || !visibleIdle)
            {
                _idleSamples=0;
                Status=ready ? "success verified; waiting for visible weapon idle" : "success verified; waiting for hands idle";
                return false;
            }
            if (defer || ++_idleSamples<2) return false;
            Cancel("consumed"); // Drop identities BEFORE invoking native inspection; a refusal is not retried.
            Status="inspection attempt consumed after verified success and idle";
            return true;
        }
    }
}
