using System;
using System.Collections.Generic;
using System.Linq;

namespace Tylevo.FieldAttachments.Core
{
    // A read-only binding of every child slot, including empty slots. The root
    // moves through one native Move; none of these child addresses may change.
    public sealed class AssemblyPart
    {
        public readonly object Slot, Parent;
        public readonly object? Item, Address;
        public readonly string Path, Id, Template, RuntimeType;
        public readonly bool Required, Locked, Deleted, Examined;
        public readonly int Pin;
        public AssemblyPart(object slot, object parent, object? item, object? address, string path,
            string id, string template, string runtimeType, bool required, bool locked, bool deleted, bool examined, int pin)
        { Slot=slot; Parent=parent; Item=item; Address=address; Path=path; Id=id; Template=template; RuntimeType=runtimeType;
          Required=required; Locked=locked; Deleted=deleted; Examined=examined; Pin=pin; }
        public bool Same(AssemblyPart other) => ReferenceEquals(Slot,other.Slot) && ReferenceEquals(Parent,other.Parent) &&
            ReferenceEquals(Item,other.Item) && Equals(Address,other.Address) && Path==other.Path && Id==other.Id &&
            Template==other.Template && RuntimeType==other.RuntimeType && Required==other.Required && Locked==other.Locked &&
            Deleted==other.Deleted && Examined==other.Examined && Pin==other.Pin;
    }
    public sealed class AttachmentAssembly
    {
        public readonly bool Complete;
        public readonly IReadOnlyList<AssemblyPart> Parts;
        public AttachmentAssembly(bool complete, IEnumerable<AssemblyPart> parts)
        { Complete=complete; Parts=Array.AsReadOnly(parts.ToArray()); }
        public bool Same(AttachmentAssembly? other) => other!=null && Complete && other.Complete &&
            Parts.Count==other.Parts.Count && Parts.Select((p,i)=>p.Same(other.Parts[i])).All(v=>v);
        public bool ContainsSlot(object? slot) => slot!=null && Parts.Any(p=>ReferenceEquals(p.Slot,slot));
        public bool IsOptic(string rootType)
        {
            bool optic=AttachmentScope.Supports(AttachmentGroup.Optic,rootType), populated=false;
            if(!Complete || (!optic && rootType!="EFT.InventoryLogic.Mount")) return false;
            foreach(var part in Parts)
            {
                if(part.Deleted || (part.Item==null && part.Required)) return false;
                if(part.Item==null) continue;
                populated=true;
                bool sight=AttachmentScope.Supports(AttachmentGroup.Optic,part.RuntimeType);
                if((!sight && part.RuntimeType!="EFT.InventoryLogic.Mount") || !part.Examined || part.Pin!=0) return false;
                optic |= sight;
            }
            return populated && optic;
        }
        public static bool Supports(AttachmentGroup? group, ItemObservation? item) => item!=null &&
            (item.HasChildren ? group==AttachmentGroup.Optic && item.Assembly?.IsOptic(item.RuntimeType)==true :
             AttachmentScope.Supports(group,item.RuntimeType));
        public static bool Same(ItemObservation before, ItemObservation after) => before.HasChildren==after.HasChildren &&
            (!before.HasChildren || before.Assembly?.Same(after.Assembly)==true);
        public static void CollapseOpticPositions(RaidSnapshot snapshot)
        {
            foreach(var unit in snapshot.Slots.Where(s=>s.Slot.Group==AttachmentGroup.Optic &&
                s.Slot.Required==false && s.Slot.Locked==false && s.Slot.Installed?.RaidModdable==true &&
                s.Slot.Installed.HasChildren && Supports(AttachmentGroup.Optic,s.Slot.Installed)))
                foreach(var child in snapshot.Slots.Where(s=>unit.Slot.Installed!.Assembly!.ContainsSlot(s.Slot.NativeSlot)))
                    child.Slot.HiddenInQuickSwap=true;
            foreach(var vm in snapshot.Slots.Where(s=>s.Slot.Group==AttachmentGroup.Optic))
            {
                var slot=vm.Slot;
                if(slot.Required!=false || slot.Locked!=false || (slot.Installed!=null &&
                    AttachmentRequest.ScopeDenial(AttachmentAction.Uninstall,slot,slot.Installed).Length!=0)) slot.HiddenInQuickSwap=true;
                if(slot.HiddenInQuickSwap) vm.Candidates.Clear();
            }
        }
    }
}
