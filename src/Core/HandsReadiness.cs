namespace Tylevo.FieldAttachments.Core
{
    // An observation, not permission to move inventory. All native gates still run on F7.
    public sealed class HandsReadiness
    {
        public const string IdleOperation = "EFT.Player+FirearmController+Idling";
        public string Operation = "UNRESOLVED";
        public bool SameWeapon;
        public bool? InventoryOpened, Aiming, TriggerPressed, InventoryLocked, HasActiveEvents;
        public bool Idle => Operation == IdleOperation;
        public string Denial => !SameWeapon ? "held weapon identity unavailable/changed" :
            !Idle ? "operation=" + Operation :
            InventoryOpened != false ? "inventory open or unknown" :
            Aiming != false ? "aiming or unknown" :
            TriggerPressed != false ? "trigger pressed or unknown" :
            InventoryLocked != false ? "inventory locked or unknown" :
            HasActiveEvents != false ? "inventory events active or unknown" : "";
        public string Display => Denial.Length == 0 ? "HANDS READY (Idling; rechecked on F7)" : "HANDS WAIT: " + Denial;
        public string Evidence => "hands=" + Operation + " sameWeapon=" + SameWeapon +
            " inventoryOpen=" + Text(InventoryOpened) + " aiming=" + Text(Aiming) + " trigger=" + Text(TriggerPressed) +
            " locked=" + Text(InventoryLocked) + " activeEvents=" + Text(HasActiveEvents);
        private static string Text(bool? value) => value?.ToString() ?? "unknown";
    }
}
