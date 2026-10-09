using System;
using System.Reflection;
using HarmonyLib;
using Tylevo.FieldAttachments.Core;
using UnityEngine;

namespace Tylevo.FieldAttachments.Runtime
{
    // Exact local 4.1.5 AddMod/RemoveMod lifecycle; only a validated submission
    // from this plugin can issue the single-use item/slot/controller ticket.
    internal sealed class AttachmentAnimationSpeed
    {
        private static AttachmentAnimationSpeed? _owner;
        private static bool _installed;
        private readonly ReadAccess _read;
        private readonly Action<string> _log;
        private readonly AttachmentSpeedLease _lease=new AttachmentSpeedLease();
        private RaidSnapshot? _snapshot;
        private object? _hands, _item, _slot, _operation;
        private Animator? _animator;
        private bool _removing, _ticket;
        private float _expires, _multiplier;
        internal float Multiplier=AttachmentSpeedLease.DefaultMultiplier;
        internal AttachmentAnimationSpeed(ReadAccess read,Action<string> log) { _read=read; _log=log; }
        private void Log(string message) { try { _log(message); } catch { } }

        internal void Prepare(RaidSnapshot snapshot,object item,object slot,bool removing)
        {
            End();
            if(_lease.Active || Multiplier<=1) return;
            try
            {
                Initialize();
                if(_owner!=null && !ReferenceEquals(_owner,this)) return;
                _snapshot=snapshot; _hands=_read.Get(snapshot.Player,"HandsController");
                _item=item; _slot=slot; _removing=removing; _multiplier=Multiplier;
                _expires=Time.unscaledTime+20; _ticket=true; _owner=this;
            }
            catch(Exception e) { End(); Log("SPEED_SKIP "+e.GetBaseException().Message); }
        }
        private static void Initialize()
        {
            if(_installed) return;
            var assembly=typeof(EFT.Player).Assembly;
            if(assembly.ManifestModule.ModuleVersionId.ToString()!=NativeInstallProbe.InspectedGameMvid)
                throw new InvalidOperationException("native animation build changed");
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly;
            var starts=new MethodInfo[2];
            for(int i=0;i<2;i++)
            {
                var t=assembly.GetType("EFT.Player+FirearmController+"+(i==0 ? "AddModOperation" : "RemoveModOperation"),true)!;
                starts[i]=t.GetMethod("Start",flags)!;
                var args=starts[i]?.GetParameters();
                if(starts[i]==null || starts[i].ReturnType!=typeof(void) || args==null || args.Length!=3 ||
                    args[0].ParameterType.FullName!="EFT.InventoryLogic.Item" || args[1].ParameterType.FullName!="EFT.InventoryLogic.Slot" ||
                    args[2].ParameterType.FullName!="Comfort.Common.Callback") throw new InvalidOperationException("native attachment Start changed");
            }
            var end=assembly.GetType("EFT.Player+ObjectInHandsOperation",true)!.GetMethod("OnEnd",flags,null,Type.EmptyTypes,null);
            if(end==null || end.ReturnType!=typeof(void)) throw new InvalidOperationException("native operation end changed");
            var harmony=new Harmony("com.tylevo.fieldattachments.attachment-speed");
            try
            {
                foreach(var start in starts) harmony.Patch(start,postfix:new HarmonyMethod(typeof(AttachmentAnimationSpeed),nameof(AfterStart)));
                harmony.Patch(end,prefix:new HarmonyMethod(typeof(AttachmentAnimationSpeed),nameof(BeforeEnd)));
                _installed=true;
            }
            catch { harmony.UnpatchSelf(); throw; }
        }
        private static void AfterStart(object __instance,object __0,object __1)
        {
            var owner=_owner;
            if(owner==null) return;
            try { owner.Started(__instance,__0,__1); }
            catch(Exception e) { owner.End(); owner.Log("SPEED_SKIP "+e.GetBaseException().Message); }
        }
        private void Started(object operation,object item,object slot)
        {
            if(!_ticket || !ReferenceEquals(item,_item) || !ReferenceEquals(slot,_slot) ||
                !ReferenceEquals(_read.Get(operation,"Controller"),_hands) ||
                operation.GetType().Name!=(_removing ? "RemoveModOperation" : "AddModOperation")) return;
            _ticket=false; _operation=operation;
            if(!ContextCurrent() || !ReferenceEquals(_read.Get(_hands,"CurrentOperation"),operation)) { End(); return; }
            object? wrapper=_read.Get(_read.Get(_hands,"FirearmsAnimator"),"Animator");
            _animator=wrapper?.GetType().FullName=="AnimationSystem.UnityAnimatorWrapper" ? _read.Get(wrapper,"_animator") as Animator : null;
            if(_animator==null || !_animator.isActiveAndEnabled) { End(); Log("SPEED_SKIP native Unity animator unavailable"); return; }
            Animator owned=_animator;
            if(_lease.Acquire(()=>owned.speed,value=>owned.speed=value,_multiplier))
                Log("SPEED_APPLIED "+operation.GetType().Name+" x"+_multiplier.ToString("F2",System.Globalization.CultureInfo.InvariantCulture));
            else End();
        }
        private static void BeforeEnd(object __instance)
        {
            if(_owner!=null && ReferenceEquals(_owner._operation,__instance)) _owner.End();
        }
        private bool ContextCurrent() => _snapshot!=null && Time.unscaledTime<_expires &&
            ReferenceEquals(_read.Get(_snapshot.Player,"HandsController"),_hands) &&
            ReferenceEquals(_read.Get(_hands,"Item"),_snapshot.Weapon) &&
            ReadAccess.Bool(_read.Get(_snapshot.Player,"IsYourPlayer"))==true &&
            ReadAccess.Bool(_read.Get(_read.Get(_snapshot.Player,"HealthController"),"IsAlive"))==true;
        internal void Poll(bool enabled)
        {
            if(!ReferenceEquals(_owner,this)) return;
            try
            {
                if(!enabled || !Application.isFocused || !ContextCurrent() ||
                    (_operation!=null && !ReferenceEquals(_read.Get(_hands,"CurrentOperation"),_operation))) End();
            }
            catch { End(); }
        }
        internal void End()
        {
            _ticket=false;
            if(_animator==null) _lease.Forget();
            bool wasActive=_lease.Active;
            if(!_lease.Release()) return; // Retry on the next Poll if Unity temporarily rejects restoration.
            if(wasActive) Log("SPEED_RELEASED prior speed restored or external speed retained");
            _snapshot=null; _hands=_item=_slot=_operation=null; _animator=null;
            if(ReferenceEquals(_owner,this)) _owner=null;
        }
    }
}
