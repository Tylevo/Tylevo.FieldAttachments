using System;
using System.Globalization;
using Tylevo.FieldAttachments.Core;
using UnityEngine;

namespace Tylevo.FieldAttachments.Runtime
{
    public sealed partial class InspectionPose
    {
        public bool StmPresentationEnabled { get; set; }
        public float StmPresentationDegrees { get; set; } = StmPresentation.DefaultDegrees;
        private bool _stmTrial;
        private Transform? _stmRoot, _stmPivot;
        private Camera? _stmCamera;
        private Transform[] _stmBones = Array.Empty<Transform>();
        private PoseFrameLease<Vector4>? _stmFrame;
        private readonly Vector4[] _stmValues = new Vector4[6];
        private string _stmStatus = "not requested", _stmFaultReported = "";
        private float _stmDegrees;
        private bool _stmApplied;
        private string _stmBypass = "";
        private Vector3 _stmScale;
        private float? _stmNativeDepth;
        private float _stmPlaybackSpeed;
        private readonly StmTiltBlend _stmBlend = new StmTiltBlend();
        public bool PresentationReturning => _standaloneClock.Returning ||
            (!_standaloneClock.Active && (_standalonePlayback.Prepared || _standalonePlayback.FrameActive ||
                _supportPresentation.Prepared || _supportPresentation.FrameActive));

