using System;
using Tylevo.FieldAttachments.Core;
using Tylevo.FieldAttachments.Runtime;
using UnityEngine;

namespace Tylevo.FieldAttachments
{
    public sealed partial class Plugin
    {
        private readonly PoseResume _poseResume = new PoseResume();
        private void TrackPoseResume(RaidSnapshot snapshot)
        {
            _poseResume.Track(snapshot,_read.Get(snapshot.Player,"HandsController"),_probe.Session.Revision,_poseActivation.Open);
            if (_poseResume.Pending) Trace("POSE_RESUME "+_poseResume.Status);
        }
        private void CancelPoseResume(string reason)
        { if (_poseResume.Cancel(reason)) Trace("POSE_RESUME "+_poseResume.Status); }
        private void PollPoseResume(bool interrupted)
        {
            if (!_poseResume.Pending) return;
            var snapshot=_state.Snapshot;
            bool context=PointerLocalOwnership() && ReferenceEquals(_reader.HeldWeapon(snapshot.Player),snapshot.Weapon) &&
                ReferenceEquals(_read.Get(snapshot.Player,"InventoryController","_inventoryController"),snapshot.Controller);
            var hands=_probe.ReadHands(snapshot);
            string before=_poseResume.Status;
            bool start=_poseResume.Poll(_probe.Session,snapshot,_read.Get(snapshot.Player,"HandsController"),
                _visible && _poseActivation.Open && _enabled.Value && context,
                interrupted || Down(_refresh.Value) || Down(_installKey.Value) || Down(_poseKey.Value) ||
                NativeReloadPressed || WeaponNumberPressed() || NativeSprintReader.Read(_read,snapshot.Player)!=false ||
                hands.Aiming==true || hands.TriggerPressed==true || _pose.Status.StartsWith("POSE FAULT",StringComparison.Ordinal),
                !_pose.Active && hands.Denial.Length==0,_pose.VisibleIdle(snapshot),Down(_report.Value),Time.unscaledTime);
            if (before!=_poseResume.Status) Trace("POSE_RESUME "+_poseResume.Status);
            if (!start) return;
            // Reuse the standalone entry and its fresh build/player/hands/animator checks.
            _pose.Start(snapshot,false);
            Trace("POSE_RESUME start result: "+_pose.Status);
            Render();
        }
    }
}
