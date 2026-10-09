using System;
using System.Collections.Generic;

namespace Tylevo.FieldAttachments.Core
{
    public enum AttachmentGroup { Optic, Muzzle, Tactical, Underbarrel }
    public enum CandidateEvidence { NativeFilterPass, SameInstalledTemplate, ExplicitFilterHit, CategoryOnly, Unverified }

    // These are observations, NOT authorization to perform an inventory operation.
    public sealed class ItemObservation
    {
        public string Id = "";
        public string TemplateId = "";
        public string Name = "";
        public string RuntimeType = "";
        public string Location = "";
        public bool? RaidModdable;
        public bool? Examined;
        public bool HasChildren;
        public AttachmentAssembly? Assembly;
        public object? NativeItem;
        public object? SourceGrid, SourceAddress;
    }

    public sealed class SlotObservation
    {
        public string Id = "";
        public string Path = "";
        public AttachmentGroup Group;
        public string ParentName = "";
        public bool? Required;
        public bool? Locked;
        public ItemObservation? Installed;
        public object? NativeSlot;
        public bool HiddenInQuickSwap;
        public readonly HashSet<string> ExplicitFilterIds = new HashSet<string>(StringComparer.Ordinal);
    }

    public sealed class CandidateObservation
    {
        public ItemObservation Item = new ItemObservation();
        public CandidateEvidence Evidence;
        public string Detail = "";
    }

    public sealed class SlotViewModel
    {
        public SlotObservation Slot = new SlotObservation();
        public readonly List<CandidateObservation> Candidates = new List<CandidateObservation>();
    }

    // Only entries in explicitly enabled roots are classified. Non-mod names/IDs are not exported.
    public sealed class ScanEntryObservation
    {
        public string EntryType = "";
        public string ItemType = "";
        public string Outcome = "";
    }
    public sealed class ScanRootObservation
    {
        public string Root = "";
        public string Status = "not found";
        public int Grids;
        public int Entries;
        public int NonMods;
        public int UnresolvedEntries;
        public int AcceptedMods;
        public int ExcludedUnexamined;
        public int ExcludedAssemblies;
        public int ExcludedContainers;
        public readonly List<ScanEntryObservation> Details = new List<ScanEntryObservation>();
    }

    public sealed class RaidSnapshot
    {
        public DateTime CapturedUtc = DateTime.UtcNow;
        public string Status = "Not scanned";
        public string WeaponId = "";
        public string WeaponName = "No held weapon";
        public string PlayerType = "";
        public string HandsType = "";
        public string InventoryControllerType = "";
        public string SessionContext = "Not observed";
        public string ScanScope = CarriedStoragePolicy.Description;
        public string CurrentHandsOperationType = "";
        public bool? InventoryOpened;
        public bool? IsAiming;
        public bool? IsTriggerPressed;
        public bool? InventoryLocked;
        public readonly List<ScanRootObservation> ScanRoots = new List<ScanRootObservation>();
        public int TraversedItems;
        public int ExcludedUnexamined;
        public int ExcludedAssemblies;
        public bool Truncated;
        public bool InputContextMissing;
        public object? Player;
        public object? Weapon;
        public object? Controller;
        public readonly List<ItemObservation> Carried = new List<ItemObservation>();
        public readonly List<SlotViewModel> Slots = new List<SlotViewModel>();
        public readonly List<string> Warnings = new List<string>();
    }
}
