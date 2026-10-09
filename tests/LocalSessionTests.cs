using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Tylevo.FieldAttachments.Core;
using Tylevo.FieldAttachments.Runtime;

// Shapes verified in the installed 4.1.5 assembly; these fixtures never load game code.
namespace Comfort.Common { public static class Singleton<T> { public static T? Instance { get; set; } } }
namespace EFT
{
    public static class InGameStatus { public static bool InRaid; }
    public class GameWorld { public object? MainPlayer; }
    public class Player
    {
        public class FirearmController { public object? Item; }
        public class SinglePlayerInventoryController { public RangeTestInventory Inventory = new RangeTestInventory(); }
        public class RangeTestInventory { public object Equipment = new object(); }
        public class RangeTestHealth { public bool IsAlive = true; }
        public class RangeTestMovement { public bool BlockFirearms; }
        public bool IsYourPlayer = true;
        public object? HandsController, InventoryController;
        public RangeTestHealth HealthController = new RangeTestHealth();
        public RangeTestMovement MovementContext = new RangeTestMovement();
    }
    public class LocalPlayer : Player { }
    public class HideoutPlayer : LocalPlayer
    {
        public bool _isInShootingRange = true, IsUpdateHideoutPlayerInventoryInProgress, IsInPatrol;
        public object? ShootingRangeInventory, OriginalInventory;
    }
}
internal static partial class Program
{
    private static LocalSessionFacts ReadySession(bool hideout) => new LocalSessionFacts {
        KnownBuild=true, SamePlayer=true, SameController=true, SameWeapon=true, YourPlayer=true, Alive=true,
        PlayerType=hideout ? "EFT.HideoutPlayer" : "EFT.LocalPlayer", HandsType="EFT.Player+FirearmController",
        ControllerType="EFT.Player+SinglePlayerInventoryController", InRaid=!hideout, InShootingRange=true,
        UpdatingInventory=false, InPatrol=false, FirearmsBlocked=false, RangeInventoryMatches=true,
        OriginalInventorySeparate=true, RangeEquipmentSeparate=true
    };
    private static void TestLocalSessions()
    {
        Check(ReadySession(false).Allowed, "Native local raid remains supported");
        Check(ReadySession(true).Allowed, "Native range copy permits pose, pointer and attachment validation outside InRaid");
        foreach (bool? inRaid in new bool?[] {false,true,null})
        {
            var range=ReadySession(true); range.InRaid=inRaid;
            Check(range.Allowed, "Range eligibility uses actual native range state, independent of raid flag="+inRaid);
        }
        foreach (string type in new[] {"EFT.LocalPlayerSubclass","Fika.Core.CoopPlayer","EFT.Player",""})
        {
            var f=ReadySession(true); f.PlayerType=type;
            Check(!f.Allowed, "Unknown/custom player denied: "+type);
        }
        foreach (bool hideout in new[] {false,true})
        {
            foreach (string name in new[] {"KnownBuild","SamePlayer","SameController","SameWeapon","YourPlayer","Alive"})
            {
                var f=ReadySession(hideout); typeof(LocalSessionFacts).GetField(name)!.SetValue(f,false);
                Check(!f.Allowed, "Session fails closed on "+name+" hideout="+hideout);
            }
            var fika=ReadySession(hideout); fika.FikaLoaded=true;
            Check(!fika.Allowed, "Fika remains blocked hideout="+hideout);
            var custom=ReadySession(hideout); custom.HandsType="Custom.FirearmController";
            Check(!custom.Allowed, "Custom hands remain blocked hideout="+hideout);
            custom=ReadySession(hideout); custom.ControllerType="EFT.InventoryLogic.OfflineInventoryController";
            Check(!custom.Allowed, "Original profile controller never authorized hideout="+hideout);
        }
        foreach (string name in new[] {"InShootingRange","UpdatingInventory","InPatrol","FirearmsBlocked"})
        {
            foreach (bool? value in new bool?[] {null, name=="InShootingRange" ? false : true})
            {
                var f=ReadySession(true); typeof(LocalSessionFacts).GetField(name)!.SetValue(f,value);
                Check(!f.Allowed, "Range rejects inactive/unknown "+name+"="+value);
            }
        }
        foreach (string name in new[] {"RangeInventoryMatches","OriginalInventorySeparate","RangeEquipmentSeparate"})
        {
            var f=ReadySession(true); typeof(LocalSessionFacts).GetField(name)!.SetValue(f,false);
            Check(!f.Allowed, "Range rejects unsafe inventory ownership: "+name);
        }
        foreach (bool? value in new bool?[] {false,null})
        {
            var f=ReadySession(false); f.InRaid=value;
            Check(!f.Allowed, "Ordinary local player cannot use hideout exception with InRaid="+value);
        }

        var read=new ReadAccess(); var reader=new LocalSessionReader(read);
        var rangeInventory=new EFT.Player.SinglePlayerInventoryController();
        var original=new EFT.Player.SinglePlayerInventoryController();
        var weapon=new EFT.InventoryLogic.Weapon {Id="same-id-in-range"};
        var player=new EFT.HideoutPlayer {InventoryController=rangeInventory,ShootingRangeInventory=rangeInventory,
            OriginalInventory=original,HandsController=new EFT.Player.FirearmController {Item=weapon}};
        var world=new EFT.GameWorld {MainPlayer=player}; Comfort.Common.Singleton<EFT.GameWorld>.Instance=world;
        var snapshot=new RaidSnapshot {Player=player,Weapon=weapon,Controller=rangeInventory};
        LocalSessionFacts ReadVerifiedShape() { var f=reader.Read(snapshot); f.KnownBuild=true; return f; }
        Check(!reader.Read(snapshot).KnownBuild, "Fixture assembly cannot pass the real game MVID gate");
        Check(ReadVerifiedShape().Allowed, "Reflection reads the exact range copy, flags and current identities");
        Check(ReadVerifiedShape().Evidence.Contains("temporary equipment"), "Range report explicitly identifies temporary equipment");
        world.MainPlayer=new EFT.LocalPlayer();
        Check(!ReadVerifiedShape().Allowed, "Stale range player rejected after scene/player replacement"); world.MainPlayer=player;
        player.InventoryController=original;
        Check(!ReadVerifiedShape().Allowed, "Controller getter changing to original cannot reuse range snapshot");
        snapshot.Controller=original;
        Check(!ReadVerifiedShape().Allowed, "Even refreshed original-controller snapshot cannot authorize range actions");
        snapshot.Controller=rangeInventory; player.InventoryController=rangeInventory;
        player.OriginalInventory=rangeInventory;
        Check(!ReadVerifiedShape().Allowed, "Same original and range controller rejected"); player.OriginalInventory=original;
        object equipment=original.Inventory.Equipment; original.Inventory.Equipment=rangeInventory.Inventory.Equipment;
        Check(!ReadVerifiedShape().Allowed, "Shared original/range equipment object rejected"); original.Inventory.Equipment=equipment;
        ((EFT.Player.FirearmController)player.HandsController).Item=new EFT.InventoryLogic.Weapon {Id=weapon.Id};
        Check(!ReadVerifiedShape().Allowed, "Recloned weapon with same ID invalidates old snapshot by reference");
        ((EFT.Player.FirearmController)player.HandsController).Item=weapon;
        player.IsUpdateHideoutPlayerInventoryInProgress=true;
        Check(!ReadVerifiedShape().Allowed, "Native range inventory rebuild invalidates pose/pointer/actions");
        player.IsUpdateHideoutPlayerInventoryInProgress=false; player._isInShootingRange=false;
        Check(!ReadVerifiedShape().Allowed, "Leaving range invalidates pose/pointer/actions"); player._isInShootingRange=true;
        player.IsInPatrol=true;
        Check(!ReadVerifiedShape().Allowed, "Hideout patrol blocks attachment mode"); player.IsInPatrol=false;
        player.MovementContext.BlockFirearms=true;
        Check(!ReadVerifiedShape().Allowed, "Native firearm-block state blocks attachment mode"); player.MovementContext.BlockFirearms=false;
        player.IsYourPlayer=false;
        Check(reader.MainPlayer()==null && !ReadVerifiedShape().Allowed, "Nonlocal hideout player rejected"); player.IsYourPlayer=true;
        Check(ReadVerifiedShape().Allowed, "Stable active range becomes eligible again after ordinary exit/reentry");

        // Pending native tasks are observed, never cancelled/retried after range identity loss.
        string root=Path.Combine(Path.GetTempPath(),"TFA-range-test-"+Guid.NewGuid().ToString("N"));
        var probe=new NativeInstallProbe(read,root);
        var pending=new TaskCompletionSource<bool>();
        typeof(NativeInstallProbe).GetField("_requestContext",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(probe,snapshot);
        typeof(NativeInstallProbe).GetField("_task",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(probe,pending.Task);
        typeof(NativeInstallProbe).GetField("_submittedUtc",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(probe,DateTime.UtcNow);
        probe.Session.TryBegin(); probe.Session.MarkSubmitted();
        // A mismatched build is also lost context; the fixture deliberately cannot satisfy it.
        probe.Poll();
        Check(probe.Session.Blocked && probe.Session.Phase==InstallPhase.Unknown, "Lost range context latches pending native request UNKNOWN");
        int revision=probe.Session.Revision; probe.Poll();
        Check(probe.Session.Revision==revision && !pending.Task.IsCompleted, "Range loss logged once; pending native task is not cancelled");
        Check(!probe.Session.TryBegin(), "No duplicate/retry after range context loss");
        pending.SetResult(true); probe.Poll();
        Check(probe.Session.Blocked && probe.Session.Phase==InstallPhase.Unknown, "Late successful task cannot clear range uncertainty");
        Check(typeof(NativeInstallProbe).GetField("_requestContext",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(probe)==null,
            "Completed task releases range snapshot references");
        Comfort.Common.Singleton<EFT.GameWorld>.Instance=null;
    }
}
