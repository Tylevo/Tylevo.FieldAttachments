using System;
using System.Globalization;
using System.IO;
using Tylevo.FieldAttachments.Core;
using UnityEngine;

namespace Tylevo.FieldAttachments.Runtime
{
    public sealed partial class InspectionPose
    {
        private readonly StandalonePoseTimeline _standaloneClock = new StandalonePoseTimeline();
        private readonly StandalonePosePlayback _standalonePlayback = new StandalonePosePlayback();
        private bool _standaloneBlocked, _standaloneEnabled = true;
        private float _standaloneNativeSpeed;
        private string _standaloneFault = "", _standaloneOutcome = "not opened";
        public bool Interrupted { get; private set; }

        // Both ordinary and Shift-modified attachment opens take this route.
        // No native inspection, clip/controller swap, speed lease or IK hook.
        public void Start(RaidSnapshot snapshot, bool learn)
        {
            if (Active) return;
            Interrupted = false;
            string requestedTemplate = "", requestedController = "";
            try
            {
                requestedTemplate = ReadAccess.Text(_read.Get(snapshot.Weapon, "TemplateId"));
                if (!CustomStmAnimationEnabled) throw new InvalidOperationException("authored attachment animation disabled");
                if (_probe.Session.Busy || _probe.Session.Blocked) throw new InvalidOperationException("inventory request pending/unknown");
                LocalSessionFacts facts = _probe.ReadContext(snapshot);
                if (!facts.Allowed) throw new InvalidOperationException(facts.Denial);
                string denial = _probe.ReadHands(snapshot).Denial;
                if (denial.Length != 0) throw new InvalidOperationException(denial);
                object? hands = _read.Get(snapshot.Player, "HandsController");
                Animator? animator = ResolveAnimator(hands);
                if (animator == null || StableIdleHash(hands, animator) == 0)
                    throw new InvalidOperationException("wait for the equipped weapon to reach visible idle");
                if (ReferenceEquals(_read.Get(_read.Get(snapshot.Player, "BodyAnimatorCommon"), "_animator"), animator))
                    throw new InvalidOperationException("weapon and body animator are shared");
                string template = requestedTemplate;
                requestedController = animator.runtimeAnimatorController.name;
                if (!CustomStmInspection.ControllerMatches(template, animator.runtimeAnimatorController.name))
                    throw new InvalidOperationException("no verified authored pose for this weapon/controller");
                _context = snapshot; _hands = hands; _operation = _read.Get(hands, "CurrentOperation");
                _animator = animator; _controller = animator.runtimeAnimatorController;
                _template = template; _controllerKey = _controller.name + "/" + animator.name;
                _standaloneNativeSpeed = animator.speed; _standaloneEnabled = true;
                denial = StandaloneDenial();
                if (denial.Length != 0) throw new InvalidOperationException(denial);
                AnimationClip? clip = _customStmAsset.Get(Path.GetDirectoryName(_path)!, template);
                if (clip == null) throw new InvalidOperationException(_customStmAsset.Status);
                _standalonePlayback.Prepare(animator, clip, template);
                PrepareSupportPresentation(snapshot, animator);
                _standaloneClock.Start(Time.unscaledTime);
                if (!_standaloneClock.Active) throw new InvalidOperationException(_standaloneClock.Fault);
                _standaloneOutcome = "playing " + clip.name;
                Status = "POSE: standalone attachment animation; aim or fire to cancel.";
                Event("STANDALONE_START weapon=" + snapshot.WeaponId + " template=" + template +
                    " clip=" + clip.name + " bindings=" + _standalonePlayback.BindingCount +
                    " supportRenderers=" + _supportPresentation.RendererCount + " supportBones=" + _supportPresentation.BoneCount +
                    " firingHandPresentation=" + _authoredFiringHand +
                    " nativeInspectInvoked=false nativeSpeed=" + _standaloneNativeSpeed +
                    " shift=" + learn + " (same standalone route)");
            }
            catch (Exception e)
            {
                _standaloneClock.Abort();
                FinishStandalone();
                Interrupted = true;
                Status = "POSE unavailable: " + e.GetBaseException().Message;
                Event(Status + "; template=" + requestedTemplate + " controller=" + requestedController + "; no native fallback");
            }
        }

        private string StandaloneDenial()
        {
            if (!_standaloneEnabled || !CustomStmAnimationEnabled) return "attachment animation disabled";
            if (!Application.isFocused) return "focus lost";
            if (!SameLiveAnimator()) return "player/weapon/animator context changed";
            if (!ReferenceEquals(_read.Get(_hands, "CurrentOperation"), _operation) ||
                _operation?.GetType().FullName != HandsReadiness.IdleOperation) return "native hands operation changed";
            if (!NeutralHands()) return "native hands, sprint, view or inventory changed";
            if (_animator!.speed != _standaloneNativeSpeed) return "native playback speed changed";
            if (_standalonePlayback.Fault.Length != 0 && _standalonePlayback.Prepared) return "presentation restoration fault";
            if (_supportPresentation.Fault.Length != 0 && _supportPresentation.Prepared) return "support hand restoration fault";
            return SupportPresentationDenial();
        }

