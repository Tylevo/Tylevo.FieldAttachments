using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Tylevo.FieldAttachments.Core;

namespace Tylevo.FieldAttachments.Runtime
{
    // Opt-in, single-player experiment. One existing leaf optic, muzzle device,
    // tactical device or foregrip moves between accessible carried storage and its native slot.
    // No Swap, direct Add/Remove, inventory-field writes, custom rollback,
    // force flags, patched restrictions, generated items, or custom networking.
    public sealed partial class NativeInstallProbe
    {
        internal const string InspectedGameMvid = "cc2d80b0-6d5b-4cb1-a581-6d2cc901d4c7";
        private readonly ReadAccess _read;
        private readonly NativeFilter _filter;
        private readonly LocalSessionReader _sessionReader;
        private RaidSnapshot? _requestContext;
        private bool _requestContextLost;
        private readonly string _root;
        private readonly List<string> _history = new List<string>();
        public readonly InstallSession Session = new InstallSession();
        public event Action<RaidSnapshot,object,object,bool>? BeforeNativeSubmit;
        public event Action? NativeSubmissionFinished;
        private Task? _task;
        private PropertyInfo? _taskResult, _resultSuccess;
        private object? _item, _slot, _sourceAddress, _sourceGrid, _destinationAddress;
        private bool _removing;
        private bool _hasChildren;
        private AttachmentAssembly? _assembly;
        private object? _assemblyController;
        private string ActionName => _removing ? "uninstall" : "install";
        private string _itemId = "", _templateId = "", _journal = "";
        private int _stack;
        private DateTime _submittedUtc;
        private bool _timeoutReported;
        private string _requestIdentity = "";
        private readonly string _sessionHeader;
        public string SessionJournalPath { get; }
        public string JournalError { get; private set; } = "";
        public string History => _sessionHeader + string.Join(Environment.NewLine, _history);
        public void RecordUi(string reason) { Record("UI " + reason); }

        public NativeInstallProbe(ReadAccess read, string root)
        {
            _read = read; _filter = new NativeFilter(read); _root = root; _sessionReader = new LocalSessionReader(read);
            string session = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6);
            SessionJournalPath = Path.Combine(root, "ProbeLogs", "session-" + session + ".txt");
            _sessionHeader = "FIELD ATTACHMENTS " + typeof(NativeInstallProbe).Assembly.GetName().Version +
                " SESSION " + session + "\nRolling last 512 events; UTC timestamps. Independent of F10 snapshot time.\n";
        }

        public LocalSessionFacts ReadContext(RaidSnapshot snapshot) => _sessionReader.Read(snapshot);
        public HandsReadiness ReadHands(RaidSnapshot snapshot)
        {
            object? hands = _read.Get(snapshot.Player, "HandsController");
            return new HandsReadiness {
                Operation = _read.Get(hands, "CurrentOperation")?.GetType().FullName ?? "UNRESOLVED",
                SameWeapon = snapshot.Weapon != null && ReferenceEquals(_read.Get(hands, "Item"), snapshot.Weapon),
                InventoryOpened = ReadAccess.Bool(_read.Get(hands, "InventoryOpened")),
                Aiming = ReadAccess.Bool(_read.Get(hands, "IsAiming")),
                TriggerPressed = ReadAccess.Bool(_read.Get(hands, "IsTriggerPressed")),
                InventoryLocked = ReadAccess.Bool(_read.Get(snapshot.Controller, "Locked")),
                HasActiveEvents = ReadAccess.Bool(_read.Get(snapshot.Controller, "HasActiveEvents"))
            };
        }

