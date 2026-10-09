using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Tylevo.FieldAttachments.Core;
using UnityEngine;

namespace Tylevo.FieldAttachments.Runtime
{
    // A neutral wrapper stays on the live animator. Only the owned clip binding
    // changes on return; swapping back to the bare controller broke RD holstering.
    // No donor controller, global asset changes, event edits or transform writes.
    public sealed class MdrInspectionMotion
    {
        private sealed class Binding
        {
            public Animator Animator = null!;
            public AnimatorOverrideController Controller = null!;
            public AnimationClip Target = null!;
            public AnimationClip Key = null!;
            public bool ExternalReplacement;
        }
        private readonly List<Binding> _bindings = new List<Binding>();
        private readonly PoseBindingLease<RuntimeAnimatorController, AnimationClip> _lease = new PoseBindingLease<RuntimeAnimatorController, AnimationClip>();
        private readonly List<PoseBindingLease<RuntimeAnimatorController,AnimationClip>> _extraLeases = new List<PoseBindingLease<RuntimeAnimatorController,AnimationClip>>();
        private Binding? _current;
        private AnimationClip? _donor;
        public bool Active => _lease.Active || _extraLeases.Any(l=>l.Active);
        public string Fault { get; private set; } = "";
        public string Status { get; private set; } = "native motion";

