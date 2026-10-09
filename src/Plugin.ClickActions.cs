using System;
using System.Linq;
using Tylevo.FieldAttachments.Core;
using Tylevo.FieldAttachments.Runtime;
using UnityEngine;

namespace Tylevo.FieldAttachments
{
    public sealed partial class Plugin
    {
        private readonly ClickRequestQueue _clicks = new ClickRequestQueue();
        private object? _clickHands;
        private string _clickStatus="";
        private bool _clickSubmitted;
        private AttachmentAction _clickAction;

        private void RequestClick(AttachmentAction action, AttachmentGroup group, string path, string item)
        {
            Trace("CLICK_RECEIVED "+action+" item="+item+" slot="+path);
            if (_clicks.Busy || _replacement.Pending || _probe.Session.Busy || _probe.Session.Blocked)
            { Trace("CLICK_REJECTED duplicate/latched; original request retained"); return; }
            string denial=!_liveInstall.Value ? "Live actions OFF." :
                _pose.Status.StartsWith("POSE FAULT",StringComparison.Ordinal) ? "Pose fault; restart required." :
                !_visible || _pointer?.Capture.Active!=true ? "Mouse context expired." : "";
            AttachmentRequest? request=null;
            if (denial.Length==0) denial=AttachmentRequest.Create(action,_state.Snapshot,group,path,item,out request);
            if (denial.Length==0 && !_pose.Active) denial=_probe.ReadHands(_state.Snapshot).Denial;
            if (denial.Length!=0) { RejectClick(denial); return; }
            if (!_clicks.TryBegin(request!,_probe.Session,Time.unscaledTime)) return;
            CancelPoseResume("new explicit click");
            _clickHands=_read.Get(request!.Player,"HandsController");
            _clickAction=action; _clickStatus="";
            _state.ClearStage("explicit mouse request replaces keyboard arm");
            Trace("CLICK_CONFIRMED "+request.Identity+"; waiting for native idle, maximum 8 seconds");
            // Same verified return as F5 release. The click, not the return event,
            // is the authorization for the one bound inventory request.
            _pose.Release("confirmed mouse "+action,true);
            _overlay?.ResetPointer();
        }
        private void PollClickRequest()
        {
            var request=_clicks.Request;
            if (request==null) return;
            string denial=!_liveInstall.Value ? "Live actions turned OFF." :
                _probe.Session.Busy || _probe.Session.Blocked ? "Native request pending or outcome unknown." :
                _pose.Status.StartsWith("POSE FAULT",StringComparison.Ordinal) ? "Pose return fault." :
                !PointerLocalOwnership() || !ReferenceEquals(_reader.HeldWeapon(request.Player),request.Weapon) ||
                !ReferenceEquals(_read.Get(request.Player,"HandsController"),_clickHands) ||
                !ReferenceEquals(_read.Get(request.Player,"InventoryController","_inventoryController"),request.Controller) ? "Player/hands/weapon context changed." :
                Down(_refresh.Value) || NativeReloadPressed || WeaponNumberPressed() ||
                (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) && _pointer?.Capture.SuppressMouse!=true ||
                NativeSprintReader.Read(_read,request.Player)!=false ? "Request interrupted by another action." :
                _clicks.Expired(Time.unscaledTime) ? "Weapon did not become ready; click cancelled." : "";
            if (denial.Length!=0) { CancelClickRequest(denial); return; }
            // Reporting is observation only, including while a click waits.
            if (Down(_report.Value) || _pose.Active || _probe.ReadHands(_state.Snapshot).Denial.Length!=0) return;
            _clicks.Take(); _clickHands=null; // Consume BEFORE fresh capture or invoking any native method. No retries.
            _resources.ClearSession();
            var fresh=_reader.Capture(_hints.Value,_unknown.Value);
            denial=request.Validate(fresh);
            _state.ReplaceSnapshot(fresh,true,"explicit click fresh validation");
            if (denial.Length!=0) { RejectClick(denial); Render(); return; }
            var slot=fresh.Slots.Single(s=>s.Slot.Path==request.SlotPath && s.Slot.Group==request.Group);
            var candidate=request.Action==AttachmentAction.Install ? slot.Candidates.Single(c=>c.Item.Id==request.ItemId) : null;
            Trace("CLICK_EXECUTE "+request.Identity);
            if(request.Action==AttachmentAction.Replace)
            {
                if(!_replacement.Begin(request,_probe.Session)) { RejectClick("Replacement already pending or blocked."); return; }
                _replacementHands=_read.Get(request.Player,"HandsController");
                _clickSubmitted=false; _clickStatus=_replacement.Status;
                _probe.TryInstall(fresh,slot,null,_liveInstall.Value,true,true);
            }
            else
            {
                _clickSubmitted=true;
                TrackPoseResume(fresh);
                _probe.TryInstall(fresh,slot,candidate,_liveInstall.Value,true,request.Action==AttachmentAction.Uninstall);
            }
            _notice=_probe.Session.Status;
            Render();
        }
        private void CancelClickRequest(string reason)
        {
            StopReplacement(reason);
            var request=_clicks.Take(); _clickHands=null;
            if (request==null) return;
            Trace("CLICK_CANCELLED "+request.Identity+"; "+reason+"; no native operation submitted");
            RejectClick(reason);
        }
        private void RejectClick(string reason)
        {
            _clickStatus="CANCELLED: "+reason;
            _probe.Session.RejectRequest(reason);
            Trace("CLICK_REJECTED before native probe: "+reason);
            UpdateActionStatus();
        }
    }
}
