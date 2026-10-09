using System;
using System.Linq;
using Tylevo.FieldAttachments.Core;
using Tylevo.FieldAttachments.Runtime;
using UnityEngine;

namespace Tylevo.FieldAttachments
{
    public sealed partial class Plugin
    {
        private readonly AttachmentReplacement _replacement=new AttachmentReplacement();
        private object? _replacementHands;
        private void StopReplacement(string reason)
        {
            if(!_replacement.Stop(reason)) return;
            _replacementHands=null; _clickStatus=_replacement.Status;
            CancelPoseResume("replacement interrupted"); Trace(_clickStatus);
        }
        private void PollReplacement(bool interrupted)
        {
            var request=_replacement.Request;
            if(request==null) return;
            string denial=!_liveInstall.Value || !_visible || !_enabled.Value ? "attachment actions closed or disabled" :
                interrupted || Down(_refresh.Value) || NativeReloadPressed || WeaponNumberPressed() ||
                NativeSprintReader.Read(_read,request.Player)!=false ? "another action interrupted the swap" :
                !PointerLocalOwnership() || !ReferenceEquals(_reader.HeldWeapon(request.Player),request.Weapon) ||
                !ReferenceEquals(_read.Get(request.Player,"HandsController"),_replacementHands) ||
                !ReferenceEquals(_read.Get(request.Player,"InventoryController","_inventoryController"),request.Controller) ? "player/hands/weapon context changed" :
                _pose.Status.StartsWith("POSE FAULT",StringComparison.Ordinal) ? "pose fault" : "";
            if(denial.Length!=0) { StopReplacement(denial); Render(); return; }
            string before=_replacement.Status;
            _replacement.Observe(_probe.Session,Time.unscaledTime);
            if(before!=_replacement.Status) { _clickStatus=_replacement.Status; Trace(_clickStatus); Render(); }
            if(!_replacement.Pending) { _replacementHands=null; return; }
            if(_replacement.Phase!=ReplacementPhase.WaitingForIdle || Down(_report.Value) || _pose.Active ||
                _probe.ReadHands(_state.Snapshot).Denial.Length!=0 || !_pose.VisibleIdle(_state.Snapshot)) return;

            _resources.ClearSession();
            var fresh=_reader.Capture(_hints.Value,_unknown.Value);
            // This consumes the second-leg authorization before invoking the existing native adapter.
            denial=_replacement.PrepareInstall(fresh,_probe.Session,Time.unscaledTime,out var installation);
            _state.ReplaceSnapshot(fresh,true,"replacement fresh validation");
            if(denial.Length!=0) { _replacementHands=null; _clickStatus=_replacement.Status; Trace(_clickStatus); Render(); return; }
            var slot=fresh.Slots.Single(s=>s.Slot.Path==installation!.SlotPath && s.Slot.Group==installation.Group);
            var candidate=slot.Candidates.Single(c=>c.Item.Id==installation!.ItemId);
            _clickStatus=_replacement.Status; Trace("REPLACEMENT_INSTALL "+installation!.Identity);
            // Keep the weapon down across removal. Resume inspection only after the final install succeeds.
            TrackPoseResume(fresh);
            _probe.TryInstall(fresh,slot,candidate,_liveInstall.Value,true);
            Render();
        }
    }
}