        // Caller has verified its owned, started inspection and the known SP build.
        public bool TryApply(Animator animator, string template, object wrapper, AnimationClip? mcxDonor = null)
        {
            try
            {
                bool customStm = CustomStmInspection.IsAuthoredClip(mcxDonor?.name);
                if (Active || Fault.Length != 0) throw new InvalidOperationException("clip lease active or trial fault latched");
                if (mcxDonor == null && !MdrMotionTrial.Supports(template)) throw new InvalidOperationException("weapon outside MCX/RD-704 trial");
                if (mcxDonor != null)
                {
                    string missing = customStm ? CustomStmInspection.MissingRigPath(template,path => animator.transform.Find(path) != null) : GlobalMcxInspection.MissingRigPath(path => animator.transform.Find(path) != null);
                    if (missing.Length != 0) throw new InvalidOperationException("missing MCX presentation rig path: " + missing);
                    if (customStm ? !CustomStmInspection.Requested(true, template, false) || !CustomStmInspection.ValidClip(template, mcxDonor.name,
                        mcxDonor.length, mcxDonor.legacy, mcxDonor.humanMotion, mcxDonor.events.Length) : mcxDonor.name != GlobalMcxInspection.DonorName)
                        throw new InvalidOperationException("unowned or incompatible presentation clip");
                }
                // Prune only destroyed animators, never a live or externally replaced controller.
                for (int i = _bindings.Count - 1; i >= 0; i--)
                    if (_bindings[i].Animator == null)
                    { if (!_bindings[i].ExternalReplacement) UnityEngine.Object.Destroy(_bindings[i].Controller); _bindings.RemoveAt(i); }
                Binding? binding = _bindings.SingleOrDefault(b => b.Animator == animator);
                RuntimeAnimatorController original = animator.runtimeAnimatorController;
                bool nativeVariant = binding==null && customStm && AuditedWeaponPresentations.For(template)!=null && original is AnimatorOverrideController;
                if (original == null || (original is AnimatorOverrideController && binding?.Controller != original && !nativeVariant) ||
                    (binding != null && binding.Controller != original) || animator.layerCount <= 1 ||
                    animator.GetLayerName(1) != "Hands" || animator.IsInTransition(1))
                    throw new InvalidOperationException("unsupported or already overridden controller");
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(1);
                if ((customStm ? !CustomStmInspection.StateMatches(template,original.name,state.fullPathHash) : state.fullPathHash != PoseMarker.SharedState) || !PoseMarker.Finite(state.normalizedTime) ||
                    state.normalizedTime < 0 || state.normalizedTime > 0.18f)
                    throw new InvalidOperationException("inspection was not observed early enough for clip replacement");
                foreach (string path in new[] {
                    "Base HumanLCollarbone/Base HumanLUpperarm/Base HumanLForearm1",
                    "Base HumanRCollarbone/Base HumanRUpperarm/Base HumanRForearm1", "Camera_animated",
                    "Weapon_root/Weapon_root_anim/weapon/weapon_L_hand_marker",
                    "Weapon_root/Weapon_root_anim/weapon/weapon_R_hand_marker" })
                    if (!(customStm && template=="5b3b713c5acfc4330140bd8d" && path==CustomStmInspection.LeftHandMarker) && animator.transform.Find(path) == null) throw new InvalidOperationException("missing shared rig path: " + path);
                AnimatorClipInfo[] playing = animator.GetCurrentAnimatorClipInfo(1).Where(p=>p.weight>0.0001f).ToArray();
                if (playing.Length < 1 || playing.Length > 4 || playing.Any(p=>p.clip==null || !PoseMarker.Finite(p.weight)) ||
                    Math.Abs(playing.Sum(p=>p.weight)-1)>0.01f || (!customStm && (playing.Length!=1 || !playing[0].clip.name.EndsWith("_look", StringComparison.Ordinal))))
                    throw new InvalidOperationException("audited native inspection clip weights required");
                AnimationClip target = playing[0].clip;
                var profiles = customStm ? AuditedWeaponPresentations.StateProfiles(template,original.name,state.fullPathHash) : Array.Empty<NativePresentationProfile>();
                if(playing.Any(p=>p.clip!=target && !profiles.Any(q=>q.Clip==p.clip.name && Math.Abs(q.Duration-p.clip.length)<.001f)))
                    throw new InvalidOperationException("unverified native blend child");
                if (customStm)
                {
                    string denial = CustomStmInspection.RecipientDenial(template, original.name, target.name,
                        target.length, target.legacy, target.humanMotion, target.events.Length);
                    if (denial.Length != 0) throw new InvalidOperationException(denial);
                }
                // Once per explicit pose start, never per frame; uses only already loaded assets.
                AnimationClip[] donors = mcxDonor != null ? new[] { mcxDonor } : Resources.FindObjectsOfTypeAll<AnimationClip>().Where(c => c != null && c.name == "mdr_look").ToArray();
                if (donors.Length != 1) throw new InvalidOperationException("mdr_look missing or ambiguous; keep the MDR loaded for this trial");
                AnimationClip donor = donors[0];
                if (target == donor || target.legacy || donor.legacy || target.humanMotion != donor.humanMotion ||
                    !PoseMarker.Finite(target.length) || !PoseMarker.Finite(donor.length) || target.length <= 0 || donor.length <= 0 ||
                    // Different source timelines require exact audited metadata above.
                    (Math.Abs(target.length - donor.length) > 0.05f && !(customStm &&
                        (CustomMcxBackport.Matches(template, target.name) || AuditedWeaponPresentations.MetadataMatches(template,original.name,target.name,target.length)))))
                    throw new InvalidOperationException("clip type/duration mismatch: " + target.name + "=" + target.length +
                        " human=" + target.humanMotion + "; " + donor.name + "=" + donor.length + " human=" + donor.humanMotion);
                if (!EventsMatch(target, donor)) throw new InvalidOperationException("clip events differ: " + target.name +
                    " count=" + target.events.Length + "; " + donor.name + " count=" + donor.events.Length + "; override refused");
                if (binding == null)
                {
                    // One wrapper per live animator, reused across openings and native inspection variants.
                    // Destroyed animators are pruned above; switching guns does not exhaust a session quota.
                    PropertyInfo? property = wrapper.GetType().GetProperty("runtimeAnimatorController", BindingFlags.Instance | BindingFlags.Public);
                    if (wrapper.GetType().FullName != "AnimationSystem.UnityAnimatorWrapper" ||
                        property?.PropertyType != typeof(RuntimeAnimatorController) || property.SetMethod == null ||
                        !ReferenceEquals(property.GetValue(wrapper), original))
                        throw new InvalidOperationException("native animator wrapper setter unavailable/changed");
                    var originalPairs=new List<KeyValuePair<AnimationClip,AnimationClip>>();
                    RuntimeAnimatorController baseController=original;
                    string nativeKey=target.name;
                    if(nativeVariant)
                    {
                        var variant=(AnimatorOverrideController)original;
                        baseController=variant.runtimeAnimatorController;
                        if(baseController==null || baseController is AnimatorOverrideController) throw new InvalidOperationException("nested native override not audited");
                        variant.GetOverrides(originalPairs);
                        if(originalPairs.Count>2048 || originalPairs.Any(p=>p.Key==null)) throw new InvalidOperationException("invalid native override map");
                        var changed=originalPairs.Where(p=>p.Value!=null && p.Value!=p.Key).Select(p=>new KeyValuePair<string,string>(p.Key.name,p.Value.name)).ToArray();
                        if(!AuditedWeaponPresentations.OverrideMatches(template,original.name,baseController.name,target.name,changed,out nativeKey))
                            throw new InvalidOperationException("native variant override map changed");
                    }
                    var created = new AnimatorOverrideController(baseController) { name = original.name };
                    if(nativeVariant) created.ApplyOverrides(originalPairs);
                    var clips = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                    created.GetOverrides(clips);
                    var keys=clips.Where(c=>c.Key!=null && c.Key.name==nativeKey && (c.Value??c.Key)==target).ToArray();
                    bool preserved=!nativeVariant || originalPairs.Count==clips.Count && originalPairs.All(p=>clips.Any(c=>c.Key==p.Key && (c.Value??c.Key)==(p.Value??p.Key)));
                    if (clips.Count > 2048 || keys.Length!=1 || !preserved)
                    { UnityEngine.Object.Destroy(created); throw new InvalidOperationException("native clip is not one unique controller binding"); }
                    binding = new Binding { Animator = animator, Controller = created, Target = target, Key=keys[0].Key };
                    _bindings.Add(binding); // Keep lifetime ownership even after a partial assignment.
                    var before = Fingerprint(animator);
                    try
                    {
                        // The inspected wrapper setter also refreshes its native parameter cache.
                        property.SetValue(wrapper, created);
                        if (animator.runtimeAnimatorController != created) throw new InvalidOperationException("neutral wrapper assignment not confirmed");
                        VerifyUnchanged(animator, before, "neutral wrapper assignment");
                    }
                    catch (Exception e) { LatchFault("neutral wrapper assignment uncertain: " + e.GetBaseException().Message); throw; }
                }
                var map=new List<KeyValuePair<AnimationClip,AnimationClip>>(); binding.Controller.GetOverrides(map);
                var targets=new List<KeyValuePair<AnimationClip,AnimationClip>>();
                if(profiles.Length==0) {
                    if(binding.Target!=target)throw new InvalidOperationException("owned native inspection binding changed");
                    targets.Add(new KeyValuePair<AnimationClip,AnimationClip>(binding.Key,target));
                } else foreach(var profile in profiles) {
                    var matches=map.Where(p=>p.Key!=null && p.Key.name==profile.BaseClip && (p.Value??p.Key).name==profile.Clip).ToArray();
                    if(matches.Length!=1)throw new InvalidOperationException("audited inspection binding missing or changed");
                    var pair=matches[0]; var native=pair.Value??pair.Key;
                    if(CustomStmInspection.RecipientDenial(template,original.name,native.name,native.length,native.legacy,native.humanMotion,native.events.Length).Length!=0 || !EventsMatch(native,donor))
                        throw new InvalidOperationException("audited blend child metadata changed");
                    if(!targets.Any(p=>p.Key==pair.Key))targets.Add(new KeyValuePair<AnimationClip,AnimationClip>(pair.Key,native));
                }
                if(targets.Count<1||targets.Count>4)throw new InvalidOperationException("inspection binding count outside audit");
                Binding owned = binding;
                _current = owned;
                _donor = donor;
                for(int i=0;i<targets.Count;i++)
                {
                    var pair=targets[i];
                    var lease=i==0?_lease:new PoseBindingLease<RuntimeAnimatorController,AnimationClip>();
                    if(i>0)_extraLeases.Add(lease);
                    lease.Acquire(owned.Controller, pair.Value, donor,
                        () => animator != null ? animator.runtimeAnimatorController : null,
                        () => owned.Controller[pair.Key] ?? pair.Key,
                        clip => {
                            var before = Fingerprint(animator);
                            try {
                                owned.Controller.ApplyOverrides(new[] { new KeyValuePair<AnimationClip, AnimationClip>(pair.Key, clip) });
                                VerifyUnchanged(animator, before, clip == pair.Value ? "native clip restore" : "inspection donor clip apply");
                            }
                            catch (Exception e) { LatchFault("clip write uncertain: " + e.GetBaseException().Message); throw; }
                        });
                }
                Status = (CustomStmInspection.IsAuthoredClip(mcxDonor?.name) ? "custom STM motion: " : mcxDonor != null ? "MCX global motion: " : "MDR motion trial: ") + target.name + " -> " + donor.name + " duration=" + donor.length;
                return true;
            }
            catch (Exception e)
            {
                Status = (CustomStmInspection.IsAuthoredClip(mcxDonor?.name) ? "custom STM fallback: " : mcxDonor != null ? "MCX global fallback: " : "MDR motion fallback: ") + e.GetBaseException().Message;
                if (!TryRestore()) Status += "; clip restore pending or trial fault latched";
                return false;
            }
        }