        public void Tick(bool requested, bool enabled, bool interrupt)
        {
            _standaloneEnabled = enabled;
            if (!Active) return;
            try
            {
                if (!_standaloneClock.Active) { FinishStandalone(); return; }
                string denial = interrupt ? "gameplay/menu input" : StandaloneDenial();
                if (denial.Length != 0) { Release(denial); return; }
                if (!requested && !_standaloneClock.Returning) { Release("attachment key released", true); return; }
                _standaloneClock.Tick(Time.unscaledTime);
                if (_standaloneClock.Fault.Length != 0)
                { Interrupted = true; _standaloneOutcome = _standaloneClock.Fault; }
                if (!_standaloneClock.Active) FinishStandalone();
            }
            catch (Exception e) { Release("pose observation failed: " + e.GetBaseException().Message); }
        }

        public void ApplyPresentationFrame()
        {
            if (!_standaloneClock.Active) return;
            try
            {
                string denial = StandaloneDenial();
                if (denial.Length != 0) { Release(denial); return; }
                _standaloneClock.Tick(Time.unscaledTime);
                if (!_standaloneClock.Active) { FinishStandalone(); return; }
                _standalonePlayback.Apply(_standaloneClock.SampleTime, _standaloneClock.Blend);
                _supportPresentation.Apply(_standaloneClock.Blend);
            }
            catch (Exception e) { Release("presentation failed: " + e.GetBaseException().Message); }
        }

        public bool RestorePresentationFrame()
        {
            bool supportRestored = _supportPresentation.Restore();
            bool restored = _standalonePlayback.Restore();
            ObserveStandaloneFault();
            return supportRestored && restored;
        }

        private void ObserveStandaloneFault()
        {
            string fault = _standalonePlayback.Prepared ? _standalonePlayback.Fault : "";
            if (_supportPresentation.Prepared && _supportPresentation.Fault.Length != 0)
                fault += (fault.Length == 0 ? "" : "; ") + "support hand: " + _supportPresentation.Fault;
            if (fault.Length == 0) return;
            _standaloneBlocked = true; Interrupted = true; _standaloneClock.Abort();
            if (_standaloneFault != fault)
            {
                _standaloneFault = fault;
                Event("STANDALONE_FAULT " + _standaloneFault + "; inventory blocked, gameplay input unchanged");
            }
            Status = "POSE FAULT: presentation ownership uncertain; see F10 report before retrying.";
        }

        public void Release(string reason, bool normalKeyRelease = false)
        {
            if (!Active) return;
            bool normal = normalKeyRelease && _standaloneClock.Active;
            try { normal = normal && StandaloneDenial().Length == 0; }
            catch { normal = false; }
            RestorePresentationFrame();
            if (normal && !_standaloneBlocked)
            {
                _standaloneClock.Close(Time.unscaledTime);
                _standaloneOutcome = "custom return: " + reason;
                Status = "POSE: returning from the standalone animation.";
            }
            else
            {
                _standaloneClock.Abort(); Interrupted = true;
                _standaloneOutcome = "cancelled: " + reason;
            }
            if (!_standaloneClock.Active) FinishStandalone();
        }

        private void FinishStandalone()
        {
            if (_standaloneClock.Fault.Length != 0)
            { Interrupted = true; _standaloneOutcome = _standaloneClock.Fault; }
            bool hadRig = _standalonePlayback.Prepared || _supportPresentation.Prepared;
            // Observe the fault before Release clears successfully restored bindings.
            RestorePresentationFrame();
            bool supportReleased = _supportPresentation.Release();
            bool playbackReleased = _standalonePlayback.Release();
            if (!supportReleased || !playbackReleased)
            { Status = "POSE RESTORE WAIT: presentation cleanup pending; inventory blocked."; return; }
            ClearSupportPresentationBinding();
            _context = null; _hands = _operation = null; _animator = null; _controller = null;
            if (hadRig) Event("STANDALONE_END " + _standaloneOutcome + "; nativeInspectInvoked=false frameOwned=false");
            if (!_standaloneBlocked) Status = "POSE released. Native gameplay is available.";
        }

        public void Mark()
        { if (!_standaloneBlocked) Status = "POSE: standalone authored animation; native inspect calibration is inactive."; }

        public string Report() => Status + "\nstandalone=true nativeInspectInvoked=false nativeClipReplaced=false nativeSpeedOwned=false" +
            "\nactive=" + Active + " held=" + Held + " returning=" + PresentationReturning +
            " sampleSeconds=" + _standaloneClock.SampleTime.ToString("F4", CultureInfo.InvariantCulture) +
            " blend=" + _standaloneClock.Blend.ToString("F4", CultureInfo.InvariantCulture) +
            " bindings=" + _standalonePlayback.BindingCount + " frameOwned=" + _standalonePlayback.FrameActive +
            "\nsupportRenderers=" + _supportPresentation.RendererCount + " supportBones=" + _supportPresentation.BoneCount +
            " firingHandPresentation=" + _authoredFiringHand +
            " supportFrameOwned=" + _supportPresentation.FrameActive + " supportFault=" + _supportPresentation.Fault +
            "\ntemplate=" + _template + " controller=" + _controllerKey + " outcome=" + _standaloneOutcome +
            "\ntimelineFault=" + _standaloneClock.Fault + " restorationFault=" + _standaloneFault +
            " inventoryBlocked=" + _standaloneBlocked + "\n" + Layers();
    }
}
