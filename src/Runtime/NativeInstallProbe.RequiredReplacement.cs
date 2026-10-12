using System;
using System.Linq;
using Tylevo.FieldAttachments.Core;

namespace Tylevo.FieldAttachments.Runtime
{
    public sealed partial class NativeInstallProbe
    {
        // This continuation is independent of the current native move. Closing the
        // UI may discard the second leg without cancelling a submitted first leg.
        private RequiredReplacementBinding? _requiredReplacement, _toolRequiredReplacement;
        private enum RequiredLeg { Removing, Waiting, Installing }
        private sealed class RequiredPartBinding
        {
            internal readonly object Item;
            internal readonly string Id, Template, RuntimeType;
            internal readonly bool HasChildren;
            internal readonly bool? RaidModdable;
            internal readonly AttachmentAssembly Assembly;
            internal RequiredPartBinding(ItemObservation item)
            {
                Item=item.NativeItem!; Id=item.Id; Template=item.TemplateId; RuntimeType=item.RuntimeType;
                HasChildren=item.HasChildren; RaidModdable=item.RaidModdable; Assembly=item.Assembly!;
            }
        }
        private sealed class RequiredReplacementBinding
        {
            internal readonly AttachmentRequest Request;
            internal readonly object Slot, Parent, IncomingGrid, IncomingAddress;
            internal readonly string IncomingLocation;
            internal readonly RequiredPartBinding Outgoing, Incoming;
            internal RequiredLeg Leg;
            internal object? OutgoingGrid, OutgoingAddress;
            internal string OutgoingLocation="";
            internal DateTime Deadline;
            internal RequiredReplacementBinding(AttachmentRequest request,SlotObservation slot,ItemObservation incoming,object parent)
            {
                Request=request; Slot=slot.NativeSlot!; Parent=parent;
                Outgoing=new RequiredPartBinding(slot.Installed!); Incoming=new RequiredPartBinding(incoming);
                IncomingGrid=incoming.SourceGrid!; IncomingAddress=incoming.SourceAddress!; IncomingLocation=incoming.Location;
            }
        }

        private bool AuthorizeRequiredReplacement(RaidSnapshot snapshot,SlotViewModel? chosenSlot,
            CandidateObservation? chosenItem,AttachmentRequest? replacement)
        {
            _toolRequiredReplacement=null;
            if(chosenSlot?.Slot.Required!=true)
            { _requiredReplacement=null; return false; }
            if(replacement==null || replacement.Action!=AttachmentAction.Replace || !replacement.RequiredReplacement ||
                !AttachmentLayers.IsAdvanced(chosenSlot.Slot.Group) || replacement.Group!=chosenSlot.Slot.Group ||
                replacement.SlotPath!=chosenSlot.Slot.Path || !ReferenceEquals(snapshot.Player,replacement.Player) ||
                !ReferenceEquals(snapshot.Weapon,replacement.Weapon) || !ReferenceEquals(snapshot.Controller,replacement.Controller) ||
                replacement.Multitool?.Same(snapshot.Multitool)!=true ||
                ReadAccess.Bool(_read.Get(_slot,"Required"))!=true)
                return false;
            if(_removing)
            {
                if(_requiredReplacement!=null || replacement.Validate(snapshot).Length!=0) return false;
                var candidates=chosenSlot.Candidates.Where(c=>c.Item.Id==replacement.ItemId).ToArray();
                if(candidates.Length!=1 || chosenSlot.Slot.Installed==null ||
                    !ReferenceEquals(chosenSlot.Slot.Installed.NativeItem,_item)) return false;
                var incoming=candidates[0].Item;
                object? parent=_read.Get(_slot,"ParentItem");
                if(parent==null || incoming.SourceGrid==null || incoming.SourceAddress==null ||
                    !AttachmentAssembly.SupportsRequiredPart(replacement.Group,incoming) ||
                    !AttachmentAssembly.SupportsRequiredPart(replacement.Group,chosenSlot.Slot.Installed)) return false;
                _requiredReplacement=new RequiredReplacementBinding(replacement,chosenSlot.Slot,incoming,parent);
            }
            else
            {
                var bound=_requiredReplacement;
                if(bound==null || bound.Leg!=RequiredLeg.Waiting || DateTime.UtcNow>=bound.Deadline ||
                    !ReferenceEquals(bound.Request,replacement) || !ReferenceEquals(bound.Slot,_slot) ||
                    !ReferenceEquals(bound.Incoming.Item,chosenItem?.Item.NativeItem) ||
                    replacement.AfterRemoval(snapshot,out var installation).Length!=0 || installation==null)
                    return false;
                // Consumed before any simulation; rejection never permits a retry.
                bound.Leg=RequiredLeg.Installing;
            }
            _toolRequiredReplacement=_requiredReplacement;
            return true;
        }