        private void PrepareStm(RaidSnapshot snapshot, Animator animator, string template, string controllerKey, PoseMarker? saved, bool learn)
        {
            _stmTrial = !_customStm && StmPresentation.Requested(StmPresentationEnabled, template);
            _stmBypass = "";
            _stmBlend.Cancel(); _stmPlaybackSpeed = animator.speed;
            _stmApplied = false; _stmFrame = null; _stmBones = Array.Empty<Transform>();
            _stmRoot = _stmPivot = null; _stmCamera = null;
            _stmScale = Vector3.zero; _stmNativeDepth = null;
            _stmStatus = !_stmTrial ? "not requested" : learn ? "native calibration; tilt bypassed" : "preparing";
            if (!_stmTrial || learn) return;
            string denial = StmPresentation.MarkerDenial(saved, controllerKey);
            if (denial.Length != 0) throw new InvalidOperationException(denial);
            if (animator.runtimeAnimatorController.name != StmPresentation.Controller ||
                animator.layerCount <= 1 || animator.GetLayerName(1) != "Hands" || !animator.HasState(1, PoseMarker.SharedState))
                throw new InvalidOperationException("STM native controller/state changed");
            if (!StmPresentation.ValidDegrees(StmPresentationDegrees)) throw new InvalidOperationException("STM tilt outside -35..35 degrees");
            _stmDegrees = StmPresentationDegrees; // F12 is an interruption; new settings take effect on next open.
            Transform root = animator.transform;
            Transform weapon = root.Find("Weapon_root"), left = root.Find("Base HumanLCollarbone"), right = root.Find("Base HumanRCollarbone");
            Transform cameraBone = root.Find("Camera_animated");
            Transform pivot = root.Find("Weapon_root/Weapon_root_anim/weapon/weapon_R_hand_marker");
            Camera? camera = WeaponAnchorReader.ResolveCamera(_read);
            if (weapon == null || left == null || right == null || pivot == null || cameraBone == null || camera == null ||
                weapon.parent != root || left.parent != root || right.parent != root || cameraBone.parent != root ||
                !pivot.IsChildOf(weapon) || camera.transform.IsChildOf(weapon) || camera.transform.IsChildOf(left) || camera.transform.IsChildOf(right))
                throw new InvalidOperationException("STM weapon/arm/pivot/camera hierarchy not verified");
            _stmRoot = root; _stmPivot = pivot; _stmCamera = camera; _stmBones = new[] { weapon, left, right };
            string scaleDenial = StmScaleDenial(snapshot.Player);
            if (scaleDenial.Length != 0) throw new InvalidOperationException("STM rig FOV scale: " + scaleDenial);
            _stmFrame = new PoseFrameLease<Vector4>(6,
                i => i % 2 == 0 ? Position(_stmBones[i / 2].localPosition) : Rotation(_stmBones[i / 2].localRotation),
                (i, v) => {
                    if (i % 2 == 0) _stmBones[i / 2].localPosition = new Vector3(v.x, v.y, v.z);
                    else _stmBones[i / 2].localRotation = new Quaternion(v.x, v.y, v.z, v.w);
                }, i => _stmBones[i / 2] != null && _stmRoot != null && _stmBones[i / 2].parent == _stmRoot,
                (i, a, b) => i % 2 == 0 ? a.Equals(b) : StmPresentation.RestoredRotation(
                    new CardVector(a.x, a.y, a.z), a.w, new CardVector(b.x, b.y, b.z), b.w),
                v => "(" + v.x.ToString("R", CultureInfo.InvariantCulture) + "," + v.y.ToString("R", CultureInfo.InvariantCulture) +
                    "," + v.z.ToString("R", CultureInfo.InvariantCulture) + "," + v.w.ToString("R", CultureInfo.InvariantCulture) + ")");
            _stmStatus = "native saved STM frame; tilt=" + _stmDegrees.ToString("F1", CultureInfo.InvariantCulture);
            Event("STM_PREPARED " + _stmStatus + "; three sibling roots, camera excluded");
        }
        private string StmScaleDenial(object? player)
        {
            if (_stmRoot == null) return "rig missing";
            object? hierarchy = _read.Get(_read.Get(player, "HandsController"), "HandsHierarchy");
            if (_read.Get(hierarchy, "Self") as Transform != _stmRoot) return "native HandsHierarchy.Self changed";
            _stmScale = _stmRoot.localScale;
            _stmNativeDepth = _read.Get(player, "RibcageScaleCurrent") is float value ? value : (float?)null;
            Matrix4x4 frame = _stmRoot.localToWorldMatrix;
            return StmPresentation.ScaleDenial(CoreVector(_stmScale), CoreVector(frame.MultiplyVector(Vector3.right)),
                CoreVector(frame.MultiplyVector(Vector3.up)), CoreVector(frame.MultiplyVector(Vector3.forward)), _stmNativeDepth);
        }
        private static Vector4 Position(Vector3 v) => new Vector4(v.x, v.y, v.z, 0);
        private static Vector4 Rotation(Quaternion q) => new Vector4(q.x, q.y, q.z, q.w);
        private static CardVector CoreVector(Vector3 v) => new CardVector(v.x, v.y, v.z);
        private static Vector3 UnityVector(CardVector v) => new Vector3(v.X, v.Y, v.Z);
        private static bool Finite(Vector3 v) => PoseMarker.Finite(v.x) && PoseMarker.Finite(v.y) && PoseMarker.Finite(v.z);