        public void TryInstall(RaidSnapshot snapshot, SlotViewModel? chosenSlot, CandidateObservation? chosenItem,
            bool enabled, bool stagedMatches, bool removing = false)
        {
            if (!Session.TryBegin()) { Record("REQUEST_REJECTED duplicate/latched; original request retained"); return; }
            _removing = removing;
            _requestIdentity = "action=" + ActionName + " weapon=" + snapshot.WeaponId + " item=" + (removing ? chosenSlot?.Slot.Installed?.Id : chosenItem?.Item.Id) + " slot=" + chosenSlot?.Slot.Path;
            if (!Record("PROBE_STARTED action=" + ActionName + " item=" + (removing ? chosenSlot?.Slot.Installed?.Id : chosenItem?.Item.Id) + " slot=" + chosenSlot?.Slot.Path))
            { Finish("Not submitted: event journal unavailable. " + JournalError, false); return; }
            bool simulationStarted = false;
            try
            {
                object? player = snapshot.Player, controller = snapshot.Controller;
                object? hands = _read.Get(player, "HandsController");
                ItemObservation? chosen = removing ? chosenSlot?.Slot.Installed : chosenItem?.Item;
                _item = chosen?.NativeItem; _slot = chosenSlot?.Slot.NativeSlot;
                HandsReadiness readiness = ReadHands(snapshot);
                Record("REQUEST_HANDS " + readiness.Evidence);
                LocalSessionFacts context = ReadContext(snapshot);
                Record("REQUEST_CONTEXT " + context.Evidence);
                Type? game = _read.FindType("EFT.Player");
                object? nativeChildren = _read.Get(_item, "Slots");
                bool childrenKnown = TryHasChildren(nativeChildren, out bool hasChildren);
                _hasChildren=hasChildren; _assemblyController=controller;
                _assembly=childrenKnown && hasChildren && _item!=null ? new AttachmentAssemblyReader(_read).Capture(_item,controller) : null;
                bool assemblyMatches=childrenKnown && chosen!=null && chosen.HasChildren==hasChildren &&
                    (!hasChildren || chosen.Assembly?.Same(_assembly)==true);
                var facts = new InstallFacts {
                    Enabled = enabled, Removing = removing,
                    InstalledItemMatches = _item != null && ReferenceEquals(_read.Get(_slot, "ContainedItem"), _item),
                    KnownBuild = game?.Module.ModuleVersionId.ToString() == InspectedGameMvid,
                    SinglePlayer = context.SupportedTypes && !context.FikaLoaded,
                    CurrentSelection = stagedMatches && _item != null && _slot != null &&
                        ReferenceEquals(_read.Get(hands, "Item"), snapshot.Weapon) &&
                        (removing ? ReferenceEquals(_read.Get(_slot, "ContainedItem"), _item) : snapshot.Carried.Any(i => ReferenceEquals(i.NativeItem, _item))) &&
                        snapshot.Slots.Any(s => ReferenceEquals(s.Slot.NativeSlot, _slot)),
                    SnapshotComplete = !snapshot.Truncated && snapshot.Warnings.Count == 0 &&
                        snapshot.ScanRoots.Count == CarriedStoragePolicy.Roots.Length && snapshot.ScanRoots.All(r => r.UnresolvedEntries == 0),
                    SourceIsCarried = chosenItem!=null && CarriedStoragePolicy.Allows(chosenItem.Item.Location),
                    SupportedLeafAttachment = AttachmentAssembly.Supports(chosenSlot?.Slot.Group,chosen) &&
                        ReadAccess.IsKind(_item, "EFT.InventoryLogic.Mod") && assemblyMatches,
                    SupportedContext = context.Allowed,
                    YourPlayer = ReadAccess.Bool(_read.Get(player, "IsYourPlayer")),
                    Alive = ReadAccess.Bool(_read.Get(_read.Get(player, "HealthController"), "IsAlive")),
                    InventoryOpened = readiness.InventoryOpened,
                    Aiming = readiness.Aiming,
                    TriggerPressed = readiness.TriggerPressed,
                    InventoryLocked = readiness.InventoryLocked,
                    HasActiveEvents = readiness.HasActiveEvents,
                    Idle = readiness.Idle,
                    Required = chosenSlot?.Slot.Required, Locked = chosenSlot?.Slot.Locked,
                    Deleted = ReadAccess.Bool(_read.Get(_slot, "Deleted")),
                    Empty = _slot == null ? (bool?)null : HasReadableNull(_slot, "ContainedItem"),
                    Examined = chosen?.Examined,
                    RaidModdable = ReadAccess.Bool(_read.Get(_item, "RaidModdable")),
                    FilterPass = _filter.Accepts(_slot, _item),
                    NeutralPinState = TryInt(_read.Get(_item, "PinLockState"), out int pin) && pin == 0
                };
                Record("GUARD " + string.Join(" ", typeof(InstallFacts).GetFields()
                    .Select(f => f.Name + "=" + (f.GetValue(facts)?.ToString() ?? "unknown"))));
                string denial = InstallGate.Denial(facts);
                if (denial.Length != 0) { Finish("Not submitted: " + denial + " | " + readiness.Display, false); return; }
                if (controller == null || _item == null || _slot == null) throw new InvalidOperationException("Live reference vanished.");

                // Resolve ALL essential signatures, including completion inspection,
                // before invoking the operation generator. No fuzzy type matching.
                Type itemType = NeedType("EFT.InventoryLogic.Item");
                Type addressType = NeedType("EFT.InventoryLogic.ItemAddress");
                Type controllerType = NeedType("EFT.InventoryLogic.ItemController");
                Type resultType = NeedType("Diz.LanguageExtensions.OperationResult");
                Type callbackType = NeedType("Comfort.Common.Callback");
                Type completionType = NeedType("Comfort.Common.IResult");
                Type moveResultType = NeedType("EFT.InventoryLogic.MoveResult");
                MethodInfo move = Exact(NeedType("EFT.InventoryLogic.ItemManipulator"), "Move", true,
                    new[] { itemType, addressType, controllerType, typeof(bool) });
                if (!move.ReturnType.IsGenericType || move.ReturnType.GetGenericTypeDefinition().FullName != "Diz.LanguageExtensions.OperationResult`1" ||
                    move.ReturnType.GetGenericArguments()[0] != moveResultType) throw new InvalidOperationException("Unexpected Move result type.");
                MethodInfo? createAddress = removing ? null : Exact(_slot.GetType(), "CreateItemAddress", false, Type.EmptyTypes);
                if (createAddress != null && createAddress.ReturnType != addressType) throw new InvalidOperationException("Unexpected slot address type.");
                MethodInfo[] conversions = move.ReturnType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.Name == "op_Implicit" && m.ReturnType == resultType && m.GetParameters().Length == 1 &&
                        m.GetParameters()[0].ParameterType == move.ReturnType).ToArray();
                if (conversions.Length != 1) throw new InvalidOperationException("Move-result conversion unresolved.");
                MethodInfo submit = Exact(controller.GetType(), "TryRunNetworkTransaction", false, new[] { resultType, callbackType });
                if (submit.ReturnType != typeof(Task<>).MakeGenericType(completionType)) throw new InvalidOperationException("Unexpected transaction task type.");
                _taskResult = submit.ReturnType.GetProperty("Result");
                _resultSuccess = completionType.GetProperty("Succeed");
                PropertyInfo? destroys = moveResultType.GetProperty("ItemsToDestroy", BindingFlags.Public | BindingFlags.Instance);
                if (_taskResult == null || _resultSuccess?.PropertyType != typeof(bool) || destroys == null)
                    throw new InvalidOperationException("Completion/destruction inspection unresolved; no operation invoked.");
                PropertyInfo? succeeded = move.ReturnType.GetProperty("Succeeded");
                FieldInfo? valueField = move.ReturnType.GetField("Value");
                if (succeeded?.PropertyType != typeof(bool) || valueField == null) throw new InvalidOperationException("Move result inspection unresolved.");

                _sourceAddress = _read.Get(_item, "Parent");
                _itemId = ReadAccess.Text(_read.Get(_item, "Id"));
                _templateId = ReadAccess.Text(_read.Get(_item, "TemplateId"));
                if (!TryInt(_read.Get(_item, "StackObjectsCount"), out _stack) || _stack != 1)
                    throw new InvalidOperationException("Probe requires one non-stacked attachment.");
                object? address;
                var storage = new CarriedStorageReader(_read).Capture(controller);
                if (!storage.Complete) throw new InvalidOperationException(string.Join("; ",storage.Warnings));
                StorageGrid[] grids=storage.Grids.ToArray();
                if (removing)
                {
                    // Installed observations alone do not prove examined status.
                    MethodInfo examined = Exact(controller.GetType(), "Examined", false, new[] { itemType });
                    if (examined.ReturnType != typeof(bool) || !Equals(examined.Invoke(controller,new[] {_item}),true))
                    { Finish("Uninstall rejected: examination could not be verified.",false); return; }
                    if (!ReadAccess.IsKind(_sourceAddress,"EFT.InventoryLogic.SlotItemAddress") ||
                        !ReferenceEquals(_read.Get(_sourceAddress,"Container"),_slot))
                        throw new InvalidOperationException("Exact installed source slot could not be verified.");
                    address = FindStorageAddress(grids, itemType);
                    if (address == null) { Finish("Uninstall rejected: no compatible free carried space, including secure storage. Attachment stays installed.",false); return; }
                }
                else
                {
                    _sourceGrid = _read.Get(_sourceAddress, "Container");
                    if (!ReadAccess.IsKind(_sourceAddress, "EFT.InventoryLogic.GridItemAddress") ||
                        !grids.Any(g=>ReferenceEquals(g.NativeGrid,_sourceGrid) && g.Location==chosen!.Location) ||
                        !ReferenceEquals(chosen!.SourceGrid,_sourceGrid) || !Equals(chosen.SourceAddress,_sourceAddress) || SourceContainsItem()!=true)
                        throw new InvalidOperationException("Exact accessible carried source grid/address could not be verified.");
                    _storageLocation=chosen.Location;
                    address = createAddress!.Invoke(_slot, null);
                    if (address == null || !ReferenceEquals(_read.Get(address, "Container"), _slot))
                        throw new InvalidOperationException("Target address did not resolve to the selected slot.");
                }
                _destinationAddress = address;

                string dir = Path.Combine(_root, "ProbeLogs"); Directory.CreateDirectory(dir);
                _journal = Path.Combine(dir, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".txt");
                File.WriteAllText(_journal, "FIELD ATTACHMENTS 0.25.2 LIVE " + ActionName.ToUpperInvariant() + " PROBE\n", new UTF8Encoding(false));
                Record("BEFORE item=" + _itemId + " template=" + _templateId + " stack=" + _stack +
                    " action=" + ActionName + " slot=" + chosenSlot!.Slot.Path + " storage=" + _storageLocation + " grid=" + _read.Get(_sourceGrid,"ID") + " " + readiness.Evidence);
                Record("BOUND " + move.DeclaringType?.FullName + "." + move + " -> " + submit.DeclaringType?.FullName + "." + submit);
                if (!OriginalLayoutIntact()) throw new InvalidOperationException("Source/slot changed before simulation.");
                if (!ReadContext(snapshot).Allowed) throw new InvalidOperationException("Local raid/range context changed before simulation.");

                // One on-demand native simulation, as in WeaponModdingManipulation.Select.
                // This temporarily mutates native structures internally. The engine's
                // simulation performs its own rollback. NEVER RollBack it a second time.
                simulationStarted = true;
                object? simulated = move.Invoke(null, new[] { _item, address, controller, (object)true });
                bool intact = OriginalLayoutIntact();
                Record("AFTER_SIMULATION originalLayoutIntact=" + intact);
                if (!intact) { Finish("Simulation did not restore the observed layout. Live install blocked; export report and restart the test game.", true); return; }
                if (simulated == null) throw new InvalidOperationException("No simulation result returned.");
                if (!(succeeded.GetValue(simulated, null) is bool success)) throw new InvalidOperationException("Unknown simulation outcome.");
                if (!success) { Finish("Native validation rejected " + ActionName + ": " + ReadAccess.Text(_read.Get(simulated, "Error")), false); return; }
                object? value = valueField.GetValue(simulated);
                if (value == null) throw new InvalidOperationException("Successful simulation returned no value.");
                object? destroyList = destroys.GetValue(value, null);
                if (destroyList != null && (!(destroyList is IEnumerable sequence) || HasAny(sequence)))
                { Finish("Install rejected: unexpected destruction list. No transaction submitted.", false); return; }
                object? converted = conversions[0].Invoke(null, new[] { simulated });
                if (converted == null) throw new InvalidOperationException("Move result conversion returned null.");

                // No yield occurs between fresh validation and submission. Normal
                // game validation/CanExecute still runs inside this transaction.
                if (!ReadContext(snapshot).Allowed) throw new InvalidOperationException("Local raid/range context changed before submission.");
                _requestContext = context.Hideout ? snapshot : null; _requestContextLost = false;
                _submittedUtc = DateTime.UtcNow; _timeoutReported = false;
                Session.MarkSubmitted(); Record("SUBMITTED " + Session.Status);
                BeforeNativeSubmit?.Invoke(snapshot,_item!,_slot!,_removing);
                object? submittedTask = submit.Invoke(controller, new[] { converted, (object?)null });
                _task = submittedTask as Task;
                if (_task == null) throw new InvalidOperationException("Transaction did not return the expected task; completion unknown.");
            }
            catch (Exception e)
            {
                Exception cause = e.GetBaseException();
                Finish(ActionName + " probe stopped: " + cause.GetType().Name + ": " + cause.Message +
                    (simulationStarted ? " Do not retry; export report and restart the test game." : " No native operation invoked."), simulationStarted);
            }
            finally
            {
                if (!Session.Busy) ClearLiveReferences();
            }
        }

        // Called from Plugin.Update even when the overlay is closed, unfocused or
        // disabled by configuration (not component unloading). No Wait/Result access until the task is completed. No Task.Run.
        public void Poll()
        {
            Task? task = _task;
            if (task == null) return;
            if (_requestContext != null && !_requestContextLost && !ReadContext(_requestContext).Allowed)
            {
                _requestContextLost = true;
                Finish("Shooting-range context changed during a native request; outcome unknown. No retry or profile synchronization.", true);
            }
            if (!task.IsCompleted)
            {
                if (!_timeoutReported && (DateTime.UtcNow - _submittedUtc).TotalSeconds >= 20)
                { _timeoutReported = true; Session.TimedOut(); Record(Session.Status); }
                return;
            }
            try
            {
                if (_requestContextLost) { Record("COMPLETION range context was lost; uncertainty latch retained"); return; }
                if (task.IsFaulted || task.IsCanceled)
                { Finish("Native task failed/cancelled; outcome uncertain. " + task.Exception?.GetBaseException().Message + " Export report; no retry until restart.", true); return; }
                object? result = _taskResult?.GetValue(task, null);
                object? successValue = result == null ? null : _resultSuccess?.GetValue(result, null);
                if (!(successValue is bool success)) { Finish("Native completion result could not be read; outcome uncertain. Export report and restart.", true); return; }
                bool installed = InstalledLayoutIntact();
                bool unchanged = OriginalLayoutIntact();
                Record("COMPLETION succeed=" + success + " installedLayoutIntact=" + installed + " originalLayoutIntact=" + unchanged +
                    " result=" + result?.GetType().FullName + (success ? "" : " error=" + ReadAccess.Text(_read.Get(result, "Error"))));
                if (success && installed)
                    Finish("Native " + ActionName + " succeeded; same item observed in " + (_removing ? _storageLocation+"; source slot is empty." : "the target slot.") + " Export F10.", false, true);
                else if (!success && unchanged)
                    Finish("Native " + ActionName + " rejected; observed item remains in its original " + (_removing ? "slot. " : "carried location. ") + ReadAccess.Text(_read.Get(result, "Error")), false);
                else Finish("Completion and observed layout disagree. Stop testing, export report, and restart the game. No automatic retry or rollback.", true);
            }
            catch (Exception e) { Finish("Completion inspection failed: " + e.GetBaseException().Message + ". Export report and restart.", true); }
            finally { _task = null; ClearLiveReferences(); }
        }

        private bool IdentityIntact()
        {
            return _item != null && ReadAccess.Text(_read.Get(_item, "Id")) == _itemId &&
                ReadAccess.Text(_read.Get(_item, "TemplateId")) == _templateId &&
                TryInt(_read.Get(_item, "StackObjectsCount"), out int n) && n == _stack &&
                TryHasChildren(_read.Get(_item,"Slots"),out bool children) && children==_hasChildren &&
                (!children || _assembly?.Same(new AttachmentAssemblyReader(_read).Capture(_item,_assemblyController))==true);
        }
        private bool OriginalLayoutIntact()
        {
            if (!IdentityIntact() || _slot==null) return false;
            if (_removing) return ReferenceEquals(_read.Get(_slot,"ContainedItem"),_item) &&
                ReferenceEquals(_read.Get(_read.Get(_item,"Parent"),"Container"),_slot) && SourceContainsItem()==false;
            return HasReadableNull(_slot,"ContainedItem")==true &&
                Equals(_read.Get(_item,"Parent"),_sourceAddress) && SourceContainsItem()==true;
        }
        private bool InstalledLayoutIntact()
        {
            if (!IdentityIntact() || _slot==null) return false;
            if (_removing) return HasReadableNull(_slot,"ContainedItem")==true &&
                Equals(_read.Get(_item,"Parent"),_destinationAddress) && SourceContainsItem()==true;
            return ReferenceEquals(_read.Get(_slot,"ContainedItem"),_item) &&
                ReferenceEquals(_read.Get(_read.Get(_item,"Parent"),"Container"),_slot) && SourceContainsItem()==false;
        }
        private bool? SourceContainsItem()
        {
            object? contents = _read.Get(_sourceGrid, "Items");
            if (!(contents is IEnumerable sequence) || contents is string) return null;
            IEnumerator? iterator = null;
            try
            {
                iterator = sequence.GetEnumerator();
                int n = 0;
                while (iterator.MoveNext())
                {
                    if (++n > 512) return null;
                    object? entry = iterator.Current;
                    if (ReferenceEquals(entry, _item)) return true;
                    if (entry != null && !ReadAccess.IsKind(entry, "EFT.InventoryLogic.Item") &&
                        (ReferenceEquals(_read.Get(entry, "Key"), _item) || ReferenceEquals(_read.Get(entry, "Value"), _item))) return true;
                }
                return false;
            }
            catch { return null; }
            finally { (iterator as IDisposable)?.Dispose(); }
        }
        private bool TryHasChildren(object? value, out bool hasChildren)
        {
            hasChildren = false;
            if (!(value is IEnumerable slots) || value is string) return false;
            try
            {
                int n = 0;
                foreach (object slot in slots)
                {
                    if (++n > 128 || HasReadableNull(slot, "ContainedItem") == null) return false;
                    if (HasReadableNull(slot, "ContainedItem") == false) { hasChildren = true; return true; }
                }
                return true;
            }
            catch { return false; }
        }
        private static bool? HasReadableNull(object target, string name)
        {
            // ReadAccess.Get deliberately conflates null and lookup failure. An
            // EMPTY slot gate must distinguish those cases before a live action.
            try
            {
                for (Type? t = target.GetType(); t != null; t = t.BaseType)
                {
                    PropertyInfo? p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                    if (p != null && p.GetIndexParameters().Length == 0) return p.GetValue(target, null) == null;
                }
            }
            catch { }
            return null;
        }
        private static bool HasAny(IEnumerable items)
        {
            IEnumerator e = items.GetEnumerator();
            try { return e.MoveNext(); } finally { (e as IDisposable)?.Dispose(); }
        }
        private static bool TryInt(object? value, out int number)
        { try { if (value == null) { number = 0; return false; } number = Convert.ToInt32(value); return true; } catch { number = 0; return false; } }
        private Type NeedType(string name) { return _read.FindType(name) ?? throw new InvalidOperationException("Missing type: " + name); }
        private static MethodInfo Exact(Type type, string name, bool isStatic, Type[] arguments)
        {
            for (Type? t = type; t != null; t = t.BaseType)
            {
                MethodInfo[] matches = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Where(m => m.Name == name && m.IsStatic == isStatic && !m.ContainsGenericParameters &&
                        m.GetParameters().Select(p => p.ParameterType).SequenceEqual(arguments)).ToArray();
                if (matches.Length == 1) return matches[0];
                if (matches.Length > 1) throw new InvalidOperationException("Ambiguous method: " + name);
            }
            throw new InvalidOperationException("Exact method unavailable: " + type.FullName + "." + name);
        }
        private void Finish(string message, bool uncertain, bool succeeded = false)
        { NativeSubmissionFinished?.Invoke(); Session.Finish(message, uncertain, succeeded); Record(Session.Phase.ToString().ToUpperInvariant() + " " + message); }
        private bool Record(string line)
        {
            if (!line.StartsWith("UI ", StringComparison.Ordinal) && _requestIdentity.Length != 0) line += " | " + _requestIdentity;
            if (line.Length > 2048) line = line.Substring(0, 2048) + " [truncated]";
            string entry = DateTime.UtcNow.ToString("O") + " | " + line;
            if (_history.Count >= 512) _history.RemoveAt(0);
            _history.Add(entry);
            bool persisted = false;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SessionJournalPath)!);
                string temp = SessionJournalPath + ".tmp";
                File.WriteAllText(temp, History + Environment.NewLine, new UTF8Encoding(false));
                if (File.Exists(SessionJournalPath)) File.Replace(temp, SessionJournalPath, null);
                else File.Move(temp, SessionJournalPath);
                JournalError = ""; persisted = true;
            }
            catch (Exception e)
            {
                JournalError = e.GetBaseException().GetType().Name + ": " + e.GetBaseException().Message;
                _read.Note("Session journal unavailable; in-memory F10 history retained: " + JournalError);
            }
            if (!string.IsNullOrEmpty(_journal))
            {
                try { File.AppendAllText(_journal, entry + Environment.NewLine, new UTF8Encoding(false)); }
                catch (Exception e) { _read.Note("Probe journal write failed; session/F10 history retained: " + e.GetBaseException().Message); }
            }
            return persisted;
        }
        private void ClearLiveReferences()
        { _item = _slot = _sourceAddress = _sourceGrid = _destinationAddress = _assemblyController = null;
          _assembly=null; _hasChildren=false; _requestContext = null; _taskResult = _resultSuccess = null; _journal = ""; }
    }
}
