using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Diz.LanguageExtensions;
using EFT.InventoryLogic;
using HarmonyLib;

namespace Tylevo.FieldAttachments.Runtime
{
    // Only a registered move may treat RaidModdable as true. A required slot is
    // exempt only for one of the two exact moves of a bound explicit replacement.
    // Neither the native template nor the slot's Required field is ever written.
    internal static class NativeToolMoveScope
    {
        private const string Owner="com.tylevo.fieldattachments.multitool-move";
        private static readonly ConditionalWeakTable<MoveResult,NativeToolMoveBinding> Bindings =
            new ConditionalWeakTable<MoveResult,NativeToolMoveBinding>();
        private static MethodInfo? _raidGetter;
        private static FieldInfo? _resultController;
        private static bool _attempted;
        internal static bool Ready { get; private set; }
        internal static string Status { get; private set; }="Multitool native route is not initialized.";
        [ThreadStatic] private static Invocation? _current;

        private enum Phase { Simulation, CanExecute, Execute }
        private sealed class Invocation : IDisposable
        {
            internal readonly NativeToolMoveBinding Binding;
            internal readonly Phase Stage;
            private readonly Invocation? _previous;
            private bool _disposed;
            internal Invocation(NativeToolMoveBinding binding,Phase stage)
            { Binding=binding; Stage=stage; _previous=_current; _current=this; }
            public void Dispose()
            {
                if(_disposed) return;
                _disposed=true;
                if(ReferenceEquals(_current,this)) _current=_previous;
                else { Binding.Revoke(); _current=null; }
            }
        }

        internal static bool EnsureReady()
        {
            if(_attempted) return Ready;
            _attempted=true;
            var harmony=new Harmony(Owner);
            try
            {
                if(typeof(Item).Assembly.ManifestModule.ModuleVersionId.ToString()!=NativeInstallProbe.InspectedGameMvid)
                    throw new InvalidOperationException("Native inventory build changed.");
                const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
                var path=typeof(ItemManipulator).GetMethod("MovePathCheck",flags,null,
                    new[]{typeof(Item),typeof(ItemAddress),typeof(ItemController)},null);
                var canMove=typeof(Mod).GetMethod("CanBeMoved",flags,null,new[]{typeof(IContainer)},null);
                var canExecute=typeof(MoveResult).GetMethod("CanExecute",flags,null,new[]{typeof(ItemController)},null);
                var execute=typeof(MoveResult).GetMethod("Execute",flags,null,Type.EmptyTypes,null);
                _raidGetter=typeof(Mod).GetProperty("RaidModdable",flags)?.GetGetMethod(true);
                _resultController=typeof(MoveResult).GetField("_itemController",flags);
                if(path==null || !path.IsStatic || canMove==null || canMove.IsStatic ||
                    canExecute?.ReturnType!=typeof(bool) || execute?.ReturnType!=typeof(OperationResult) ||
                    _raidGetter?.ReturnType!=typeof(bool) || _resultController?.FieldType!=typeof(ItemController))
                    throw new InvalidOperationException("Native multitool signatures changed.");
                foreach(var method in new[]{path,canMove,canExecute,execute})
                {
                    var patches=Harmony.GetPatchInfo(method);
                    if(patches!=null && patches.Owners.Any(owner=>owner!=Owner))
                        throw new InvalidOperationException("Another patch owns a multitool validation boundary.");
                }
                // The transpiler verifies both getter and vital-restriction instruction counts.
                harmony.Patch(path,transpiler:new HarmonyMethod(typeof(NativeToolMoveScope),nameof(ReplaceRaidGetter)));
                harmony.Patch(canMove,transpiler:new HarmonyMethod(typeof(NativeToolMoveScope),nameof(ReplaceRaidGetter)));
                harmony.Patch(canExecute,prefix:new HarmonyMethod(typeof(NativeToolMoveScope),nameof(BeforeCanExecute)),
                    finalizer:new HarmonyMethod(typeof(NativeToolMoveScope),nameof(AfterInvocation)));
                harmony.Patch(execute,prefix:new HarmonyMethod(typeof(NativeToolMoveScope),nameof(BeforeExecute)),
                    finalizer:new HarmonyMethod(typeof(NativeToolMoveScope),nameof(AfterInvocation)));
                Ready=true; Status="";
            }
            catch(Exception e) { harmony.UnpatchSelf(); Ready=false; Status=e.GetBaseException().Message; }
            return Ready;
        }