        // Called after native LateUpdate, before the existing card/anchor projection.
        private string StmFrameDenial()
        {
            if (!StmPresentationEnabled) return "trial disabled";
            if (!Application.isFocused) return "focus lost";
            if (_context == null || _animator == null) return "pose context/animator missing";
            if (!SameLiveAnimator()) return "live animator/context: enabled=" + _animator.isActiveAndEnabled +
                " player=" + ReferenceEquals(_reader.MainPlayer(), _context.Player) +
                " hands=" + ReferenceEquals(_read.Get(_context.Player, "HandsController"), _hands) +
                " weapon=" + ReferenceEquals(_read.Get(_hands, "Item"), _context.Weapon) +
                " wrapper=" + (ResolveAnimator(_hands) == _animator) + " controller=" + (_animator.runtimeAnimatorController == _controller) +
                " " + _probe.ReadContext(_context).Evidence;
            if (!OwnedOperation()) return "owned inspection operation changed";
            if (!StartEventFired()) return "native inspection start event not Ready";
            if (!NeutralHands()) return "hands not neutral: " + _probe.ReadHands(_context!).Evidence +
                " sprint=" + NativeSprint() + " view=" + ReadAccess.Text(_read.Get(_context!.Player, "PointOfView")) +
                " malfunction=" + ReadAccess.Text(_read.Get(_read.Get(_context.Weapon, "MalfState"), "State"));
            if (_state.Phase == PosePhase.Held)
            {
                if (!_speed.Active || _animator!.speed != 0) return "paused speed ownership changed";
            }
            else if ((_state.Phase != PosePhase.Playing && _state.Phase != PosePhase.Returning) || _speed.Active ||
                !PoseMarker.Finite(_stmPlaybackSpeed) || _stmPlaybackSpeed <= 0 || _animator.speed != _stmPlaybackSpeed)
                return "native playback speed/phase changed";
            if (_motion.Active) return "donor clip still owned";
            if (_stmRoot != _animator.transform || _stmPivot == null) return "bound STM root/pivot changed";
            if (_stmCamera == null || !_stmCamera.isActiveAndEnabled) return "bound FPS camera inactive";
            string scaleDenial = StmScaleDenial(_context.Player);
            if (scaleDenial.Length != 0) return "rig FOV scale: " + scaleDenial + " " + StmScaleReport;
            return StmClipDenial();
        }
        private string StmClipDenial()
        {
            if (_animator == null || _animator.layerCount <= 1 ||
                _animator.GetCurrentAnimatorStateInfo(1).fullPathHash != PoseMarker.SharedState || _animator.IsInTransition(1))
                return "native Hands inspection state changed/transitioning";
            var clips = _animator.GetCurrentAnimatorClipInfo(1);
            if (clips.Length != 1 || clips[0].clip == null || clips[0].clip.name != "stm9_look" || clips[0].weight < .99f)
                return "native stm9_look clip not confirmed";
            return "";
        }
        private void ApplyLegacyPresentationFrame()
        {
            if (!_stmTrial || _learning || _stmFrame == null ||
                (_state.Phase != PosePhase.Playing && _state.Phase != PosePhase.Held && !PresentationReturning) || _stmBypass.Length != 0) return;
            if (_stmFrame.Fault.Length != 0) { Release("STM frame restoration fault; see STM_FAULT"); return; }
            try
            {
                // Native start/transition is expected before its stable inspection. Wait
                // without writing; once we have applied, any such change fails closed.
                if (_state.Phase == PosePhase.Playing && !_stmApplied && !StmEntryReady()) return;
                string denial = StmFrameDenial();
                if (denial.Length != 0)
                {
                    // Reject only the extra visual offset. Tick still owns the native pose's
                    // identity, hands, sprint, focus, speed, duration and release checks.
                    RestorePresentationFrame(); _stmBypass = denial;
                    StopStmReturn(denial);
                    _stmStatus = "native hold only; tilt bypassed: " + denial;
                    Event("STM_BYPASS " + denial + "; no visual retry this opening; native pose guards retained");
                    return;
                }
                Vector3 pivot = _stmRoot!.InverseTransformPoint(_stmPivot!.position);
                // Rotation-only conversion is deliberate: the local rig is rotated before
                // its shared native depth compression. InverseTransformVector would distort this axis.
                Vector3 axis = _stmRoot.InverseTransformDirection(_stmCamera!.transform.forward).normalized;
                if (!Finite(pivot) || !Finite(axis)) throw new InvalidOperationException("Nonfinite STM pivot/axis");
                float blend = PresentationReturning ? _stmBlend.Return(Time.unscaledTime) :
                    _stmBlend.Entry(Time.unscaledTime, _animator!.GetCurrentAnimatorStateInfo(1).normalizedTime,
                        _targetMarker!.Time, _state.Phase == PosePhase.Held);
                if (blend <= 0) return;
                float degrees = _stmDegrees * blend;
                Quaternion turn = Quaternion.AngleAxis(degrees, axis);
                for (int i = 0; i < _stmBones.Length; i++)
                {
                    Transform bone = _stmBones[i];
                    if (bone == null || bone.parent != _stmRoot || !Finite(bone.localPosition) ||
                        !Finite(new Vector3(bone.localRotation.x, bone.localRotation.y, bone.localRotation.z)) || !PoseMarker.Finite(bone.localRotation.w))
                        throw new InvalidOperationException("STM root binding/value changed");
                    Vector3 position = pivot + UnityVector(StmPresentation.Rotate(CoreVector(bone.localPosition - pivot), CoreVector(axis), degrees));
                    _stmValues[i * 2] = Position(position);
                    _stmValues[i * 2 + 1] = Rotation(turn * bone.localRotation);
                }
                _stmFrame.Apply(_stmValues);
                if (!_stmApplied) { _stmApplied = true; Event("STM_BLEND native rise + rig-space tilt; " + StmScaleReport + "; restored before each native frame"); }
                _stmStatus = _state.Phase + " tilt=" + degrees.ToString("F1", CultureInfo.InvariantCulture) + "; pivot=firing hand; camera untouched";
            }
            catch (Exception e)
            {
                _motion.LatchFault("STM presentation failed: " + e.GetBaseException().Message);
                Release("STM visual error; no retry");
            }
        }
        private bool StmEntryReady() => _targetMarker != null && StartEventFired() && StmClipDenial().Length == 0;
        private void BeginStmReturn()
        {
            if (!_stmTrial || _learning || !_stmApplied || _stmBypass.Length != 0 || _stmFrame == null ||
                _stmFrame.Fault.Length != 0 || (_state.Phase != PosePhase.Held && _state.Phase != PosePhase.Playing) || StmFrameDenial().Length != 0) return;
            if (_stmBlend.BeginReturn(Time.unscaledTime)) Event("STM_RETURN_BLEND O close; native outro resumes immediately; duration=" + StmTiltBlend.ReturnSeconds);
        }
        private void TickStmReturn(bool allowed)
        {
            if (!PresentationReturning) return;
            string denial = !allowed ? "focus/disable/gameplay interruption" : StmFrameDenial();
            if (denial.Length != 0) { StopStmReturn(denial); return; }
            _stmBlend.Return(Time.unscaledTime);
            if (!PresentationReturning) Event("STM_RETURN_BLEND finished; native outro continues");
        }
        private void StopStmReturn(string reason)
        {
            if (PresentationReturning) Event("STM_RETURN_BLEND interrupted: " + reason);
            _stmBlend.Cancel();
        }
        // Before next native Update AND synchronously on every release/teardown path.
        private bool RestoreLegacyPresentationFrame()
        {
            if (_stmFrame == null) return true;
            bool restored = _stmFrame.Restore();
            if (_stmFrame.Fault.Length != 0)
            {
                _motion.LatchFault("STM " + _stmFrame.Fault);
                if (_stmFaultReported != _stmFrame.Fault)
                { _stmFaultReported = _stmFrame.Fault; Event("STM_FAULT " + _stmFrame.Fault); }
            }
            return restored;
        }
        private string StmReport => "stmTrial=" + _stmTrial + " calibration=" + _learning + " frameOwned=" + (_stmFrame?.Active == true) +
            " applied=" + _stmApplied + " weight=" + _stmBlend.Weight.ToString("F3", CultureInfo.InvariantCulture) +
            " returnBlend=" + PresentationReturning + " targetDegrees=" + _stmDegrees + " status=" + _stmStatus + " " + StmScaleReport + " fault=" + (_stmFrame?.Fault ?? "");
        private string StmScaleReport => "rigScale=" + _stmScale.ToString("F6") +
            " nativeDepth=" + (_stmNativeDepth?.ToString("F6", CultureInfo.InvariantCulture) ?? "unavailable");
        private void ClearStmBindings()
        {
            if (_stmFrame?.Active == true) return;
            _stmBlend.Cancel();
            _stmFrame = null; _stmBones = Array.Empty<Transform>(); _stmRoot = _stmPivot = null; _stmCamera = null;
        }
    }
}