        public static string NativeClipName(AnimatorClipInfo[] clips) => clips.Length<=4 ?
            clips.Where(p=>p.clip!=null && PoseMarker.Finite(p.weight) && p.weight>.0001f).Select(p=>p.clip.name).FirstOrDefault() ?? "" : "";

        private static bool EventsMatch(AnimationClip target, AnimationClip donor)
        {
            AnimationEvent[] a = target.events, b = donor.events;
            if (a.Length != b.Length || a.Length > 32) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i].functionName != b[i].functionName || a[i].stringParameter != b[i].stringParameter ||
                    a[i].intParameter != b[i].intParameter || a[i].floatParameter != b[i].floatParameter ||
                    a[i].objectReferenceParameter != b[i].objectReferenceParameter || a[i].messageOptions != b[i].messageOptions ||
                    Math.Abs(a[i].time - b[i].time) > 0.01f) return false;
            return true;
        }

        public bool IsPlaying(Animator animator)
        {
            if (!Active || _donor == null || animator.runtimeAnimatorController != _current?.Controller) return false;
            AnimatorClipInfo[] playing = animator.GetCurrentAnimatorClipInfo(1);
            return playing.Length>0 && playing.Length<=4 && playing.All(p=>PoseMarker.Finite(p.weight) && p.weight>=0 && (p.weight<=.0001f || p.clip==_donor)) && Math.Abs(playing.Sum(p=>p.weight)-1)<.01f;
        }

        public bool TryRestore()
        {
            bool restored=_lease.TryRestore();
            foreach(var lease in _extraLeases)restored=lease.TryRestore() && restored;
            if(!restored)return false;
            bool external=_lease.ExternalChange || _extraLeases.Any(l=>l.ExternalChange);
            _extraLeases.Clear();
            if (_current != null && external) _current.ExternalReplacement = true;
            if (_current != null) Status = external ? "external controller/clip change preserved" :
                "native inspection clip restored; live controller retained; immediate state/parameters unchanged";
            _current = null; _donor = null;
            return Fault.Length == 0;
        }
        public void LatchFault(string reason) { if (Fault.Length == 0) Fault = reason; }
        private void VerifyUnchanged(Animator animator, Dictionary<string, string> before, string action)
        {
            var after = Fingerprint(animator);
            string? changed = before.Keys.FirstOrDefault(k => !after.TryGetValue(k, out string value) || value != before[k]);
            if (changed == null && after.Count == before.Count) return;
            string reason = action + " changed " + (changed == null ? "parameter/layer count" : changed + ": " + before[changed] + " -> " +
                (after.TryGetValue(changed, out string value) ? value : "missing"));
            LatchFault(reason); throw new InvalidOperationException(reason);
        }
        // Immediate, same-frame observation only: never replay states, write parameters,
        // call Rebind/Update, or manufacture completion events. Triggers are not read.
        private static Dictionary<string, string> Fingerprint(Animator animator)
        {
            var result = new Dictionary<string, string>();
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            result["speed"] = animator.speed.ToString("R", culture);
            if (animator.layerCount > 16 || animator.parameterCount > 256) throw new InvalidOperationException("animator bounds changed");
            for (int i = 0; i < animator.layerCount; i++)
            {
                var state = animator.GetCurrentAnimatorStateInfo(i);
                result["layer " + i] = state.fullPathHash + "/" + state.normalizedTime.ToString("R", culture) + "/" +
                    animator.IsInTransition(i) + "/" + animator.GetLayerWeight(i).ToString("R", culture);
            }
            foreach (var p in animator.parameters)
            {
                string key = "parameter " + p.name;
                if (p.type == AnimatorControllerParameterType.Bool) result[key] = animator.GetBool(p.nameHash).ToString();
                else if (p.type == AnimatorControllerParameterType.Int) result[key] = animator.GetInteger(p.nameHash).ToString(culture);
                else if (p.type == AnimatorControllerParameterType.Float) result[key] = animator.GetFloat(p.nameHash).ToString("R", culture);
            }
            return result;
        }
    }
}
