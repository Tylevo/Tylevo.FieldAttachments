using System;
using System.Linq;

namespace Tylevo.FieldAttachments.Core
{
    public enum AttachmentAction { Install, Uninstall, Replace }

    // One explicit click binds identities, never a mutable highlight/index. It may
    // wait only for our inspection return; every native guard runs again afterward.
    public sealed class AttachmentRequest
    {
        public AttachmentAction Action { get; }
        public AttachmentGroup Group { get; }
        public string SlotPath { get; }
        public string ItemId { get; }
        public string ItemName { get; }
        public object Player { get; }
        public object Weapon { get; }
        public object Controller { get; }
        private readonly object _slot, _item;
        private readonly object? _sourceGrid, _sourceAddress;
        private readonly string _weaponId, _template, _location;
        private readonly bool? _required, _locked;
        private readonly bool _hasChildren;
        private readonly AttachmentAssembly? _assembly;
        public AttachmentRequest? Removal { get; }
        public static AttachmentAction ChoiceAction(SlotObservation slot) => slot.Installed==null ? AttachmentAction.Install : AttachmentAction.Replace;
        private AttachmentRequest(AttachmentAction action, RaidSnapshot snapshot, SlotObservation slot, ItemObservation item, AttachmentRequest? removal=null)
        {
            Action=action; Removal=removal; Group=slot.Group; SlotPath=slot.Path; ItemId=item.Id; ItemName=item.Name;
            Player=snapshot.Player!; Weapon=snapshot.Weapon!; Controller=snapshot.Controller!;
            _slot=slot.NativeSlot!; _item=item.NativeItem!; _weaponId=snapshot.WeaponId;
            _template=item.TemplateId; _location=item.Location; _required=slot.Required; _locked=slot.Locked;
            _sourceGrid=item.SourceGrid; _sourceAddress=item.SourceAddress;
            _hasChildren=item.HasChildren; _assembly=item.Assembly;
        }
        public static string ScopeDenial(AttachmentAction action, SlotObservation slot, ItemObservation? item)
        {
            if(action!=AttachmentAction.Install && action!=AttachmentAction.Uninstall && action!=AttachmentAction.Replace) return "Unknown attachment action.";
            if (!AttachmentAssembly.Supports(slot.Group,item))
                return "Unsupported attachment category for this slot.";
            if (item==null || slot.HiddenInQuickSwap) return "Use the intact assembly's mounting point.";
            if (slot.Required!=false || slot.Locked!=false) return "Slot is restricted or unknown.";
            if (action==AttachmentAction.Install && slot.Installed!=null) return "Uninstall the current attachment first.";
            if (action!=AttachmentAction.Uninstall && !CarriedStoragePolicy.Allows(item.Location)) return "Install from accessible carried storage only.";
            if(action==AttachmentAction.Replace)
            {
                if(slot.Installed==null) return "Attachment to replace disappeared.";
                if(slot.Installed.Id==item.Id || ReferenceEquals(slot.Installed.NativeItem,item.NativeItem)) return "Choose a different carried attachment.";
                string removal=ScopeDenial(AttachmentAction.Uninstall,slot,slot.Installed);
                if(removal.Length!=0) return "Current attachment: "+removal;
            }
            if (action==AttachmentAction.Uninstall && (slot.Installed==null || !ReferenceEquals(slot.Installed.NativeItem,item.NativeItem)))
                return "Installed attachment changed.";
            if (item.Examined!=true || item.RaidModdable!=true) return "Attachment is unexamined or not raid-moddable.";
            return "";
        }
        public static string Create(AttachmentAction action, RaidSnapshot snapshot, AttachmentGroup group,
            string path, string itemId, out AttachmentRequest? request)
        {
            request=null;
            if (snapshot.Truncated || snapshot.Warnings.Count!=0) return "Inventory scan incomplete.";
            if (snapshot.Player==null || snapshot.Weapon==null || snapshot.Controller==null || snapshot.WeaponId.Length==0)
                return "Player/weapon identity unavailable.";
            var slots=snapshot.Slots.Where(s=>s.Slot.Group==group && s.Slot.Path==path).ToArray();
            if (slots.Length!=1) return "Slot identity missing or ambiguous.";
            var slot=slots[0];
            var items=action==AttachmentAction.Uninstall ? new[] {slot.Slot.Installed}.Where(i=>i!=null && i.Id==itemId).ToArray() :
                slot.Candidates.Where(c=>c.Item.Id==itemId).Select(c=>(ItemObservation?)c.Item).ToArray();
            if (items.Length!=1 || items[0]?.NativeItem==null || slot.Slot.NativeSlot==null || itemId.Length==0 || items[0]!.TemplateId.Length==0)
                return "Attachment identity missing or ambiguous.";
            string denial=ScopeDenial(action,slot.Slot,items[0]);
            if (denial.Length!=0) return denial;
            if (action!=AttachmentAction.Uninstall && slot.Candidates.Single(c=>c.Item.Id==itemId).Evidence!=CandidateEvidence.NativeFilterPass)
                return "Native filter has not accepted this item.";
            AttachmentRequest? removal=null;
            if(action==AttachmentAction.Replace)
            {
                denial=Create(AttachmentAction.Uninstall,snapshot,group,path,slot.Slot.Installed!.Id,out removal);
                if(denial.Length!=0) return denial;
            }
            request=new AttachmentRequest(action,snapshot,slot.Slot,items[0]!,removal); return "";
        }
        public string Validate(RaidSnapshot fresh)
        {
            if (!ReferenceEquals(Player,fresh.Player) || !ReferenceEquals(Weapon,fresh.Weapon) ||
                !ReferenceEquals(Controller,fresh.Controller) || _weaponId!=fresh.WeaponId) return "Player/weapon/controller changed.";
            string denial=Create(Action,fresh,Group,SlotPath,ItemId,out var now);
            if (denial.Length!=0) return denial;
            if(!SameBinding(now!)) return "Clicked item/slot identity or state changed.";
            return Removal?.Validate(fresh) ?? "";
        }
        private bool SameBinding(AttachmentRequest now) =>
            ReferenceEquals(Player,now.Player) && ReferenceEquals(Weapon,now.Weapon) && ReferenceEquals(Controller,now.Controller) &&
            _weaponId==now._weaponId && ReferenceEquals(_slot,now._slot) && ReferenceEquals(_item,now._item) &&
            _template==now._template && _location==now._location && ReferenceEquals(_sourceGrid,now._sourceGrid) &&
            Equals(_sourceAddress,now._sourceAddress) && _required==now._required && _locked==now._locked &&
            _hasChildren==now._hasChildren && (!_hasChildren || _assembly?.Same(now._assembly)==true);
        public string AfterRemoval(RaidSnapshot fresh,out AttachmentRequest? installation)
        {
            installation=null;
            if(Action!=AttachmentAction.Replace || Removal==null) return "No replacement was authorized.";
            string denial=Create(AttachmentAction.Install,fresh,Group,SlotPath,ItemId,out var next);
            if(denial.Length!=0) return denial;
            if(!SameBinding(next!)) return "Replacement item/slot identity or source changed.";
            var old=fresh.Carried.Where(i=>i.Id==Removal.ItemId).ToArray();
            if(old.Length!=1 || !ReferenceEquals(old[0].NativeItem,Removal._item) || old[0].TemplateId!=Removal._template ||
                !CarriedStoragePolicy.Allows(old[0].Location) || old[0].SourceGrid==null || old[0].SourceAddress==null ||
                old[0].HasChildren!=Removal._hasChildren || (Removal._hasChildren && Removal._assembly?.Same(old[0].Assembly)!=true))
                return "Removed attachment could not be verified in carried storage.";
            installation=next; return "";
        }
        public string Identity => Action+" item="+ItemId+" weapon="+_weaponId+" slot="+SlotPath;
    }

    public sealed class ClickRequestQueue
    {
        public AttachmentRequest? Request { get; private set; }
        public bool Busy => Request!=null;
        private double _deadline;
        public bool TryBegin(AttachmentRequest request, InstallSession native, double now)
        {
            if (Busy || native.Busy || native.Blocked) return false;
            Request=request; _deadline=now+8; return true;
        }
        public bool Expired(double now) => Busy && now>=_deadline;
        public AttachmentRequest? Take() { var request=Request; Request=null; return request; }
    }
}
