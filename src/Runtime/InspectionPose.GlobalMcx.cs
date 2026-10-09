using System;
using Tylevo.FieldAttachments.Core;
using UnityEngine;

namespace Tylevo.FieldAttachments.Runtime
{
    public sealed partial class InspectionPose
    {
        public bool GlobalMcxInspectionEnabled { get; set; } = true;
        private readonly McxInspectionDonor _mcxDonor = new McxInspectionDonor();
        private AnimationClip? _mcxDonorClip;
        private PoseMarker? _mcxReference;
        private bool _mcxGlobalAttempted, _mcxOverride, _mcxClipAwaiting;
        private int _mcxAppliedFrame;
        private string _mcxGlobalStatus = "not requested";

        // Prepare assets before starting native inspection; disk loading cannot eat its early replacement window.
        private void PrepareGlobalMcx(Animator animator, string template, bool learn)
        {
            _mcxDonorClip=null; _mcxReference=null; _mcxOverride=_mcxClipAwaiting=false;
            _mcxGlobalAttempted=true; _mcxGlobalStatus="not requested";
            if(!GlobalMcxInspectionEnabled || learn || _stmTrial || _customStm || template==McxInspectionSegment.Template) return;
            string denial="";
            if(_idleHash==0 || !animator.HasState(1,PoseMarker.SharedState)) denial="stable native Hands idle/inspection state unavailable";
            else
            {
                _mcxReference=GlobalMcxInspection.ReferenceMarker(_markers.Values);
                if(_mcxReference==null) denial="unique saved late native MCX marker unavailable";
                else
                {
                    string missing=GlobalMcxInspection.MissingRigPath(path=>animator.transform.Find(path)!=null);
                    if(missing.Length!=0) denial="missing presentation rig path: "+missing;
                    else { _mcxDonorClip=_mcxDonor.Get(); if(_mcxDonorClip==null) denial=_mcxDonor.Status; }
                }
            }
            _mcxGlobalAttempted=denial.Length!=0;
            _mcxGlobalStatus=denial.Length==0 ? "prepared; waiting for owned native inspection" : "native fallback: "+denial;
            Event("MCX_GLOBAL "+_mcxGlobalStatus);
        }
        private bool TickGlobalMcx()
        {
            if(_mcxClipAwaiting)
            {
                if(Time.frameCount<=_mcxAppliedFrame) return false;
                if(!McxClipMatches() || !OwnedOperation() || !StartEventFired() ||
                    _animator!.GetCurrentAnimatorStateInfo(1).fullPathHash!=PoseMarker.SharedState || _animator.IsInTransition(1))
                { McxEntryFault("global MCX clip read-back uncertain"); return false; }
                _mcxClipAwaiting=false;
                _mcxGlobalStatus="clip confirmed on next frame; MCX entry permitted";
                Event("MCX_GLOBAL "+_mcxGlobalStatus);
            }
            if(_mcxGlobalAttempted || _state.Phase!=PosePhase.Playing) return true;
            if(!GlobalMcxInspectionEnabled) { _mcxGlobalAttempted=true; return true; }
            if(_animator!.GetCurrentAnimatorStateInfo(1).fullPathHash!=PoseMarker.SharedState ||
                !_state.CanHold(OwnedOperation(),StartEventFired(),!_animator.IsInTransition(1))) return false;
            _mcxGlobalAttempted=true; // One application attempt per explicit activation; no donor/MDR retry.
            string eventDenial=ReadInspectionEvents(out var events,false) ? GlobalMcxInspection.EventDenial(events,_mcxReference!.Time) : "target inspection event table unreadable/outside bounds";
            if(eventDenial.Length!=0)
            {
                _mcxGlobalStatus="native fallback: "+eventDenial;
                Event("MCX_GLOBAL "+_mcxGlobalStatus); return true;
            }
            Event("MCX_GLOBAL validated "+events.Count+" native inspection events (start and named audio only)");
            bool applied=_motion.TryApply(_animator,_template,_read.Get(_read.Get(_hands,"FirearmsAnimator"),"Animator")!,_mcxDonorClip!);
            _controller=_animator.runtimeAnimatorController;
            _motionOutcome=_mcxGlobalStatus=_motion.Status; Event("MCX_GLOBAL "+_mcxGlobalStatus);
            if(!applied)
            {
                if(_motion.Active || _motion.Fault.Length!=0) { Release("MCX clip restore pending or uncertainty"); return false; }
                return true; // Original saved/native marker stays selected after a rejection with no mutation.
            }
            _mcxOverride=_mcxClipAwaiting=true; _mcxAppliedFrame=Time.frameCount;
            _targetMarker=GlobalMcxInspection.Retarget(_mcxReference!,_template,_controllerKey);
            _timingSource="global MCX saved late segment";
            _mcxStatus="global clip applied; awaiting evaluation before seek";
            return false;
        }
    }
}