        private static IEnumerable<CodeInstruction> ReplaceRaidGetter(IEnumerable<CodeInstruction> source, MethodBase __originalMethod)
        {
            var code=source.Select(i=>new CodeInstruction(i)).ToList();
            bool path=__originalMethod.DeclaringType==typeof(ItemManipulator) && __originalMethod.Name=="MovePathCheck";
            bool canMove=__originalMethod.DeclaringType==typeof(Mod) && __originalMethod.Name=="CanBeMoved";
            if(!path && !canMove) throw new InvalidOperationException("Unexpected multitool patch target.");
            var calls=code.Where(i=>(i.opcode==OpCodes.Call || i.opcode==OpCodes.Callvirt) && Equals(i.operand,_raidGetter)).ToArray();
            int required=code.Count(i=>i.opcode==OpCodes.Ldfld && i.operand is FieldInfo field &&
                field.DeclaringType==typeof(Slot) && field.Name=="Required");
            int vital=code.Count(i=>i.opcode==OpCodes.Newobj && i.operand is ConstructorInfo ctor &&
                ctor.DeclaringType==typeof(CannotModifyVitalInRaidError));
            int denied=code.Count(i=>i.opcode==OpCodes.Newobj && i.operand is ConstructorInfo ctor &&
                ctor.DeclaringType==typeof(NotRaidModdableError));
            if(calls.Length!=1 || required!=(path?2:1) || vital!=(path?2:1) || denied!=1)
                throw new InvalidOperationException("Native raid/vital validation IL changed.");
            int index=code.IndexOf(calls[0]);
            var first=new CodeInstruction(path?OpCodes.Ldarg_0:OpCodes.Ldarg_1);
            first.MoveLabelsFrom(calls[0]); first.MoveBlocksFrom(calls[0]);
            var added=new List<CodeInstruction>{first};
            if(path) { added.Add(new CodeInstruction(OpCodes.Ldarg_1)); added.Add(new CodeInstruction(OpCodes.Ldarg_2)); }
            calls[0].opcode=OpCodes.Call;
            calls[0].operand=typeof(NativeToolMoveScope).GetMethod(path?nameof(PathRaidModdable):nameof(SlotRaidModdable),
                BindingFlags.Static|BindingFlags.NonPublic)!;
            code.InsertRange(index,added);
            // Keep both native error branches. Replace only their field reads,
            // preserving the original value unless this exact slot/leg is bound.
            foreach(var load in code.Where(i=>i.opcode==OpCodes.Ldfld && i.operand is FieldInfo field &&
                field.DeclaringType==typeof(Slot) && field.Name=="Required").ToArray())
            {
                index=code.IndexOf(load);
                first=new CodeInstruction(OpCodes.Ldarg_0);
                first.MoveLabelsFrom(load); first.MoveBlocksFrom(load);
                added=new List<CodeInstruction>{first,new CodeInstruction(OpCodes.Ldarg_1)};
                if(path) added.Add(new CodeInstruction(OpCodes.Ldarg_2));
                load.opcode=OpCodes.Call;
                load.operand=typeof(NativeToolMoveScope).GetMethod(path?nameof(PathRequired):nameof(SlotRequired),
                    BindingFlags.Static|BindingFlags.NonPublic)!;
                code.InsertRange(index,added);
            }
            return code;
        }

        private static bool PathRequired(Slot slot,Item item,ItemAddress to,ItemController controller)
        {
            var call=_current;
            return slot.Required && !(call!=null && call.Stage!=Phase.CanExecute &&
                call.Binding.RequiredReplacement && !call.Binding.Revoked &&
                ReferenceEquals(call.Binding.Slot,slot) && ReferenceEquals(call.Binding.Item,item) &&
                ReferenceEquals(call.Binding.Controller,controller) && ReferenceEquals(call.Binding.To,to));
        }
        private static bool SlotRequired(Slot slot,Mod mod,IContainer to)
        {
            var call=_current;
            return slot.Required && !(call!=null && call.Stage==Phase.CanExecute &&
                call.Binding.RequiredReplacement && !call.Binding.Revoked &&
                ReferenceEquals(call.Binding.Slot,slot) && ReferenceEquals(call.Binding.Item,mod) &&
                ReferenceEquals(((ItemAddress)call.Binding.To).Container,to));
        }

        private static bool PathRaidModdable(Mod mod,Item item,ItemAddress to,ItemController controller)
        {
            if(mod.RaidModdable) return true;
            var call=_current;
            return call!=null && call.Stage!=Phase.CanExecute && !call.Binding.Revoked &&
                ReferenceEquals(mod,item) && ReferenceEquals(call.Binding.Item,item) &&
                ReferenceEquals(call.Binding.Controller,controller) && ReferenceEquals(call.Binding.To,to);
        }
        private static bool SlotRaidModdable(Mod mod,IContainer to)
        {
            if(mod.RaidModdable) return true;
            var call=_current;
            return call!=null && call.Stage==Phase.CanExecute && !call.Binding.Revoked &&
                ReferenceEquals(call.Binding.Item,mod) && ReferenceEquals(((ItemAddress)call.Binding.To).Container,to);
        }

        internal static object? Simulate(NativeToolMoveBinding binding,Func<object?> move)
        {
            if(!Ready || !binding.Validate(false)) throw new InvalidOperationException(binding.Denial.Length!=0 ? binding.Denial : Status);
            using(new Invocation(binding,Phase.Simulation)) return move();
        }
        internal static void Register(object result,NativeToolMoveBinding binding)
        {
            if(!Ready || !(result is MoveResult move) || !Matches(move,binding) || !binding.Validate(false))
                throw new InvalidOperationException("Native move did not match the multitool request.");
            Bindings.Add(move,binding);
        }
        private static bool Matches(MoveResult result,NativeToolMoveBinding binding) =>
            _resultController?.GetValue(result) is object controller &&
            binding.Matches(result.Item,controller,result.From,result.To);

        private static bool BeforeCanExecute(MoveResult __instance,ItemController __0,ref bool __result,out IDisposable? __state)
        {
            __state=null;
            if(!Bindings.TryGetValue(__instance,out var binding)) return true;
            if(!ReferenceEquals(binding.Controller,__0) || !Matches(__instance,binding) || !binding.Validate(false))
            { binding.Revoke(); __result=false; return false; }
            __state=new Invocation(binding,Phase.CanExecute); return true;
        }
        private static bool BeforeExecute(MoveResult __instance,ref OperationResult __result,out IDisposable? __state)
        {
            __state=null;
            if(!Bindings.TryGetValue(__instance,out var binding)) return true;
            if(!Matches(__instance,binding) || !binding.Validate(true))
            {
                binding.Revoke();
                __result=new NotRaidModdableError(__instance.Item);
                return false;
            }
            __state=new Invocation(binding,Phase.Execute); return true;
        }
        private static Exception? AfterInvocation(Exception? __exception,IDisposable? __state)
        { __state?.Dispose(); return __exception; }
    }
}