        private string ValidateRequiredReplacement(CarriedStorageScan storage)
        {
            var bound=_toolRequiredReplacement;
            if(bound==null) return "No explicit essential-part replacement is bound.";
            if(!ReferenceEquals(bound.Slot,_slot) || !ReferenceEquals(_toolSlotParent,bound.Parent) ||
                ReadAccess.Bool(_read.Get(bound.Slot,"Required"))!=true ||
                !ReferenceEquals(_item,_removing ? bound.Outgoing.Item : bound.Incoming.Item) ||
                bound.Leg!=(_removing ? RequiredLeg.Removing : RequiredLeg.Installing))
                return "Essential replacement leg or mounting point changed.";
            foreach(var part in new[]{bound.Outgoing,bound.Incoming})
            {
                if(!ReferenceEquals(_read.Get(part.Item,"Owner"),bound.Request.Controller) ||
                    ReadAccess.Text(_read.Get(part.Item,"Id"))!=part.Id ||
                    ReadAccess.Text(_read.Get(part.Item,"TemplateId"))!=part.Template ||
                    part.Item.GetType().FullName!=part.RuntimeType ||
                    !TryInt(_read.Get(part.Item,"StackObjectsCount"),out int count) || count!=1 ||
                    !TryInt(_read.Get(part.Item,"PinLockState"),out int pin) || pin!=0 ||
                    ReadAccess.Bool(_read.Get(part.Item,"RaidModdable"))!=part.RaidModdable ||
                    !Equals(_toolExamined?.Invoke(bound.Request.Controller,new[]{part.Item}),true) ||
                    _filter.Accepts(bound.Slot,part.Item)!=true ||
                    !TryHasChildren(_read.Get(part.Item,"Slots"),out bool children) || children!=part.HasChildren ||
                    !part.Assembly.Same(new AttachmentAssemblyReader(_read).Capture(part.Item,bound.Request.Controller)))
                    return "Essential replacement item, examination or complete assembly changed.";
            }
            if(!RequiredPartCarried(storage,bound.Incoming,bound.IncomingGrid,bound.IncomingAddress,bound.IncomingLocation))
                return "Bound incoming essential part moved or became inaccessible.";
            if(!_removing && !RequiredPartCarried(storage,bound.Outgoing,bound.OutgoingGrid,bound.OutgoingAddress,bound.OutgoingLocation))
                return "Removed essential part moved or became inaccessible.";
            return "";
        }

        private bool RequiredPartCarried(CarriedStorageScan storage,RequiredPartBinding part,object? grid,object? address,string location)
            => storage.Complete && grid!=null && address!=null &&
                ReadAccess.IsKind(address,"EFT.InventoryLogic.GridItemAddress") &&
                ReferenceEquals(_read.Get(address,"Container"),grid) && Equals(_read.Get(part.Item,"Parent"),address) &&
                storage.Grids.Count(g=>ReferenceEquals(g.NativeGrid,grid) && g.Location==location &&
                    g.Items.Count(i=>ReferenceEquals(i,part.Item))==1)==1;

        private void FinishRequiredReplacement(bool uncertain,bool succeeded)
        {
            var bound=_toolRequiredReplacement;
            if(!uncertain && succeeded && _removing && bound!=null && ReferenceEquals(bound,_requiredReplacement))
            {
                bound.OutgoingAddress=_destinationAddress; bound.OutgoingGrid=_sourceGrid;
                bound.OutgoingLocation=_storageLocation; bound.Leg=RequiredLeg.Waiting;
                bound.Deadline=DateTime.UtcNow.AddSeconds(8);
            }
            else _requiredReplacement=null;
        }

        public void StopToolReplacement()
        {
            _requiredReplacement=null;
            if(!Session.Busy) RevokeActiveToolScope();
        }
    }
}
