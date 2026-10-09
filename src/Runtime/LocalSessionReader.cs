using System;
using System.Linq;
using Tylevo.FieldAttachments.Core;

namespace Tylevo.FieldAttachments.Runtime
{
    public sealed class LocalSessionReader
    {
        private readonly ReadAccess _read;
        private Type? _singleton;
        public LocalSessionReader(ReadAccess read) { _read = read; }
        public object? MainPlayer()
        {
            Type? world = _read.FindType("EFT.GameWorld"), singleton = _read.FindType("Comfort.Common.Singleton`1");
            if (world == null || singleton == null) return null;
            _singleton ??= singleton.MakeGenericType(world);
            object? player = _read.Get(_read.Get(_singleton, "Instance"), "MainPlayer");
            return ReadAccess.Bool(_read.Get(player, "IsYourPlayer")) == true ? player : null;
        }
        public LocalSessionFacts Read(RaidSnapshot snapshot)
        {
            object? player = snapshot.Player, hands = _read.Get(player, "HandsController");
            // Use the virtual property. Never fall back to the raid field or OriginalInventory in the hideout.
            object? controller = _read.Get(player, "InventoryController");
            var facts = new LocalSessionFacts {
                KnownBuild = _read.FindType("EFT.Player")?.Module.ModuleVersionId.ToString() == NativeInstallProbe.InspectedGameMvid,
                FikaLoaded = AppDomain.CurrentDomain.GetAssemblies().Any(a => (a.GetName().Name ?? "").StartsWith("Fika", StringComparison.OrdinalIgnoreCase)),
                PlayerType = player?.GetType().FullName ?? "", HandsType = hands?.GetType().FullName ?? "",
                ControllerType = controller?.GetType().FullName ?? "",
                SamePlayer = player != null && ReferenceEquals(MainPlayer(), player),
                SameController = controller != null && ReferenceEquals(controller, snapshot.Controller),
                SameWeapon = snapshot.Weapon != null && ReferenceEquals(_read.Get(hands, "Item"), snapshot.Weapon),
                InRaid = ReadAccess.Bool(_read.Get(_read.FindType("EFT.InGameStatus"), "InRaid")),
                YourPlayer = ReadAccess.Bool(_read.Get(player, "IsYourPlayer")),
                Alive = ReadAccess.Bool(_read.Get(_read.Get(player, "HealthController"), "IsAlive"))
            };
            if (facts.Hideout)
            {
                object? range = _read.Get(player, "ShootingRangeInventory"), original = _read.Get(player, "OriginalInventory");
                object? equipment = _read.Get(_read.Get(range, "Inventory"), "Equipment");
                object? originalEquipment = _read.Get(_read.Get(original, "Inventory"), "Equipment");
                facts.InShootingRange = ReadAccess.Bool(_read.Get(player, "_isInShootingRange"));
                facts.UpdatingInventory = ReadAccess.Bool(_read.Get(player, "IsUpdateHideoutPlayerInventoryInProgress"));
                facts.InPatrol = ReadAccess.Bool(_read.Get(player, "IsInPatrol"));
                facts.FirearmsBlocked = ReadAccess.Bool(_read.Get(_read.Get(player, "MovementContext"), "BlockFirearms"));
                facts.RangeInventoryMatches = range != null && ReferenceEquals(controller, range);
                facts.OriginalInventorySeparate = original != null && range != null && !ReferenceEquals(range, original);
                facts.RangeEquipmentSeparate = equipment != null && originalEquipment != null && !ReferenceEquals(equipment, originalEquipment);
            }
            return facts;
        }
    }
}
