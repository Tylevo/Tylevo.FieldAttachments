using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Tylevo.FieldAttachments.Core;
using UnityEngine;

namespace Tylevo.FieldAttachments.Runtime
{
    public sealed partial class InspectionPose
    {
        public bool McxLateInspectionEnabled { get; set; } = true;
        private readonly McxInspectionSegment _mcxEntry = new McxInspectionSegment();
        private bool _mcxNative;
        private float _mcxReturnStarted;
        private string _mcxStatus = "not requested";
        private bool McxSegmentActive => _mcxNative || _mcxOverride;

        private bool McxClipMatches()
        {
            var clips = _animator!.GetCurrentAnimatorClipInfo(1);
            return clips.Length == 1 && clips[0].weight >= 0.99f && clips[0].clip != null &&
                (_mcxOverride ? _motion.IsPlaying(_animator) : clips[0].clip.name == "mcx_look") && !clips[0].clip.legacy &&
                Math.Abs(clips[0].clip.length - 3.300001144f) < 0.01f && clips[0].clip.events.Length == 0;
        }

        // Reads only the event table belonging to THIS animator's inspected state.
        // The custom native events, not Unity clip.events alone, determine seek safety.
        private string _inspectionEventsStatus = "not read";
        private bool ReadInspectionEvents(out List<McxInspectionEvent> observed, bool nativeMcx, int maximumEvents = GlobalMcxInspection.MaximumEvents)
        {
            observed = new List<McxInspectionEvent>();
            bool Reject(string reason) { _inspectionEventsStatus = reason; return false; }
            // Unity's actual state binding is authoritative. WTT MCX carries a stale
            // serialized FullNameHash; EFT dispatches the bound behaviour's event list.
            int stateHash = _customStm ? _animator!.GetCurrentAnimatorStateInfo(1).fullPathHash : PoseMarker.SharedState;
            string stateName = _customStm ? CustomStmInspection.StateName(_template,_animator!.runtimeAnimatorController.name,stateHash) : "Hands.LOOK";
            if(stateName.Length==0)return Reject("unaudited inspection state");
            var all = _animator!.GetBehaviours(stateHash, 1);
            if (all == null || all.Length > 512) return Reject("Hands.LOOK behaviour lookup unavailable/outside bounds");
            var matches = all.Where(b => b != null && b.GetType().FullName == "AnimationEventSystem.AnimationEventsStateBehaviour").ToArray();
            if (matches.Length != 1) return Reject("Hands.LOOK event behaviour count=" + matches.Length);
            object behaviour = matches[0];
            if (ReadAccess.Text(_read.Get(behaviour, "FullName")) != stateName ||
                !(_read.Get(behaviour, "EventsListId") is int index) || index < 0) return Reject("Hands.LOOK event name/index invalid");
            object? data = _read.Get(behaviour, "EventsData");
            if (!(data is UnityEngine.Object asset) || (nativeMcx && asset.name != McxInspectionSegment.Controller + "_StaticData") ||
                !(_read.Get(data, "_stateHashToEventsCollection") is IList lists) || lists.Count > 512 || index >= lists.Count ||
                !(_read.Get(lists[index], "_animationEvents") is IList events)) return Reject("Hands.LOOK event asset/list unreadable or outside verified bounds");
            if (nativeMcx ? events.Count != 3 : events.Count < 3 || events.Count > maximumEvents)
                return Reject("Hands.LOOK event count=" + events.Count + " maximum=" + (nativeMcx ? 3 : maximumEvents) + " asset=" + asset.name + " list=" + index);
            foreach (object e in events)
            {
                object? p = _read.Get(e, "Parameter");
                if (!(_read.Get(e, "_time") is float time) || !(_read.Get(e, "Enabled") is bool enabled) ||
                    !(_read.Get(e, "_functionNameHash") is int hash) || !(_read.Get(e, "EventConditions") is IList conditions) ||
                    !(_read.Get(p, "BoolParam") is bool boolean) || !(_read.Get(p, "FloatParam") is float number) ||
                    !(_read.Get(p, "IntParam") is int integer) || _read.Get(p, "ParamType") == null) return Reject("Hands.LOOK event fields unreadable at " + observed.Count);
                if(conditions.Count>8) return Reject("Hands.LOOK condition count outside bounds");
                var conditionData=new List<McxInspectionCondition>();
                foreach(object condition in conditions)
                {
                    if(!(_read.Get(condition,"BoolValue") is bool cb) || !(_read.Get(condition,"FloatValue") is float cf) ||
                        !(_read.Get(condition,"IntValue") is int ci) || _read.Get(condition,"ConditionParamType")==null ||
                        _read.Get(condition,"ConditionMode")==null) return Reject("Hands.LOOK condition fields unreadable");
                    conditionData.Add(new McxInspectionCondition {Parameter=ReadAccess.Text(_read.Get(condition,"ParameterName")),Boolean=cb,Float=cf,Integer=ci,
                        Type=Convert.ToInt32(_read.Get(condition,"ConditionParamType"),CultureInfo.InvariantCulture),Mode=Convert.ToInt32(_read.Get(condition,"ConditionMode"),CultureInfo.InvariantCulture)});
                }
                observed.Add(new McxInspectionEvent { Name = ReadAccess.Text(_read.Get(e, "_functionName")), Time = time,
                    Hash = hash, Enabled = enabled, Conditions = conditions.Count, Boolean = boolean, Float = number, Integer = integer,
                    Text = ReadAccess.Text(_read.Get(p, "StringParam")), ParameterType = Convert.ToInt32(_read.Get(p, "ParamType"), CultureInfo.InvariantCulture),ConditionData=conditionData.ToArray() });
            }
            _inspectionEventsStatus = "bound " + stateName + "/1 storedHash=" + ReadAccess.Text(_read.Get(behaviour, "FullNameHash")) +
                " asset=" + asset.name + " list=" + index + " count=" + observed.Count;
            return true;
        }
        private bool McxEventsMatch() => ReadInspectionEvents(out var events, !_mcxOverride) &&
            (_mcxOverride ? GlobalMcxInspection.CompatibleEvents(events,_targetMarker!.Time) : McxInspectionSegment.EventsMatch(events));

