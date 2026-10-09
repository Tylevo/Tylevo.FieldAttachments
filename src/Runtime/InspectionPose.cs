using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Tylevo.FieldAttachments.Core;
using UnityEngine;

namespace Tylevo.FieldAttachments.Runtime
{
    // Public entry points use standalone presentation only. Legacy helpers remain
    // private during migration and have no entry point from attachment mode.
    public sealed partial class InspectionPose
    {
        private const string Utility = "EFT.Player+FirearmController+UtilityOperation";
        private readonly ReadAccess _read;
        private readonly RaidReader _reader;
        private readonly NativeInstallProbe _probe;
        private readonly string _path;
        private readonly Dictionary<string, PoseMarker> _markers = new Dictionary<string, PoseMarker>();
        private readonly InspectionPoseState _state = new InspectionPoseState();
        private readonly PoseSpeedLease _speed = new PoseSpeedLease();
        private readonly PoseIdleReturn _idleReturn = new PoseIdleReturn();
        private readonly MdrInspectionMotion _motion = new MdrInspectionMotion();
        private readonly PoseCleanupCheck _cleanup = new PoseCleanupCheck();
        private RaidSnapshot? _context;
        private object? _hands, _operation;
        private Animator? _animator;
        private RuntimeAnimatorController? _controller;
        private int[] _initialStates = Array.Empty<int>();
        private string _template = "", _controllerKey = "", _restoreError = "";
        private bool _learning;
        private PoseMarker? _targetMarker;
        private int _idleHash;
        private MethodInfo? _cancelInspect;
        private string _timingSource = "none";
        private string _motionOutcome = "native motion";
        private bool _motionAttempted, _motionRestoreReported;
        public bool ShortReturnEnabled { get; set; } = true;
        public bool MdrMotionEnabled { get; set; } = true;
        public bool Active => _standaloneClock.Active || _standalonePlayback.Prepared || _standalonePlayback.FrameActive ||
            _supportPresentation.Prepared || _supportPresentation.FrameActive || _standaloneBlocked;
        public bool Held => _standaloneClock.Held;
        public string Status { get; private set; } = "POSE: attachment mode uses an independent authored animation.";
        public InspectionPose(ReadAccess read, RaidReader reader, NativeInstallProbe probe, string root)
        {
            _read = read; _reader = reader; _probe = probe; _path = Path.Combine(root, "PoseMarkers.txt");
            try
            {
                if (File.Exists(_path)) foreach (string line in File.ReadLines(_path).Take(64))
                { PoseMarker? marker = PoseMarker.Decode(line); if (marker != null) _markers[Key(marker.WeaponTemplate, marker.Controller)] = marker; }
            }
            catch (Exception e) { Event("MARKERS unavailable: " + e.GetBaseException().Message); }
        }
        private static string Key(string template, string controller) => template + "|" + controller;
        private void Event(string message) { _probe.RecordUi("POSE " + message); }
        private bool OwnedOperation() => _context != null && ReferenceEquals(_read.Get(_context.Player, "HandsController"), _hands) &&
            ReferenceEquals(_read.Get(_hands, "Item"), _context.Weapon) && ReferenceEquals(_read.Get(_hands, "CurrentOperation"), _operation) &&
            _operation?.GetType().FullName == Utility && ReadAccess.Text(_read.Get(_operation, "_utilityOperationType")) == "ExamineWeapon";
        private bool StartEventFired() => ReadAccess.Text(_read.Get(_operation, "State")) == "Ready";
        private Animator? ResolveAnimator(object? hands)
        {
            object? wrapper = _read.Get(_read.Get(hands, "FirearmsAnimator"), "Animator");
            // FastAnimator's speed setter is not proven to control playback. Fail closed.
            return wrapper?.GetType().FullName == "AnimationSystem.UnityAnimatorWrapper" ? _read.Get(wrapper, "_animator") as Animator : null;
        }
        private bool? NativeSprint() => NativeSprintReader.Read(_read, _context?.Player);
        private void StartLegacy(RaidSnapshot snapshot, bool learn)
        {
            if (Active) return;
            try
            {
                if (_probe.Session.Busy || _probe.Session.Blocked) throw new InvalidOperationException("inventory request pending/unknown");
                LocalSessionFacts context = _probe.ReadContext(snapshot);
                Event("CONTEXT " + context.Evidence);
                if (!context.Allowed) throw new InvalidOperationException(context.Denial);
                string denial = _probe.ReadHands(snapshot).Denial;
                if (denial.Length != 0) throw new InvalidOperationException(denial);
                object? hands = _read.Get(snapshot.Player, "HandsController");
                if (ReadAccess.Text(_read.Get(snapshot.Player, "PointOfView")) != "FirstPerson" ||
                    ReadAccess.Text(_read.Get(_read.Get(snapshot.Weapon, "MalfState"), "State")) != "None")
                    throw new InvalidOperationException("requires first person and no weapon malfunction");
                Animator? animator = ResolveAnimator(hands);
                if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null ||
                    animator.layerCount < 1 || animator.layerCount > 16 || !PoseMarker.Finite(animator.speed) || animator.speed <= 0)
                    throw new InvalidOperationException("supported live weapon animator unavailable/already paused");
                if (ReferenceEquals(_read.Get(_read.Get(snapshot.Player, "BodyAnimatorCommon"), "_animator"), animator))
                    throw new InvalidOperationException("weapon and body animator are shared; pose hold refused");
                bool? sprinting = NativeSprintReader.Read(_read, snapshot.Player);
                if (!sprinting.HasValue) throw new InvalidOperationException("native player sprint state unavailable (Physical.Sprinting)");
                if (sprinting.Value) throw new InvalidOperationException("stop sprinting, then release and press F5 again");
                string template = ReadAccess.Text(_read.Get(snapshot.Weapon, "TemplateId"));
                if (template.Length == 0) throw new InvalidOperationException("weapon template unavailable");
                _mcxNative = CustomStmInspection.NativeMcxRequested(McxLateInspectionEnabled || GlobalMcxInspectionEnabled,
                    CustomStmAnimationEnabled, template, learn);
                _mcxEntry.Reset(); _mcxStatus = _mcxNative ? "native MCX segment selected" : "not requested";
                string controllerKey = animator.runtimeAnimatorController.name + "/" + animator.name;
                _markers.TryGetValue(Key(template, controllerKey), out PoseMarker mcxSaved);
                PrepareCustomStm(animator, template, learn);
                PrepareSupportHand(snapshot.Player!, animator);
                PrepareStm(snapshot, animator, template, controllerKey, mcxSaved, learn);
                if (_mcxNative && !learn)
                {
                    string markerDenial = McxInspectionSegment.MarkerDenial(mcxSaved, template, controllerKey);
                    if (markerDenial.Length != 0) throw new InvalidOperationException(markerDenial);
                    if (animator.runtimeAnimatorController.name != McxInspectionSegment.Controller)
                        throw new InvalidOperationException("native MCX controller name changed");
                }
                MethodInfo? examine = hands!.GetType().GetMethod("ExamineWeapon", BindingFlags.Instance | BindingFlags.Public,
                    null, Type.EmptyTypes, null);
                if (examine == null || examine.ReturnType != typeof(bool)) throw new InvalidOperationException("native ExamineWeapon signature changed");
                _initialStates = Enumerable.Range(0, animator.layerCount).Select(i => animator.GetCurrentAnimatorStateInfo(i).fullPathHash).ToArray();
                // Capture the verified native idle target BEFORE starting our inspect.
                // No guessed hash and no use of an idle from another weapon/controller.
                _idleHash = StableIdleHash(hands, animator);
                if (!learn && (_mcxNative || !GlobalMcxInspectionEnabled && MdrMotionEnabled && MdrMotionTrial.Supports(template)) && _idleHash == 0)
                    throw new InvalidOperationException("MDR trial requires stable native idle before inspect; observed=" +
                        (animator.layerCount > 1 ? animator.GetCurrentAnimatorStateInfo(1).fullPathHash : 0));
                if ((_stmTrial || _customStm) && !learn && _idleHash == 0) throw new InvalidOperationException("STM trial requires stable native Hands idle");
                PrepareGlobalMcx(animator, template, learn);
                _cancelInspect = hands.GetType().GetMethod("SetTriggerPressed", BindingFlags.Instance | BindingFlags.Public,
                    null, new[] { typeof(bool) }, null);
                if (_cancelInspect?.ReturnType != typeof(void)) _cancelInspect = null;
                if (!(examine.Invoke(hands, null) is bool accepted) || !accepted)
                    throw new InvalidOperationException("native inspection refused the request");
                _context = snapshot; _hands = hands; _operation = _read.Get(hands, "CurrentOperation");
                _animator = animator; _controller = animator.runtimeAnimatorController;
                _template = template; _controllerKey = _controller.name + "/" + animator.name;
                if (!OwnedOperation()) throw new InvalidOperationException("native call did not enter a distinct inspection utility operation");
                _state.Start(Time.unscaledTime); _learning = learn;
                _motionAttempted = learn || _customStm || _mcxNative || GlobalMcxInspectionEnabled || !MdrMotionEnabled || !MdrMotionTrial.Supports(template);
                _motionOutcome = _motionAttempted ? "native motion" : "MDR trial awaiting native inspection state";
                _markers.TryGetValue(Key(_template, _controllerKey), out PoseMarker saved);
                _targetMarker = PoseMarker.Resolve(saved, _template, _controllerKey,
                    animator.layerCount > 1 && animator.GetLayerName(1) == "Hands" && animator.HasState(1, PoseMarker.SharedState), learn);
                _timingSource = _targetMarker == null ? "manual" : ReferenceEquals(saved, _targetMarker) ? "saved override" : "shared 0.27";
                if (_mcxNative && !learn) _timingSource = "MCX late saved segment";
                Status = _targetMarker != null ? "POSE: moving to " + _timingSource + " angle; release F5 to return." :
                    "POSE: inspecting. Tap F4 at the wanted angle to hold/save it; release F5 to return.";
                Event("START weapon=" + snapshot.WeaponId + " template=" + _template + " animator=" + _controllerKey +
                    " timing=" + _timingSource + " nativeIdle=" + _idleHash);
            }
            catch (Exception e)
            {
                ClearSupportHand();
                Release("start rejected");
                if (!Active) { _context = null; _hands = _operation = null; _animator = null; _controller = null; ClearStmBindings(); }
                Status = "POSE unavailable: " + e.GetBaseException().Message; Event(Status);
            }
        }
        private void TickLegacy(bool requested, bool enabled, bool interrupt)
        {
            if (!Active) return;
            try
            {
                if (_state.Phase == PosePhase.Returning)
                {
                    TickStmReturn(enabled && StmPresentationEnabled && Application.isFocused && !interrupt);
                    FinishReturn(enabled && Application.isFocused && !interrupt); return;
                }
                string reason = !enabled ? "plugin disabled" : _customStm && !CustomStmAnimationEnabled ? "custom STM disabled" : _stmTrial && !StmPresentationEnabled ? "STM trial disabled" : _mcxOverride && !GlobalMcxInspectionEnabled ? "global MCX disabled" :
                    !_customStm && !_mcxOverride && !MdrMotionEnabled && _motion.Active ? "MDR trial disabled" : !Application.isFocused ? "focus lost" :
                    interrupt ? "gameplay/menu input" : !requested ? "F5 released" : _state.Deadline(Time.unscaledTime);
                if (reason.Length == 0 && (_context == null || !_probe.ReadContext(_context).Allowed)) reason = "local raid/range/player context changed";
                if (reason.Length == 0 && NativeSprint() != false)
                    reason = NativeSprint() == true ? "native sprint" : "native sprint state unavailable";
                if (reason.Length == 0 && (!OwnedOperation() || _animator == null || ResolveAnimator(_hands) != _animator ||
                    _animator.runtimeAnimatorController != _controller)) reason = "inspection/weapon/animator changed or completed";
                if (reason.Length == 0)
                {
                    HandsReadiness hands = _probe.ReadHands(_context!);
                    if (hands.InventoryOpened != false || hands.Aiming != false || hands.TriggerPressed != false ||
                        hands.InventoryLocked != false || hands.HasActiveEvents != false) reason = "hands/inventory changed";
                }
                if (reason.Length == 0 && _state.Phase == PosePhase.Held &&
                    (_animator!.speed != 0 || !StartEventFired())) reason = "external animator/inspection change";
                if (reason.Length != 0) { Release(reason, reason == "F5 released"); return; }
                if (!TickCustomStm() || !TickGlobalMcx() || !TickMcxEntry()) return;
                if (!MdrMotionEnabled) _motionAttempted = true;
                if (!_motionAttempted && _state.Phase == PosePhase.Playing && _animator!.layerCount > 1 &&
                    _animator.GetCurrentAnimatorStateInfo(1).fullPathHash == PoseMarker.SharedState &&
                    _state.CanHold(OwnedOperation(), StartEventFired(), !_animator.IsInTransition(1)))
                {
                    _motionAttempted = true; // One attempt per explicit F5, including missing donor/rejection.
                    bool applied = _motion.TryApply(_animator, _template, _read.Get(_read.Get(_hands, "FirearmsAnimator"), "Animator")!);
                    _controller = _animator.runtimeAnimatorController;
                    _motionOutcome = _motion.Status; Event("MOTION " + _motionOutcome);
                    if ((_motion.Active || _motion.Fault.Length != 0) && !applied) { Release("MDR clip restore pending or trial fault"); return; }
                    if (applied)
                    {
                        // A native MCX/RD marker belongs to a different clip. Never overwrite it.
                        _targetMarker = new PoseMarker { WeaponTemplate = _template, Controller = _controllerKey,
                            Layer = 1, State = PoseMarker.SharedState, Time = MdrMotionTrial.HoldTime };
                        _timingSource = "MDR motion 0.2756";
                    }
                    Status = applied ? "POSE: MDR motion trial; release F5 to return." : "POSE: native motion fallback; see F10 for reason.";
                }
                PoseMarker? marker = _targetMarker;
                if (!_learning && _state.Phase == PosePhase.Playing && marker != null &&
                    marker.Layer < _animator!.layerCount)
                {
                    AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(marker.Layer);
                    if (marker.Matches(_template, _controllerKey, marker.Layer, state.fullPathHash) && marker.Reached(state.normalizedTime) &&
                        _state.CanHold(OwnedOperation(), StartEventFired(), !_animator.IsInTransition(marker.Layer))) Hold(marker, false);
                }
            }
            catch (Exception e)
            {
                if (_customStmAwaiting || _customStmReturn.Pending) CustomStmFault("custom observation uncertain: " + e.GetBaseException().Message);
                else if (_mcxClipAwaiting || _mcxEntry.Pending) McxEntryFault("MCX observation uncertain: " + e.GetBaseException().Message);
                else Release("pose observation error: " + e.GetBaseException().Message);
            }
        }
        private void MarkLegacy()
        {
            if (_state.Phase != PosePhase.Playing) return;
            if (_motion.Active)
            {
                Status = "POSE: donor motion uses its reference angle. Shift + attachment key bypasses it for native F4 calibration.";
                Event("MARK skipped during donor motion; native saved marker preserved"); return;
            }
            try
            {
                if (_animator == null || !_state.CanHold(OwnedOperation(), StartEventFired(), true))
                { Status = "POSE: native inspection-start event not ready; mark later in the inspection."; Event("MARK rejected: start event not ready"); return; }
                // Pick an active, changed, non-looping timeline within THIS owned inspection.
                // Record the full layer/state hash and normalized time; never guess frame counts.
                for (int layer = 0; layer < _animator.layerCount; layer++)
                {
                    AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(layer);
                    if (state.loop || state.fullPathHash == _initialStates[layer] || state.fullPathHash == 0 ||
                        !PoseMarker.Finite(state.normalizedTime) || state.normalizedTime <= 0 || state.normalizedTime >= 0.95f || _animator.IsInTransition(layer) ||
                        (layer != 0 && _animator.GetLayerWeight(layer) < 0.5f)) continue;
                    Hold(new PoseMarker { WeaponTemplate = _template, Controller = _controllerKey, Layer = layer,
                        State = state.fullPathHash, Time = state.normalizedTime }, true);
                    return;
                }
                Status = "POSE: no stable inspection timeline yet. Release F5, try again and mark after the turn starts.";
                Event("MARK rejected: no stable changed non-looping state | " + Layers());
            }
            catch (Exception e) { Release("mark error: " + e.GetBaseException().Message); }
        }
        private void Hold(PoseMarker marker, bool save)
        {
            Animator owned = _animator!;
            if (_motion.Active && !_motion.IsPlaying(owned))
            {
                Event("MOTION hold rejected: donor clip playback not confirmed");
                Release("MDR clip playback not confirmed"); return;
            }
            _speed.Acquire(() => owned.speed, value => owned.speed = value);
            _state.Held(Time.unscaledTime);
            if (McxSegmentActive) _mcxStatus = "held MCX; closing plays remaining outro";
            Status = (_customStm ? "POSE HELD [CUSTOM STM]" : _mcxOverride ? "POSE HELD [GLOBAL MCX]" : _motion.Active ? "POSE HELD [MDR TRIAL]" : "POSE HELD [NATIVE]") +
                ": release F5 to return. F7 releases pose only; press again when hands are ready.";
            Event("HELD " + marker.Encode() + " priorSpeed=" + _speed.PreviousSpeed + " | " + Layers());
            if (_customStm) Event("HELD " + SupportHandReport);
            if (!save) return;
            _markers[Key(_template, _controllerKey)] = marker;
            try
            {
                string temp = _path + ".tmp";
                File.WriteAllLines(temp, _markers.Values.Take(64).Select(m => m.Encode()));
                if (File.Exists(_path)) File.Replace(temp, _path, null); else File.Move(temp, _path);
                Event("MARK saved for this weapon template/animator");
            }
            catch (Exception e) { Event("MARK held, save failed: " + e.GetBaseException().Message); Status += " Pose save failed; session only."; }
        }
        private void ReleaseLegacy(string reason, bool normalKeyRelease = false)
        {
            if (!normalKeyRelease) _customStmReturn.Cancel();
            if (!normalKeyRelease) StopStmReturn(reason);
            if (!RestorePresentationFrame())
            { Status = "POSE RESTORE WAIT: STM presentation restore pending; inventory blocked."; return; }
            if (!Active) return;
            if (_state.Phase == PosePhase.Returning && !_speed.Active) { FinishReturn(normalKeyRelease && Application.isFocused); return; }
            if (normalKeyRelease) BeginStmReturn();
            BeginCustomStmReturn(normalKeyRelease);
            bool shortReturn = normalKeyRelease && _state.Phase == PosePhase.Held && _speed.Active && _animator != null && _animator.speed == 0;
            if (McxSegmentActive) shortReturn = false; // Keep the MCX outro on native and compatible receiving weapons.
            if (_stmTrial) shortReturn = false; // Keep native playback/events; only ease the visual offset.
            if (_customStm) shortReturn = false; // Finish authored exit before verified idle blend.
            if (_state.Phase != PosePhase.Returning) _mcxReturnStarted = Time.unscaledTime;
            _mcxEntry.Cancel();
            _state.Returning();
            if (_animator == null) _speed.ForgetDestroyedAnimator();
            if (!_speed.TryRelease())
            {
                Status = "POSE RESTORE WAIT: retrying original animator speed; inventory requests blocked.";
                if (_restoreError != reason) { _restoreError = reason; Event("RESTORE failed: " + reason); }
                return;
            }
            _restoreError = "";
            Status = "POSE RETURNING: native inspection continues; wait for hands ready. No inventory action queued.";
            Event("RELEASE " + reason + " previous speed restored (or external nonzero speed retained)");
            if (_stmApplied && !PresentationReturning) { Event("STM_RELEASE visual tilt removed before native outro"); _stmApplied = false; _stmStatus = "visual roots restored; native outro"; }
            if (shortReturn) TryShortReturn();
            if (!_customStmReturn.Pending) FinishReturn();
        }
        private bool SameLiveAnimator() => _context != null && _animator != null && _animator.isActiveAndEnabled &&
            ReferenceEquals(_reader.MainPlayer(), _context.Player) &&
            ReferenceEquals(_read.Get(_context.Player, "HandsController"), _hands) &&
            ReferenceEquals(_read.Get(_hands, "Item"), _context.Weapon) && ResolveAnimator(_hands) == _animator &&
            _animator.runtimeAnimatorController == _controller &&
            _probe.ReadContext(_context).Allowed;
        private bool NeutralHands()
        {
            HandsReadiness hands = _probe.ReadHands(_context!);
            return hands.SameWeapon && hands.InventoryOpened == false && hands.Aiming == false && hands.TriggerPressed == false &&
                hands.InventoryLocked == false && hands.HasActiveEvents == false &&
                NativeSprint() == false &&
                ReadAccess.Text(_read.Get(_context!.Player, "PointOfView")) == "FirstPerson" &&
                ReadAccess.Text(_read.Get(_read.Get(_context.Weapon, "MalfState"), "State")) == "None";
        }
        private void TryShortReturn(bool authoredReturn = false)
        {
            try
            {
                // Authored clips end at idle before the native inspect timeline ends.
                // Their completion is independent of the optional native-inspect shortcut.
                if (!ShortReturnEnabled && !authoredReturn) return;
                if (!SameLiveAnimator() || !OwnedOperation() || !StartEventFired() || !NeutralHands() ||
                    _idleHash == 0 || _cancelInspect == null || _animator!.layerCount <= 1 ||
                    (_customStm ? !CustomInspectionStateMatches() : _animator.GetCurrentAnimatorStateInfo(1).fullPathHash != PoseMarker.SharedState) || _animator.IsInTransition(1) ||
                    !_animator.HasState(1, _idleHash) || !PoseMarker.Finite(_animator.speed) || _animator.speed <= 0)
                { Event("SHORT_RETURN skipped: native idle/owned stable inspection not verified; continuing native inspect"); return; }
                _idleReturn.Begin(Time.unscaledTime, () => _animator.CrossFadeInFixedTime(_idleHash, PoseIdleReturn.BlendSeconds, 1, 0));
                Status = "POSE RETURNING: blending to native idle; inventory blocked until return is confirmed.";
                Event("SHORT_RETURN blend requested nativeIdle=" + _idleHash + " duration=" + PoseIdleReturn.BlendSeconds);
            }
            catch (Exception e) { Event("SHORT_RETURN blend uncertain; observing native playback, no retry: " + e.GetBaseException().Message); }
        }
        private void FinishReturn(bool mayCancel = false)
        {
            if (!RestorePresentationFrame()) return;
            if (_speed.Active) { Release("restore retry"); return; }
            if (WaitCustomStmReturn(mayCancel)) return;
            bool sameLiveContext = _context != null && _animator != null &&
                _probe.ReadContext(_context).Allowed;
            if (_idleReturn.Active && sameLiveContext && SameLiveAnimator() && NativeSprint() == true)
            {
                _idleReturn.End(); // Native SprintStateChangedEvent cancels inspection itself.
                Event("SHORT_RETURN yielded to native sprint; no extra blend/cancel");
            }
            if (_idleReturn.Active && sameLiveContext && SameLiveAnimator())
            {
                string operation = _read.Get(_hands, "CurrentOperation")?.GetType().FullName ?? "";
                if (OwnedOperation() || operation == HandsReadiness.IdleOperation)
                {
                    bool atIdle = _animator!.GetCurrentAnimatorStateInfo(1).fullPathHash == _idleHash && !_animator.IsInTransition(1);
                    try
                    {
                        if (_idleReturn.Complete(atIdle, OwnedOperation() && StartEventFired(), mayCancel && NeutralHands(),
                            pressed => {
                                Event("SHORT_RETURN native inspection cancel requested trigger=false after visible idle");
                                // Verified SPT 4.1.5: UtilityOperation cancels ExamineWeapon,
                                // then forwards false to Idling. No shot, inventory or direct State write.
                                _cancelInspect!.Invoke(_hands, new object[] { pressed });
                            }, () => _read.Get(_hands, "CurrentOperation")?.GetType().FullName == HandsReadiness.IdleOperation))
                            Event("SHORT_RETURN complete: visible idle and native Idling confirmed");
                    }
                    catch (Exception e) { Event("SHORT_RETURN cancel uncertain, no retry: " + e.GetBaseException().Message); }
                    if (_idleReturn.ReportTimeout(Time.unscaledTime))
                    {
                        Status = "POSE RETURN WAIT: idle not confirmed; inventory blocked. Native playback is running.";
                        Event("SHORT_RETURN timeout: observing only; no repeated blend/cancel");
                    }
                    if (_idleReturn.Active) return;
                }
                else Event("SHORT_RETURN yielded to changed native operation=" + operation);
            }
            if (McxSegmentActive && sameLiveContext && SameLiveAnimator())
            {
                bool owned = OwnedOperation();
                bool nativeIdle = _read.Get(_hands, "CurrentOperation")?.GetType().FullName == HandsReadiness.IdleOperation;
                bool visibleIdle = _animator!.GetCurrentAnimatorStateInfo(1).fullPathHash == _idleHash && !_animator.IsInTransition(1);
                if (owned || nativeIdle && !visibleIdle && NativeSprint() == false)
                {
                    if (Time.unscaledTime - _mcxReturnStarted > 6)
                    { _motion.LatchFault("MCX native outro did not reach visible/native idle; no retry"); RestoreMotion(); }
                    return;
                }
                if (nativeIdle && visibleIdle && _mcxStatus != "native outro completed; visible/native idle confirmed")
                { _mcxStatus = "native outro completed; visible/native idle confirmed"; Event("MCX_OUTRO " + _mcxStatus); }
            }
            if (sameLiveContext && OwnedOperation()) return; // Native events finish inspection; never force idle.
            bool hadMotion = _motion.Active;
            bool expectIdle = hadMotion && SameLiveAnimator() && _idleHash != 0 &&
                _read.Get(_hands, "CurrentOperation")?.GetType().FullName == HandsReadiness.IdleOperation;
            bool wasIdle = expectIdle && _animator!.GetCurrentAnimatorStateInfo(1).fullPathHash == _idleHash && !_animator.IsInTransition(1);
            if (hadMotion && !_motionRestoreReported) Event("MOTION before cleanup | " + Layers());
            if (!RestoreMotion()) return;
            if (expectIdle)
            {
                _cleanup.Begin(Time.frameCount, _idleHash, wasIdle, Time.unscaledTime);
                Event("MOTION cleanup check: " + _cleanup.Reason + " sprint=" + NativeSprint());
                Status = "POSE RETURNING: native clip restored; waiting for native presentation to settle.";
                return;
            }
            if (_cleanup.Active)
            {
                bool same = SameLiveAnimator();
                PoseCleanupResult result = _cleanup.Observe(Time.frameCount, same,
                    _read.Get(_hands, "CurrentOperation")?.GetType().FullName == HandsReadiness.IdleOperation,
                    same ? _animator!.GetCurrentAnimatorStateInfo(1).fullPathHash : 0,
                    same && _animator!.IsInTransition(1), same ? NativeSprint() : null, Time.unscaledTime);
                if (result == PoseCleanupResult.Pending) return;
                Event("MOTION post-cleanup=" + result + " reason=" + _cleanup.Reason + " sprint=" + NativeSprint() + " | " + Layers());
                if (result == PoseCleanupResult.Fault)
                { _motion.LatchFault(_cleanup.Reason); RestoreMotion(); return; }
            }
            _idleReturn.End();
            _state.End(); _context = null; _hands = _operation = null; _animator = null; _controller = null;
            ClearStmBindings();
            Status = "POSE released. Hold F5 for saved/shared angle; Shift+F5 then F4 adjusts it.";
            Event("RETURNED native inspection ended/controller changed; no queued inventory action");
        }
        private bool RestoreMotion()
        {
            ClearSupportHand();
            bool wasActive = _motion.Active;
            if (!_motion.TryRestore())
            {
                Status = _motion.Fault.Length == 0 ? "POSE RESTORE WAIT: native clip restore pending; inventory blocked." :
                    "POSE FAULT: restart the test session; pose/inventory actions blocked. " + _motion.Fault;
                if (!_motionRestoreReported) { _motionRestoreReported = true; Event("MOTION restore pending/fault: " + _motion.Fault); }
                return false;
            }
            if (wasActive) Event("MOTION " + _motion.Status);
            _motionRestoreReported = false; return true;
        }
        public void Shutdown(string reason)
        {
            Release(reason); // Synchronous: there may be no later Update.
        }
        private string Layers()
        {
            if (_animator == null) return "animator unavailable";
            return string.Join("; ", Enumerable.Range(0, _animator.layerCount).Select(i => {
                AnimatorStateInfo s = _animator.GetCurrentAnimatorStateInfo(i);
                return i + ":" + _animator.GetLayerName(i) + " state=" + s.fullPathHash + " time=" + s.normalizedTime.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) + " transition=" + _animator.IsInTransition(i);
            }));
        }
        private string LegacyReport() => Status + "\nphase=" + _state.Phase + " speedOwned=" + _speed.Active +
            " timing=" + _timingSource + " nativeIdle=" + _idleHash + " shortReturn=" + _idleReturn.Active +
            " cancelAttempted=" + _idleReturn.CancelAttempted + "\nmotion=" + _motionOutcome + " clipOwned=" + _motion.Active +
            " cleanupPending=" + _cleanup.Active + " cleanupReason=" + _cleanup.Reason + " nativeSprint=" + NativeSprint() +
            " motionFault=" + _motion.Fault + "\nmcxSegment=" + McxSegmentActive + " globalOverride=" + _mcxOverride + " consumed=" + _mcxEntry.Consumed +
            "\nmcxGlobal=" + _mcxGlobalStatus + "\nmcxDonor=" + _mcxDonor.Status +
            " pending=" + _mcxEntry.Pending + " status=" + _mcxStatus + "\n" + StmReport + "\n" + CustomStmReport + "\n" + SupportHandReport + "\n" + Layers();
    }
}
