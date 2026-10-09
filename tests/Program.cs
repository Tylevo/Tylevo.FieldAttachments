using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Tylevo.FieldAttachments.Core;
using Tylevo.FieldAttachments.Runtime;

internal static partial class Program
{
    private static int _passed;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("FAILED: " + message);
        Console.WriteLine("PASS: " + message); _passed++;
    }
    private static ItemObservation Item(string id, string tpl = "tpl-a", string type = "EFT.InventoryLogic.OpticScope")
    {
        return new ItemObservation { Id = id, TemplateId = tpl, Name = "Scope " + id, RuntimeType = type, Examined = true, RaidModdable=true };
    }
    private static int Sample(int value) { return value > 2 ? value + 1 : value - 1; }
    private static int WithCallSample(int value) { return Sample(value); }
    private static T GenericSample<T>(T value) { return value; }
    private static int ExceptionSample(int value) { try { return value / 2; } finally { GC.KeepAlive(value); } }
    private static async Task<int> AsyncSample() { await Task.Yield(); return 1; }
    private static int Main()
    {
        try
        {
            TestInspectionPose();
            TestStmPresentation();
            TestCustomStmInspection();
            TestStandalonePoseTimeline();
            TestFiringHandPresentation();
            TestInstalledWeaponAliases();
            TestCustomMcxBackport();
            TestCustomM4M870Backport();
            TestGeneratedPresentationFamilies();
            TestAuditedWeaponPresentations();
            TestAuthoredSupportHand();
            TestMcxInspectionSegment();
            TestGlobalMcxInspection();
            TestLongRifleMcxInspection();
            TestCategoryMcxInspection();
            TestLocalSessions();
            TestSpatialCards();
            TestPanelArrangement();
            TestWeaponCardBasis();
            TestCardTuning();
            TestCardReadability();
            TestPointer();
            TestAltMenuInput();
            TestQuickSwap();
            TestOpticAssemblies();
            TestClickActions();
            TestReplacements();
            TestPoseResume();
            TestCarriedStorage();
            TestAttachmentScope();
            TestHexSelectors();
            TestCrossSelectors();
            TestStudioCrossLayout();
            TestWeaponPanelAttachment();
            TestCrossPresentation();
            TestCameraFacingCards();
            TestDedicatedCards();
            // CanvasScaler's matching-width/height virtual canvas at common aspect ratios.
            foreach (float aspect in new[] { 4f / 3, 16f / 10, 16f / 9, 21f / 9, 32f / 9 })
            {
                float width = (float)Math.Sqrt(1920 * 1080 * aspect), height = width / aspect;
                var panels = Enum.GetValues(typeof(AttachmentGroup)).Cast<AttachmentGroup>()
                    .Select(group => OverlayLayout.Panel(group, width, height)).ToArray();
                Check(panels.All(p => p.x >= 24 && p.x + OverlayLayout.PanelWidth <= width - 23.9f &&
                    p.y >= 136 && p.y + OverlayLayout.PanelHeight <= height - 151.9f),
                    "All group panels stay below header and above footer at aspect " + aspect);
                bool overlaps = false;
                for (int i = 0; i < panels.Length; i++)
                    for (int j = i + 1; j < panels.Length; j++)
                        overlaps |= Math.Abs(panels[i].x - panels[j].x) < OverlayLayout.PanelWidth &&
                            Math.Abs(panels[i].y - panels[j].y) < OverlayLayout.PanelHeight;
                Check(!overlaps, "Group panels do not overlap at aspect " + aspect);
            }
            Check(OverlayLayout.Project(952, 248.25f, 1, 1904, 993, 1920, 1080, out float px, out float py) &&
                px == 960 && py == 810, "Projection accounts for canvas scaling and inverted screen Y");
            Check(!OverlayLayout.Project(10, 10, -1, 1920, 1080, 1920, 1080, out _, out _), "Behind-camera anchors are hidden");
            Check(!OverlayLayout.Project(10, 10, 0, 1920, 1080, 1920, 1080, out _, out _), "Camera-plane anchors are hidden");
            Check(!OverlayLayout.Project(-1, 10, 1, 1920, 1080, 1920, 1080, out _, out _) &&
                !OverlayLayout.Project(10, 1081, 1, 1920, 1080, 1920, 1080, out _, out _), "Off-screen anchors are hidden");
            Check(!OverlayLayout.Project(float.NaN, 10, 1, 1920, 1080, 1920, 1080, out _, out _) &&
                !OverlayLayout.Project(10, 10, float.PositiveInfinity, 1920, 1080, 1920, 1080, out _, out _), "Nonfinite projections are hidden");
            Check(!OverlayLayout.Project(10, 10, 1, 0, 1080, 1920, 1080, out _, out _) &&
                !OverlayLayout.Project(10, 10, 1, 1920, 1080, 0, 1080, out _, out _), "Uninitialized display sizes are hidden");

            // Regression: hold F8, then use another control. Old exact-chord
            // checks closed the overlay or suppressed the action in this case.
            const int none = 0, f8 = 1, f9 = 2, f10 = 3, left = 4, right = 5,
                up = 6, down = 7, pageUp = 8, pageDown = 9, enter = 10, ctrl = 11, move = 12, f7 = 13, keypadEnter = 14, f6 = 15;
            var heldKeys = new HashSet<int>();
            var downKeys = new HashSet<int>();
            int[] noModifiers = Array.Empty<int>();
            Check(!KeyChord.IsHeld(f8, noModifiers, heldKeys.Contains), "Overlay inactive without activation key");
            heldKeys.Add(f8);
            Check(KeyChord.IsHeld(f8, noModifiers, heldKeys.Contains), "F8 activates held overlay");
            foreach (int extra in new[] { left, right, up, down, pageUp, pageDown, enter, keypadEnter, f6, f7, f9, f10, move })
            {
                heldKeys.Add(extra);
                Check(KeyChord.IsHeld(f8, noModifiers, heldKeys.Contains), "F8 remains active with secondary key " + extra);
                heldKeys.Remove(extra);
            }
            heldKeys.Add(f9); downKeys.Add(f9);
            Check(KeyChord.IsDown(f9, noModifiers, heldKeys.Contains, downKeys.Contains), "F8 + F9 triggers rescan");
            downKeys.Clear();
            Check(!KeyChord.IsDown(f9, noModifiers, heldKeys.Contains, downKeys.Contains), "Held F9 does not trigger every frame");
            heldKeys.Remove(f9); heldKeys.Add(f10); downKeys.Add(f10);
            Check(KeyChord.IsDown(f10, noModifiers, heldKeys.Contains, downKeys.Contains), "F8 + F10 triggers export");
            heldKeys.Add(f7); downKeys.Add(f7);
            Check(KeyChord.IsDown(f7, noModifiers, heldKeys.Contains, downKeys.Contains), "F8 + F7 keeps explicit execution chord working");
            heldKeys.Remove(f7); downKeys.Remove(f7);
            heldKeys.Add(f6); downKeys.Add(f6);
            Check(KeyChord.IsDown(f6, noModifiers, heldKeys.Contains, downKeys.Contains), "F8 plus configured F6 arm shortcut reaches staging");
            heldKeys.Remove(f6); downKeys.Remove(f6);
            Check(!KeyChord.IsHeld(f8, new[] { ctrl }, heldKeys.Contains), "Configured modifier still required");
            heldKeys.Add(ctrl);
            Check(KeyChord.IsHeld(f8, new[] { ctrl }, heldKeys.Contains), "Configured modifier accepted with extra keys");
            heldKeys.Add(left);
            Check(KeyChord.IsDown(f10, new[] { ctrl }, heldKeys.Contains, downKeys.Contains), "Modified action tolerates activation and navigation keys");
            heldKeys.Remove(f8);
            Check(!KeyChord.IsHeld(f8, noModifiers, heldKeys.Contains), "Releasing F8 closes hold mode even with another key held");
            heldKeys.Add(none); downKeys.Add(none);
            Check(!KeyChord.IsHeld(none, noModifiers, heldKeys.Contains), "Unbound activation key never matches");
            Check(!KeyChord.IsDown(none, noModifiers, heldKeys.Contains, downKeys.Contains), "Unbound action key never matches");
            Check(KeyChord.IsHeld(f10, null, heldKeys.Contains), "Null modifier collection means no modifiers");
            heldKeys.Remove(f10);
            Check(!KeyChord.IsDown(f10, noModifiers, heldKeys.Contains, downKeys.Contains), "Down marker without held main key does not match");

            Check(!SelectionState.StageChangesInventory, "SelectionState staging itself cannot change inventory");
            Check(SelectionState.Wrap(-1, 4) == 3, "Negative group wrapping");
            Check(SelectionState.Wrap(3, 0) == 0, "Empty candidate navigation");
            Check(CandidatePolicy.TryGroup("mod_scope_002", out var g) && g == AttachmentGroup.Optic, "Nested numbered optic slots");
            Check(CandidatePolicy.TryGroup("mod_muzzle", out g) && g == AttachmentGroup.Muzzle, "Muzzle grouping");
            Check(CandidatePolicy.TryGroup("mod_tactical001", out g) && g == AttachmentGroup.Tactical, "Tactical naming variation");
            Check(CandidatePolicy.TryGroup("mod_foregrip", out g) && g == AttachmentGroup.Underbarrel, "Foregrip grouping");
            Check(!CandidatePolicy.TryGroup("mod_stock", out _), "Stocks intentionally outside v0.1");
            Check(!CandidatePolicy.TryGroup("mod_magazine", out _), "Magazines intentionally outside v0.1");
            var slot = new SlotObservation { Id = "mod_scope", Path = "gun/rail/scope", Group = AttachmentGroup.Optic, Required=false, Locked=false, Installed = Item("installed") };
            var same = Item("same"); var filtered = Item("filter", "tpl-b"); var typeOnly = Item("type", "tpl-c"); var unknown = Item("unknown", "tpl-d", "EFT.InventoryLogic.OtherMod");
            slot.ExplicitFilterIds.Add("tpl-b");
            Check(CandidatePolicy.Assess(slot, same) == CandidateEvidence.SameInstalledTemplate, "Same template is evidence, not authorization");
            Check(CandidatePolicy.Assess(slot, filtered) == CandidateEvidence.ExplicitFilterHit, "Exact filter hit is partial evidence");
            Check(CandidatePolicy.Assess(slot, typeOnly) == CandidateEvidence.CategoryOnly, "Same subclass remains unverified fit");
            Check(CandidatePolicy.Assess(slot, unknown) == CandidateEvidence.Unverified, "Unknown fit stays unknown");
            var carried = new[] { unknown, typeOnly, filtered, same, same, slot.Installed! };
            same.Name="A sight"; filtered.Name="B sight"; typeOnly.Name="C sight";
            var list = CandidatePolicy.Build(slot, carried, true, false,(s,i)=>true);
            Check(list.Count == 3, "Exclude duplicates, installed item and all-unknown by default");
            Check(list[0].Item.Id == "same" && list[1].Item.Id == "filter", "Native-fitting choices have stable name ordering");
            Check(CandidatePolicy.Build(slot, carried, false, false).Count == 0, "Unknown native fit hides candidates");
            Check(CandidatePolicy.Build(slot, carried, false, true).Count == 0, "Legacy diagnostic option cannot show non-fitting choices");
            slot.Locked = true; Check(CandidatePolicy.Build(slot, carried, true, true).Count == 0, "Locked slot has no candidates"); slot.Locked = false;
            slot.Required = true; Check(CandidatePolicy.Build(slot, carried, true, true).Count == 0, "Required slot has no candidates"); slot.Required = false;
            var hidden = Item("hidden"); hidden.Examined = null;
            Check(CandidatePolicy.Build(slot, new[] { hidden }, true, true).Count == 0, "Unresolved examination fails closed");
            hidden.Examined = false; Check(CandidatePolicy.Build(slot, new[] { hidden }, true, true).Count == 0, "Unexamined identity is excluded");
            var assembly = Item("assembly"); assembly.HasChildren = true;
            Check(CandidatePolicy.Build(slot, new[] { assembly }, true, true).Count == 0, "Mount assemblies excluded");
            var generic = new SlotObservation { Installed = Item("i", "a", "EFT.InventoryLogic.Mod") };
            Check(CandidatePolicy.Assess(generic, Item("j", "b", "EFT.InventoryLogic.Mod")) == CandidateEvidence.Unverified, "Generic Mod class is not a category hint");
            var snapshot = new RaidSnapshot { WeaponId = "gun" }; var view = new SlotViewModel { Slot = slot }; view.Candidates.AddRange(list); snapshot.Slots.Add(view);
            var state = new SelectionState(); state.ReplaceSnapshot(snapshot);
            string result = state.Stage();
            Check(state.StagedItemId == "same" && result.Contains("ARMED"), "Staging stores only UI selection");
            Check(same.Location == "" && slot.Installed!.Id == "installed", "Staging leaves item/slot observations untouched");
            state.NextCandidate(-1); Check(state.Selected()?.Item.Id == "type", "Candidate navigation wraps");
            state.NextGroup(-1); Check(state.Group == AttachmentGroup.Underbarrel, "Group navigation wraps");
            Check(state.Selected() == null, "Absent group handled");
            state.ReplaceSnapshot(new RaidSnapshot()); Check(state.StagedItemId == "" && state.StagedSlotPath == "", "Rescan clears stale staged selection");
            var emptySnapshot = new RaidSnapshot();
            emptySnapshot.Slots.Add(new SlotViewModel { Slot = new SlotObservation { Id = "mod_scope", Path = "gun/a/scope", Group = AttachmentGroup.Optic } });
            emptySnapshot.Slots.Add(new SlotViewModel { Slot = new SlotObservation { Id = "mod_scope", Path = "gun/b/scope", Group = AttachmentGroup.Optic } });
            var emptyState = new SelectionState(); emptyState.ReplaceSnapshot(emptySnapshot);
            emptyState.NextSlot(1);
            Check(emptyState.SlotIndex(AttachmentGroup.Optic) == 1 && emptyState.Current(AttachmentGroup.Optic)?.Slot.Path == "gun/b/scope", "Slot cycling works without carried candidates");
            emptyState.NextCandidate(1);
            Check(emptyState.Selected() == null && emptyState.CandidateIndex(AttachmentGroup.Optic) == 0, "Empty candidate list remains empty when navigating");
            Check(emptyState.Stage().Contains("No candidate"), "Enter on empty list reports no candidate");
            emptyState.NextGroup(1);
            Check(emptyState.Group == AttachmentGroup.Muzzle, "Group cycling works without carried candidates");
            Check(JsonText.Quote("a\"b\\c\n") == "\"a\\\"b\\\\c\\n\"", "JSON quotes backslashes and controls");
            Check(JsonText.Bool(null) == "null" && JsonText.Bool(false) == "false", "Unknown booleans remain unknown");
            MethodInfo m = typeof(Program).GetMethod("Sample", BindingFlags.NonPublic | BindingFlags.Static)!;
            Check(IlText.Signature(m).Contains("Sample"), "Signature exporter handles managed methods");
            string il = IlText.Dump(m);
            Check(il.Contains("IL_") && il.Contains("ret"), "IL decoder handles local managed method");
            Check(IlText.Dump(m, 1).Contains("partial"), "IL export enforces instruction cap");
            // 0.2.0: actual leaf attachments should be shown before their parent mounts or empty slots.
            var hierarchy = new RaidSnapshot { WeaponId = "m4" };
            var empty = new SlotViewModel { Slot = new SlotObservation { Group = AttachmentGroup.Optic, Path = "m4/empty" } };
            var mount = new SlotViewModel { Slot = new SlotObservation { Group = AttachmentGroup.Optic, Path = "m4/mount", Installed = Item("mount", "mount-tpl", "EFT.InventoryLogic.Mount") } };
            mount.Slot.Installed!.HasChildren = true;
            var leaf = new SlotViewModel { Slot = new SlotObservation { Group = AttachmentGroup.Optic, Path = "m4/optic", Installed = Item("optic") } };
            leaf.Candidates.Add(new CandidateObservation { Item = Item("spare") });
            hierarchy.Slots.AddRange(new[] { empty, mount, leaf });
            var ui = new SelectionState(); ui.ReplaceSnapshot(hierarchy);
            Check(ui.Current(AttachmentGroup.Optic)?.Slot.Path == "m4/optic", "Functional leaf selected ahead of mount and empty slot");
            ui.Stage(); ui.ReplaceSnapshot(hierarchy, true);
            Check(ui.StagedItemId == "spare" && ui.Current(AttachmentGroup.Optic)?.Slot.Path == "m4/optic", "Fresh same-weapon report preserves a valid staged item");
            ui.NextSlot(1);
            Check(ui.Current(AttachmentGroup.Optic)?.Slot.Path == "m4/mount", "Mount remains accessible by slot cycling");
            ui.ReplaceSnapshot(hierarchy, true);
            Check(ui.Current(AttachmentGroup.Optic)?.Slot.Path == "m4/mount", "Export preserves current slot by identity");
            var disappeared = new RaidSnapshot { WeaponId = "m4" };
            disappeared.Slots.Add(empty);
            ui.ReplaceSnapshot(disappeared, true);
            Check(ui.StagedItemId == "" && ui.Current(AttachmentGroup.Optic)?.Slot.Path == "m4/empty", "Missing slot or item clears stale stage during export");
            ui.ReplaceSnapshot(hierarchy); ui.Stage();
            ui.ReplaceSnapshot(new RaidSnapshot { WeaponId = "other-gun" }, true);
            Check(ui.StagedItemId == "", "Weapon change never retains staged attachment");
            TestArming();
            var ignoredGeneric = typeof(Program).GetMethod("GenericSample", BindingFlags.Static | BindingFlags.NonPublic)!;
            Check(IlText.Signature(ignoredGeneric).Contains("GenericSample<T>"), "Export preserves generic method arguments");
            Check(IlText.TypeName(typeof(Dictionary<string, int[]>)) == "System.Collections.Generic.Dictionary<System.String,System.Int32[]>", "Compact generic type output excludes repeated assembly qualification");
            var refs = new List<MethodBase>();
            IlText.Dump(typeof(Program).GetMethod("WithCallSample", BindingFlags.Static | BindingFlags.NonPublic)!, 600, refs);
            Check(refs.Any(x => x.Name == "Sample"), "Referenced-method metadata is collected without invoking the method");
            string eh = IlText.Dump(typeof(Program).GetMethod("ExceptionSample", BindingFlags.Static | BindingFlags.NonPublic)!);
            Check(eh.Contains("EH Finally") && eh.Contains("handler=["), "Export includes exception-handler boundaries");
            var asyncBody = IlText.StateMachineBodies(typeof(Program).GetMethod("AsyncSample", BindingFlags.Static | BindingFlags.NonPublic)!);
            Check(asyncBody.Any(x => x.Name == "MoveNext"), "Async state-machine implementation can be located through metadata");
            Check(IlText.StateMachineBodies(m).Count == 0, "Ordinary method has no invented async state machine");
            // Pure policy/lifecycle tests: not Unity behavior or proof of native rollback.
            InstallFacts ValidFacts() => new InstallFacts {
                Enabled=true, KnownBuild=true, SinglePlayer=true, CurrentSelection=true,
                SnapshotComplete=true, SourceIsCarried=true, SupportedLeafAttachment=true,
                SupportedContext=true, YourPlayer=true, Alive=true, InventoryOpened=false, Aiming=false,
                TriggerPressed=false, InventoryLocked=false, HasActiveEvents=false, Idle=true,
                Required=false, Locked=false, Deleted=false, Empty=true, Examined=true,
                RaidModdable=true, FilterPass=true, NeutralPinState=true
            };
            Check(InstallGate.Denial(ValidFacts()) == "", "Verified pure policy facts permit attempting native validation");
            foreach (FieldInfo fact in typeof(InstallFacts).GetFields())
            {
                if (fact.Name == "Removing" || fact.Name == "InstalledItemMatches") continue; // Direction-specific facts tested in ClickActionTests.
                var f = ValidFacts();
                object? good = fact.GetValue(f);
                if (fact.FieldType == typeof(bool))
                {
                    fact.SetValue(f, !(bool)good!);
                    Check(InstallGate.Denial(f).Length > 0, "Flipped required policy fact denies: " + fact.Name);
                }
                else if (fact.FieldType == typeof(bool?))
                {
                    fact.SetValue(f, null);
                    Check(InstallGate.Denial(f).Length > 0, "Unknown safety fact denies: " + fact.Name);
                    fact.SetValue(f, !(bool)good!);
                    Check(InstallGate.Denial(f).Length > 0, "Invalid safety fact denies: " + fact.Name);
                }
            }
            Check(CandidatePolicy.Build(slot, new[] { same }, true, true, (a,b) => false).Count == 0,
                "Native filter rejection overrides same-template and unknown-hint display");
            unknown.RuntimeType="EFT.InventoryLogic.Collimator";
            var nativeAccepted = CandidatePolicy.Build(slot, new[] { unknown }, false, false, (a,b) => true);
            Check(nativeAccepted.Count == 1 && nativeAccepted[0].Evidence == CandidateEvidence.NativeFilterPass,
                "Native filter acceptance can show a candidate without heuristic type/ID evidence");
            Check(CandidatePolicy.Build(slot, new[] { same }, false, false, (a,b) => null).Count == 0,
                "Unknown filter binding cannot advertise fit from template hints");
            var flight = new InstallSession();
            Check(flight.TryBegin() && !flight.TryBegin(), "One validation at a time");
            flight.MarkSubmitted();
            Check(flight.Phase == InstallPhase.Pending, "Submitted request has explicit PENDING phase");
            bool duplicateSubmitBlocked = false;
            try { flight.MarkSubmitted(); } catch (InvalidOperationException) { duplicateSubmitBlocked = true; }
            Check(duplicateSubmitBlocked, "A request cannot be marked submitted twice");
            int pendingRevision = flight.Revision;
            flight.RejectRequest("duplicate F7");
            Check(flight.Revision == pendingRevision && flight.Phase == InstallPhase.Pending, "Duplicate F7 cannot replace original pending status");
            new SelectionState().ReplaceSnapshot(new RaidSnapshot());
            Check(flight.Busy && !flight.TryBegin(), "UI snapshot reset cannot authorize another in-flight operation");
            flight.TimedOut();
            Check(flight.Busy && flight.Blocked && !flight.TryBegin(), "Timeout remains in-flight and blocks retries");
            int timeoutRevision = flight.Revision; flight.TimedOut();
            Check(flight.Revision == timeoutRevision && flight.Phase == InstallPhase.Unknown, "Timeout transition is recorded once and labeled UNKNOWN");
            flight.Finish("late successful completion", false, true);
            Check(!flight.Busy && flight.Blocked && !flight.TryBegin(), "Late completion never clears uncertainty latch");
            Check(flight.Phase == InstallPhase.Unknown, "Late success keeps UNKNOWN latch visible");
            var completed = new InstallSession(); completed.TryBegin(); completed.MarkSubmitted(); completed.Finish("verified", false, true);
            Check(completed.Phase == InstallPhase.Succeeded, "Verified success has explicit SUCCEEDED phase");
            var rejected = new InstallSession(); rejected.TryBegin(); rejected.Finish("guard rejection", false);
            Check(!rejected.Blocked && rejected.TryBegin(), "Known pre-submit rejection permits a new explicit request");
            rejected.Finish("unknown", true);
            Check(rejected.Blocked && !rejected.TryBegin(), "Uncertain exception latches future live actions");
            var emptyRead = new ReadAccess();
            Check(new NativeFilter(emptyRead).Accepts(null, null) == null, "No live game bindings means filter UNKNOWN, not pass");
            string journalRoot = Path.Combine(Path.GetTempPath(), "TFA-journal-tests-" + Guid.NewGuid().ToString("N"));
            var noGame = new NativeInstallProbe(emptyRead, journalRoot);
            noGame.RecordUi("F7_REJECTED before probe: NOT ARMED");
            Check(noGame.History.Contains("F7_REJECTED") && !noGame.History.Contains("PROBE_STARTED"), "F10 probe history includes UI rejection without implying native invocation");
            Check(File.ReadAllText(noGame.SessionJournalPath).Contains("F7_REJECTED before probe: NOT ARMED"),
                "Early rejection is persisted immediately without F10 or native probe");
            Check(DateTime.TryParse(File.ReadAllLines(noGame.SessionJournalPath)[2].Split('|')[0], out _), "Persisted event carries a parseable timestamp");
            string capturedHistory = noGame.History;
            noGame.TryInstall(new RaidSnapshot(), null, null, false, false);
            Check(!noGame.Session.Busy && noGame.Session.Status.Contains("disabled"), "Disabled native boundary rejects before any inventory operation");
            Check(noGame.Session.Phase == InstallPhase.Rejected, "Native guard rejection has explicit REJECTED phase");
            Check(!capturedHistory.Contains("PROBE_STARTED") && File.ReadAllText(noGame.SessionJournalPath).Contains("PROBE_STARTED"),
                "Automatic journal advances independently of an earlier F10 history copy");
            Check(File.ReadAllText(noGame.SessionJournalPath).Contains("REQUEST_HANDS hands=UNRESOLVED"), "Request persists freshly sampled hands type even when unavailable");
            noGame.Session.TryBegin(); noGame.Session.MarkSubmitted();
            int beforeDuplicate = noGame.Session.Revision;
            noGame.TryInstall(new RaidSnapshot(), null, null, true, true);
            Check(noGame.Session.Revision == beforeDuplicate && noGame.History.Contains("REQUEST_REJECTED duplicate"), "Duplicate probe entry is journaled and leaves original request untouched");
            typeof(NativeInstallProbe).GetMethod("Finish", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(noGame, new object[] { "unit completion fixture", false, true });
            Check(File.ReadAllText(noGame.SessionJournalPath).Contains("SUCCEEDED unit completion fixture"), "Completion path persists its outcome without F10 (unit fixture, not native success); journal error=" + noGame.JournalError);
            for (int eventIndex = 0; eventIndex < 520; eventIndex++) noGame.RecordUi("KEY fixture-" + eventIndex);
            string bounded = File.ReadAllText(noGame.SessionJournalPath);
            Check(!File.ReadAllLines(noGame.SessionJournalPath).Any(line => line.EndsWith("UI KEY fixture-0", StringComparison.Ordinal)) && bounded.Contains("KEY fixture-519") &&
                File.ReadAllLines(noGame.SessionJournalPath).Length == 514, "Automatic journal retains only 512 events plus provenance header; journal error=" + noGame.JournalError);
            string blockedRoot = Path.Combine(journalRoot, "not-a-directory"); File.WriteAllText(blockedRoot, "fixture");
            var failedJournal = new NativeInstallProbe(emptyRead, blockedRoot);
            failedJournal.RecordUi("KEY Return"); failedJournal.TryInstall(new RaidSnapshot(), null, null, true, true);
            Check(failedJournal.JournalError.Length > 0 && failedJournal.History.Contains("KEY Return") && !failedJournal.Session.Submitted &&
                !failedJournal.Session.Busy && failedJournal.Session.Status.Contains("journal unavailable"), "Failed journal write retains memory and prevents native submission");
            Directory.Delete(journalRoot, true); // Unique directory created by this test under the system temp root.
            Console.WriteLine("\n" + _passed + " assertions passed. These are core/metadata tests, NOT Unity/SPT/Fika tests.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }

    private static void TestArming()
    {
        object player = new object(), weapon = new object(), controller = new object(), nativeItem = new object(), nativeSlot = new object();
        RaidSnapshot Snapshot(bool reverse = false)
        {
            var s = new RaidSnapshot { WeaponId = "m4", Player = player, Weapon = weapon, Controller = controller };
            var target = new SlotViewModel { Slot = new SlotObservation { Id = "mod_tactical", Path = "m4/m-lok-off/mod_tactical",
                Group = AttachmentGroup.Tactical, ParentName = "M-LOK Off", Required = false, Locked = false, NativeSlot = nativeSlot } };
            var wmx = Item("real-wmx200", "wmx-template", "EFT.InventoryLogic.TacticalCombo");
            wmx.Name = "WMX200"; wmx.Location = "Pockets"; wmx.NativeItem = nativeItem;
            var other = Item("other"); other.Location = "Pockets";
            target.Candidates.Add(new CandidateObservation { Item = wmx, Evidence = CandidateEvidence.NativeFilterPass });
            target.Candidates.Add(new CandidateObservation { Item = other });
            var second = new SlotViewModel { Slot = new SlotObservation { Id = "mod_tactical", Path = "m4/other-mount/mod_tactical", Group = AttachmentGroup.Tactical } };
            s.Slots.AddRange(new[] { target, second }); s.Carried.AddRange(new[] { wmx, other });
            if (reverse) { s.Slots.Reverse(); target.Candidates.Reverse(); }
            return s;
        }
        var state = new SelectionState(); var history = new List<string>(); state.Transition += history.Add;
        state.ReplaceSnapshot(Snapshot()); state.NextGroup(2);
        var session = new InstallSession();
        Check(!state.ArmedMatches && state.ExecutionDenial(true, session).Contains("NOT ARMED"), "Highlight alone never authorizes execution");
        state.Stage();
        Check(state.ArmedMatches && !session.Busy && session.Phase == InstallPhase.None, "Enter arms exact identity without starting any request");
        state.ClearStage("alternate key test"); state.Stage("F6");
        Check(state.ArmedMatches && !session.Busy && history.Last().Contains("ARM_ACCEPTED F6") &&
            state.ExecutionDenial(true, session, true).Contains("together"), "F6 uses the same arm and same-frame guard, with its actual input named");
        var hands = new HandsReadiness { SameWeapon = true, Operation = "EFT.Player+FirearmController+UtilityOperation",
            InventoryOpened = false, Aiming = false, TriggerPressed = false, InventoryLocked = false, HasActiveEvents = false };
        Check(hands.Denial.Contains("UtilityOperation") && !hands.Idle && state.ArmedMatches,
            "Armed selection is independent of busy UtilityOperation hands");
        hands.Operation = HandsReadiness.IdleOperation;
        Check(hands.Denial.Length == 0 && state.ArmedMatches && !session.Busy,
            "Becoming idle preserves arm and never starts a deferred request");
        hands.HasActiveEvents = null;
        Check(hands.Denial.Contains("unknown"), "Unknown native readiness fact cannot display READY");
        hands.HasActiveEvents = false; hands.SameWeapon = false;
        Check(hands.Denial.Contains("identity"), "Changed weapon cannot display READY for the old selection");
        Check(state.ExecutionDenial(true, session, true).Contains("together"), "Enter plus F7 in one frame requires a separate execute press");
        Check(state.ExecutionDenial(false, session).Contains("OFF") && state.ArmedMatches, "Live OFF rejects F7 without secretly consuming or executing the arm");
        state.ReplaceSnapshot(Snapshot(true), true, "F9 rescan");
        Check(state.ArmedMatches && state.Selected()?.Item.Id == "real-wmx200" && state.SlotIndex(AttachmentGroup.Tactical) == 1,
            "F9 preserves exact armed item and slot across both list reorderings");
        state.ReplaceSnapshot(Snapshot(), true, "F10 report refresh");
        Check(state.ArmedMatches && state.ExecutionDenial(true, session) == "", "F10 preserves unchanged identity; explicit F7 may reach native validation");
        Check(history.Any(x => x.Contains("ARM_PRESERVED F9")) && history.Any(x => x.Contains("ARM_PRESERVED F10")), "Refresh transition reasons are recorded");
        state.ClearStage("arm consumed by explicit F7 request");
        Check(state.ExecutionDenial(true, session).Contains("NOT ARMED"), "Consumed confirmation cannot authorize a duplicate request even before session begins");
        session.RejectRequest("test early rejection");
        Check(session.Phase == InstallPhase.Rejected && !session.Busy, "Pre-probe denial is a visible result without native work");
        state.Stage(); state.NextCandidate(1);
        Check(!state.ArmedMatches && history.Last().Contains("candidate changed"), "Moving highlight disarms with reason");
        state.NextCandidate(-1); Check(!state.ArmedMatches, "Returning to the previous highlight never rearms implicitly");
        state.Stage(); state.NextSlot(1); Check(!state.ArmedMatches, "Changing target slot disarms");
        state.ReplaceSnapshot(Snapshot()); state.Stage(); state.NextGroup(1); Check(!state.ArmedMatches, "Changing group disarms");
        state.NextGroup(-1);
        foreach (string cause in new[] { "item missing", "slot missing", "occupied", "weapon", "player", "controller", "weapon reference", "item reference", "slot reference", "template", "location", "unexamined", "locked", "required", "scan incomplete", "children" })
        {
            state.ReplaceSnapshot(Snapshot()); state.Stage(); var fresh = Snapshot(); var target = fresh.Slots[0];
            switch (cause)
            {
                case "item missing": target.Candidates.RemoveAt(0); break;
                case "slot missing": fresh.Slots.RemoveAt(0); break;
                case "occupied": target.Slot.Installed = Item("occupied"); break;
                case "weapon": fresh.WeaponId = "other"; break;
                case "player": fresh.Player = new object(); break;
                case "controller": fresh.Controller = new object(); break;
                case "weapon reference": fresh.Weapon = new object(); break;
                case "item reference": target.Candidates[0].Item.NativeItem = new object(); break;
                case "slot reference": target.Slot.NativeSlot = new object(); break;
                case "template": target.Candidates[0].Item.TemplateId = "other"; break;
                case "location": target.Candidates[0].Item.Location = "TacticalRig"; break;
                case "unexamined": target.Candidates[0].Item.Examined = null; break;
                case "locked": target.Slot.Locked = true; break;
                case "required": target.Slot.Required = true; break;
                case "scan incomplete": fresh.Warnings.Add("incomplete"); break;
                case "children": target.Candidates[0].Item.HasChildren = true; break;
            }
            state.ReplaceSnapshot(fresh, true, "F7 fresh validation");
            Check(!state.ArmedMatches && state.StagedItemId == "" && history.Last().Contains("ARM_CLEARED"), "Fresh capture invalidates and explains: " + cause);
        }
        state.ReplaceSnapshot(Snapshot()); state.Stage();
        state.ReplaceSnapshot(new RaidSnapshot(), false, "F8 released");
        Check(!state.ArmedMatches && !session.Busy && state.StageReason.Contains("F8 released"), "Closing/releasing F8 clears arm without executing or resetting request session");
        state.ReplaceSnapshot(Snapshot()); Check(!state.ArmedMatches, "Reopening overlay never restores a consumed or cleared arm");
        var noIdentity = Snapshot(); noIdentity.Slots[0].Candidates[0].Item.Id = ""; state.ReplaceSnapshot(noIdentity);
        state.Stage(); Check(!state.ArmedMatches && history.Last().Contains("ARM_REJECTED"), "Enter rejects missing identity and journals the reason");
    }
}