        // Called after the existing identity, alive, hands, sprint and ownership checks.
        private bool TickMcxEntry()
        {
            if (!McxSegmentActive || _learning || _state.Phase != PosePhase.Playing) return true;
            AnimatorStateInfo state = _animator!.GetCurrentAnimatorStateInfo(1);
            bool stable = state.fullPathHash == PoseMarker.SharedState && !state.loop && !_animator.IsInTransition(1);
            if (_mcxEntry.Pending)
            {
                McxSeekResult result = _mcxEntry.Observe(Time.frameCount, Time.unscaledTime, state.normalizedTime,
                    stable && OwnedOperation() && StartEventFired() && McxClipMatches());
                if (result == McxSeekResult.Pending) return false;
                if (result == McxSeekResult.Fault) { McxEntryFault("forward seek read-back did not match the owned native clip/time"); return false; }
                _mcxStatus = "entry confirmed; playing native lead-in to saved hold";
                Event("MCX_ENTRY confirmed time=" + state.normalizedTime.ToString("R", CultureInfo.InvariantCulture));
                return true;
            }
            if (_mcxEntry.Consumed) return true;
            if (!stable || !StartEventFired()) return false;
            if (!McxClipMatches() || !McxEventsMatch())
            {
                _mcxStatus = "rejected: native MCX clip/event profile changed; no seek";
                Event("MCX_ENTRY " + _mcxStatus); Release("MCX clip/event validation rejected"); return false;
            }
            if (!_mcxEntry.TryIssue(state.normalizedTime, _targetMarker!.Time, Time.frameCount, Time.unscaledTime, out string denial))
            { _mcxStatus = "rejected: " + denial; Event("MCX_ENTRY " + _mcxStatus); Release(denial); return false; }
            _mcxStatus = "forward seek requested; awaiting next-frame confirmation";
            Event("MCX_ENTRY seek " + state.normalizedTime.ToString("R", CultureInfo.InvariantCulture) + " -> " +
                _mcxEntry.Entry.ToString("R", CultureInfo.InvariantCulture) + " hold=" + _targetMarker.Time.ToString("R", CultureInfo.InvariantCulture) +
                (_mcxOverride ? " events=validated target start/hand sounds and optional inspection audio" : " events=StartUtilityOperation@0,HandOff@0.0407192,HandOn@0.9338974"));
            try { _animator.Play(PoseMarker.SharedState, 1, _mcxEntry.Entry); }
            catch (Exception e) { McxEntryFault("seek uncertain: " + e.GetBaseException().Message); }
            return false; // Never pause before native evaluation confirms the seek.
        }

        private void McxEntryFault(string reason)
        {
            _mcxClipAwaiting=false;
            _mcxStatus = "fault: " + reason;
            Event("MCX_ENTRY " + _mcxStatus);
            _motion.LatchFault("MCX segment: " + reason); // Existing session uncertainty latch also blocks inventory.
            Release("MCX entry fault");
            RestoreMotion();
        }
    }
}
