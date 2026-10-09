using System;
using System.IO;
using System.Threading;
using BepInEx;
using BepInEx.Configuration;
using Cysharp.Threading.Tasks;
using Tylevo.FieldAttachments.Core;
using Tylevo.FieldAttachments.Runtime;
using UnityEngine;

namespace Tylevo.FieldAttachments
{
    [BepInPlugin("com.tylevo.fieldattachments", "Tylevo Field Attachments", "1.0.0")]
    [BepInProcess("EscapeFromTarkov.exe")]
    [BepInDependency("com.arys.unitytoolkit", "2.0.2")]
    [DefaultExecutionOrder(-10000)]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        private ConfigEntry<bool> _enabled = null!, _toggle = null!, _icons = null!, _hints = null!, _unknown = null!, _il = null!, _liveInstall = null!;
        private ConfigEntry<KeyboardShortcut> _open = null!, _refresh = null!, _report = null!, _installKey = null!, _armKey = null!, _poseKey = null!, _markPoseKey = null!;
        private readonly PoseActivation _poseActivation = new PoseActivation();
        private ConfigEntry<bool> _poseToggle = null!;
        private ConfigEntry<bool> _shortPoseReturn = null!, _mdrMotion = null!, _mcxLateInspection = null!, _globalMcx = null!;
        private ConfigEntry<bool> _stmPresentation = null!;
        private ConfigEntry<float> _stmTilt = null!;
        private ConfigEntry<bool> _followingCards = null!, _cameraFacingCards = null!, _hexSelectors = null!, _crossSelectors = null!, _studioCross = null!;
        private ConfigEntry<float> _cardPerspective = null!, _cardClockwise = null!;
        private ConfigEntry<KeyCode> _mouseModifier = null!;
        private ConfigEntry<KeyboardShortcut> _cyclePosition = null!;
        private bool NativeReloadPressed => Input.GetKeyDown(KeyCode.R) && _pointer?.Capture.Active!=true;
        private ConfigEntry<float> _mouseSensitivity = null!;
        private AttachmentPointerInput? _pointer;
        private object? _pointerPlayer, _pointerWeapon, _pointerHands;
        private bool _pointerWasActive;
        private readonly ReadAccess _read = new ReadAccess();
        private readonly SelectionState _state = new SelectionState();
        private RaidReader _reader = null!;
        private NativeResources _resources = null!;
        private ReportWriter _reports = null!;
        private NativeInstallProbe _probe = null!;
        private InspectionPose _pose = null!;
        private AttachmentAnimationSpeed _attachmentSpeed = null!;
        private ConfigEntry<float> _attachmentSpeedMultiplier = null!;
        private int _probeRevision;
        private AttachmentOverlay? _overlay;
        private CancellationTokenSource? _visualLoop;
        private bool _visible, _skip, _dismissedUntilRelease;
        private bool _poseOwnsOverlay;
        private string _notice = "";
        private float _nextPoll, _nextIconPoll, _nextStatusPoll;

        private void Awake()
        {
            _enabled = BindSetting("General", "Enabled", true, "Enable the attachment menu.");
            BindAltMenu();
            _mouseModifier = BindSetting("Controls", "MouseModifier", KeyCode.LeftAlt, "Hold while attachment mode is open for the TSC-style virtual cursor. Blocks camera look and shooting; release returns control. Click an attachment to install, or Uninstall to move it to a compatible free carried space. Click a different carried attachment on an occupied mount to remove the current part then install your selection. Click confirms; no arming step.");
            _attachmentSpeedMultiplier = BindSetting("Controls", "AttachmentAnimationSpeedMultiplier", AttachmentSpeedLease.DefaultMultiplier,
                new ConfigDescription("Playback speed for this mod's native install/remove operation. 1.12 is 12% faster; 1 keeps native speed. Normal inspect, firing and reload are unchanged.", new AcceptableValueRange<float>(1f,1.15f)));
            _mouseSensitivity = BindSetting("Controls", "MouseSensitivity", 20f, new ConfigDescription("Virtual cursor speed while the modifier is held.", new AcceptableValueRange<float>(1,80)));
            _open = BindSetting("Controls", "OpenOverlay", new KeyboardShortcut(KeyCode.F8), "Hold to show the overlay. The separate mouse modifier captures look/clicks while held.");
            _toggle = BindSetting("Controls", "ToggleInsteadOfHold", false, "Use the open key as a toggle instead of hold.");
            _poseToggle = BindSetting("Controls", "ToggleAttachmentPose", true, "Applies only if you assign a legacy attachment key. ON toggles its presentation; OFF requires holding that key. Closing cancels a click waiting for idle, never a submitted native transaction.");
            _poseKey = BindSetting("Controls", "AttachmentPose", new KeyboardShortcut(KeyCode.None), "Optional legacy key for opening the weapon presentation separately from the cursor. Unbound by default. Use the Left Alt menu shortcut for combined presentation and cursor control.");
            _markPoseKey = BindSetting("Controls", "MarkPose", new KeyboardShortcut(KeyCode.F4), "Legacy calibration key retained for configuration compatibility. F4 pose marking and saved native inspection markers are inactive in standalone attachment mode.");
            _shortPoseReturn = BindSetting("Experiments", "ShortPoseReturn", false, "Legacy native inspection setting retained for configuration compatibility; inactive in standalone attachment mode. The authored return uses its own clock and fades its final 0.15 seconds.");
            _mcxLateInspection = BindSetting("Experiments", "McxLateInspection", false, "Legacy native MCX inspection setting retained for configuration compatibility; inactive in standalone attachment mode.");
            _globalMcx = BindSetting("Experiments", "GlobalMcxInspection", false, "Legacy native donor inspection setting retained for configuration compatibility; inactive in standalone attachment mode. Unsupported authored recipients are refused without a native inspection fallback.");
            _mdrMotion = BindSetting("Experiments", "MdrMotionTrial", false, "Legacy native MDR clip substitution setting retained for configuration compatibility; inactive in standalone attachment mode.");
            _stmPresentation = BindSetting("Experiments", "StmPresentationTrial", false, "Legacy native STM inspection tilt setting retained for configuration compatibility; inactive in standalone attachment mode.");
            _stmTilt = BindSetting("Experiments", "StmPresentationTiltDegrees", StmPresentation.DefaultDegrees, new ConfigDescription(
                "Legacy native STM tilt value retained for configuration compatibility; inactive in standalone attachment mode.", new AcceptableValueRange<float>(-StmPresentation.MaxDegrees, StmPresentation.MaxDegrees)));
            _refresh = BindSetting("Controls", "Rescan", new KeyboardShortcut(KeyCode.F9), "Rescan while the overlay is open.");
            _armKey = BindSetting("Controls", "ArmSelection", new KeyboardShortcut(KeyCode.F6), "Additional staging-only shortcut. Return and KeypadEnter also arm. No inventory action; F7 remains separate. Uses the same inclusive F8 chord handling.");
            _report = BindSetting("Controls", "ExportReport", new KeyboardShortcut(KeyCode.F10), "Export snapshot, scan reasons, install-probe history and focused method metadata. F10 never initiates an inventory operation; while one is pending it exports the last snapshot.");
            _liveInstall = BindSetting("Experiments", "EnableEmptySlotInstall", true, "Allow installing, replacing and removing compatible carried attachments. Enabled by default. Disable to browse without changing attachments. Removed parts need compatible free space in your gear. Attachment changes are not supported with Fika.");
            _installKey = BindSetting("Controls", "AttemptEmptySlotInstall", new KeyboardShortcut(KeyCode.F7), "LIVE ACTION when enabled: after staging with Enter, press this while the overlay is open. Empty compatible slot + loose carried optic, muzzle device, tactical device or foregrip only.");
            _cyclePosition = BindSetting("Controls", "CycleAttachmentPosition", new KeyboardShortcut(KeyCode.R,KeyCode.LeftAlt), "While the attachment cursor is active, cycle the hovered card's mounting point, or the open LIST's point. Updates its native leader. Does not reload or move inventory.");
            _icons = BindSetting("Display", "RequestNativeIcons", true, "Request existing game thumbnails through a structurally matched LoadItemIcon adapter. Missing icons fall back to labels.");
            _hexSelectors = BindSetting("Experiments", "HexAttachmentSelectors", false, "Experimental compact hex attachment clusters with native icons and weapon perspective. Same Alt click actions, slot chooser and safeguards. OFF keeps the current cards; no weapon animation or inventory behavior change.");
            _crossSelectors = BindSetting("Experiments", "CrossAttachmentSelectors", true, "Experimental weapon-following cross: optic above, muzzle left, tactical right, foregrip below. Uses native Tarkov neutral interaction colors and real item icons. Takes precedence over HexAttachmentSelectors while ON; OFF restores your previous layout. Same Alt actions and native safeguards.");
            _studioCross = BindSetting("Display", "StudioCrossLayout", true, "For CrossAttachmentSelectors: use the exported MCX gentle layout (19 Sep 14:00 UTC), including its positions, 17/-39/30 degree axes and size. Installed card + REMOVE/LIST; available items stay in LIST. Native slot lines and bounded weapon sway remain live. Overrides CameraFacingCards and card angle trims for cross only. OFF restores the previous cross.");
            _followingCards = BindSetting("Display", "WeaponFollowingCards", true, "Place attachment panels around native weapon anchors with bounded weapon-relative rotation and perspective. Header/status stay fixed. OFF restores the flat layout; no effect on inspection or inventory actions.");
            _cameraFacingCards = BindSetting("Display", "CameraFacingCards", true, "Keep cards, hexes and cross tiles straight and facing you while their positions follow the live gun with WeaponFollowingCards ON. OFF restores the previous barrel-aligned perspective. No weapon/animation changes.");
            _cardPerspective = BindSetting("Display", "CardPerspectiveStrength", .55f, new ConfigDescription(
                "Used when CameraFacingCards is OFF. Card surface perspective: 0 faces the viewer with live weapon tilt; 1 uses native perspective, with surface pitch/yaw limited for readability. Lower values make cards wider and less skewed. Does not rotate the gun.", new AcceptableValueRange<float>(0, 1)));
            _cardClockwise = BindSetting("Display", "CardClockwiseDegrees", 5f, new ConfigDescription(
                "Used when CameraFacingCards is OFF. Small clockwise adjustment of every card surface, in degrees. Positive turns card edges to the right. Weapon motion, slot anchors and inspection are unchanged.", new AcceptableValueRange<float>(-15, 15)));
            _hints = BindSetting("Discovery", "ShowSameClassHints", true, "Legacy setting retained for config compatibility. The quick-swap picker now shows only native-fitting raid-moddable attachments.");
            _unknown = BindSetting("Discovery", "ShowAllUnknownMods", false, "Legacy setting retained for config compatibility. Unknown or incompatible items stay hidden.");
            _il = BindSetting("Diagnostics", "ExportLimitedIL", true, "Export capped IL for selected inventory methods. Reads metadata only, does not invoke those methods. Review before sharing.");
            _resources = new NativeResources(_read); _reader = new RaidReader(_read, _resources);
            string root = Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? BepInEx.Paths.PluginPath;
            _reports = new ReportWriter(_read, root);
            _probe = new NativeInstallProbe(_read, root);
            _attachmentSpeed = new AttachmentAnimationSpeed(_read,Trace);
            _probe.BeforeNativeSubmit += (snapshot,item,slot,removing) => { _attachmentSpeed.Multiplier=_attachmentSpeedMultiplier.Value; _attachmentSpeed.Prepare(snapshot,item,slot,removing); };
            _probe.NativeSubmissionFinished += _attachmentSpeed.End;
            _pose = new InspectionPose(_read, _reader, _probe, root);
            _state.Transition += message => Trace((message.StartsWith("ARM_CLEARED", StringComparison.Ordinal) ? "INVALIDATE " : "STAGE ") + message);
            _skip = Application.isBatchMode;
            Logger.LogInfo("Field Attachments 1.0.0 loaded. Targets SPT 4.1.5/4.1.6 with the inspected native client. Attachment changes=" + _liveInstall.Value + "; menu=" + _menuKey.Value + "/" + _menuActivationMode.Value + "; legacy attachment mode=" + _poseKey.Value + "; legacy cursor=" + _mouseModifier.Value + ".");
            if (_skip) Logger.LogInfo("Batch/headless graphics session: UI disabled.");
        }
        private void Update()
        {
            if (_skip) return;
            try
            {
                MaintainAltInput();
                _pointer?.Refresh();
                bool menuHeld = AltCaptureRequested(_pointer?.Registered == true && PointerLocalOwnership());
                bool held = Held(_open.Value);
                bool posePhysical = Held(_poseKey.Value), poseDown = Down(_poseKey.Value);
                bool legacyPoseHeld = _legacyPoseActivation.Sample(posePhysical,poseDown,_poseToggle.Value);
                bool poseHeld = _poseActivation.Sample(legacyPoseHeld || menuHeld,false,false);
                bool menuClose = _altPoseRequested && (!menuHeld || poseDown);
                bool menuStart = menuHeld && !_altPoseRequested;
                bool returnDown = Input.GetKeyDown(KeyCode.Return), keypadDown = Input.GetKeyDown(KeyCode.KeypadEnter);
                bool shortcutArm = Down(_armKey.Value);
                bool armPressed = returnDown || keypadDown || shortcutArm;
                string armInput = returnDown ? "Return" : keypadDown ? "KeypadEnter" : _armKey.Value.ToString();
                // Record input before focus/close/refresh guards; only while this UI is active or its chord is held.
                if (_visible || held)
                {
                    if (returnDown) Trace("KEY Return");
                    if (keypadDown) Trace("KEY KeypadEnter");
                    if (shortcutArm && _armKey.Value.MainKey != KeyCode.Return && _armKey.Value.MainKey != KeyCode.KeypadEnter)
                        Trace("KEY " + _armKey.Value + " (ArmSelection)");
                    if (Down(_installKey.Value)) Trace("KEY " + _installKey.Value);
                    if (Down(_refresh.Value)) Trace("KEY " + _refresh.Value);
                    if (Down(_report.Value)) Trace("KEY " + _report.Value);
                }
                // Completion tracking is independent of whether the UI is open.
                _attachmentSpeed.Poll(_enabled.Value);
                _probe.Poll();
                if (menuClose)
                { Close(AltCloseReason()); _dismissedUntilRelease = true; return; }
                if (_pointer != null)
                {
                    _pointer.Modifier = _mouseModifier.Value; _pointer.Refresh();
                }
                bool pointerActive = _pointer?.Capture.Active == true;
                bool pointerMouse = _pointer?.Capture.SuppressMouse == true;
                bool interruptPose = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab) ||
                    Input.GetKeyDown(KeyCode.F12) ||
                    (!pointerMouse && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))) ||
                    (!pointerActive && (Input.GetKeyDown(KeyCode.R) || Input.mouseScrollDelta.sqrMagnitude > 0 || WeaponNumberPressed()));
                _pose.ShortReturnEnabled = _shortPoseReturn.Value;
                _pose.MdrMotionEnabled = _mdrMotion.Value;
                _pose.McxLateInspectionEnabled = _mcxLateInspection.Value;
                _pose.GlobalMcxInspectionEnabled = _globalMcx.Value;
                _pose.StmPresentationEnabled = _stmPresentation.Value;
                _pose.CustomStmAnimationEnabled = true;
                _pose.StmPresentationDegrees = _stmTilt.Value;
                _pose.Tick(poseHeld, _enabled.Value, interruptPose);
                if (_poseOwnsOverlay && _pose.Interrupted)
                { Close("standalone pose interrupted"); _dismissedUntilRelease = true; return; }
                if (!_visible && !_pose.PresentationReturning) StopVisualLoop();
                if (_probeRevision != _probe.Session.Revision)
                {
                    _probeRevision = _probe.Session.Revision;
                    string resultNotice = _probe.Session.Status;
                    if (_clickSubmitted)
                    {
                        _clickStatus=_probe.Session.Busy ? _clickAction.ToString().ToUpperInvariant()+" PENDING" :
                            _probe.Session.Phase==InstallPhase.Succeeded ? (_clickAction==AttachmentAction.Uninstall ? "UNINSTALLED TO INVENTORY" : "INSTALLED") :
                            _probe.Session.Status;
                        if (!_probe.Session.Busy) _clickSubmitted=false;
                    }
                    if (_visible && !_probe.Session.Busy) Capture("request result refresh");
                    _notice = resultNotice; Logger.LogInfo(resultNotice); Render();
                }
                if (_poseToggle.Value && poseDown && !legacyPoseHeld && _visible)
                { Close("F5 toggled off"); _dismissedUntilRelease=true; return; }
                if (!_enabled.Value || !Application.isFocused) { Close("plugin disabled or focus lost"); return; }
                // Works even when the visual overlay cannot be constructed.
                if (!_visible && held && Down(_report.Value)) { Capture(); Export(); Close(); _dismissedUntilRelease = true; return; }
                if (!held && !posePhysical && (MenuKeyCode == KeyCode.None || !Input.GetKey(MenuKeyCode))) _dismissedUntilRelease = false;
                if (!poseHeld && _poseOwnsOverlay)
                { _poseOwnsOverlay = false; if (!held) Close(_poseToggle.Value ? "F5 toggled off" : "F5 released"); }
                if ((poseDown || menuStart) && poseHeld && !_dismissedUntilRelease)
                {
                    if (menuStart) _altPoseRequested = true;
                    if (!_visible) Open();
                    _poseOwnsOverlay = true;
                    _pose.Start(_state.Snapshot, Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
                    if (_pose.Interrupted)
                    { Close("standalone pose unavailable"); _dismissedUntilRelease = true; return; }
                    _notice = _pose.Status; Render();
                }
                if (poseHeld) { if (!_visible && !_dismissedUntilRelease) Open(); }
                else if (_toggle.Value)
                {
                    if (Down(_open.Value)) { if (_visible) Close(); else Open(); }
                }
                else if (held && !_dismissedUntilRelease && !_visible) Open();
                else if (!held && _visible) Close("F8 released");
                if (!_visible) return;
                if (_state.Snapshot.Player != null && (ReadAccess.Bool(_read.Get(_state.Snapshot.Player,"IsInventoryOpened")) == true ||
                    Cursor.visible || Cursor.lockState != CursorLockMode.Locked))
                { Close("native cursor/menu owns input"); _dismissedUntilRelease = true; return; }
                if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.F12))
                { Close("native menu opened"); _dismissedUntilRelease = true; return; }
                if (Input.GetKeyDown(KeyCode.Escape)) { Close("Escape"); _dismissedUntilRelease = true; return; }
                if (_state.Snapshot.Player != null && !_reader.Alive(_state.Snapshot.Player))
                { Close("player no longer alive"); _dismissedUntilRelease = true; return; } // Does not cancel a native task already submitted.
                if (_state.Snapshot.PlayerType == "EFT.HideoutPlayer" && !_probe.ReadContext(_state.Snapshot).Allowed)
                { Close("hideout range exited or inventory changed"); _dismissedUntilRelease = true; return; }
                PollReplacement(interruptPose);
                PollPoseResume(interruptPose);
                PollClickRequest();
                if (_clicks.Busy || _replacement.Pending)
                {
                    if (Down(_report.Value)) Export();
                    if (Down(_installKey.Value) || armPressed || Input.GetMouseButtonUp(0)) Trace("CLICK_DUPLICATE input ignored while waiting");
                    return;
                }
                if (Down(_markPoseKey.Value)) { _pose.Mark(); _notice = _pose.Status; Render(); }
                if (Down(_refresh.Value) && !_probe.Session.Busy)
                {
                    Capture("F9 rescan");
                    _notice = "Rescan complete. " + _state.StageReason;
                    Logger.LogInfo(_notice); Render();
                }
                if (Down(_report.Value)) Export();
                if (Time.unscaledTime >= _nextStatusPoll)
                { _nextStatusPoll = Time.unscaledTime + 0.5f; UpdateActionStatus(); } // Observation only; no log or deferred action.
                if (_overlay!=null) _overlay.LiveActionsEnabled=_liveInstall.Value;
                UpdateCrossPresentation();
                bool changed = false;
                if (_pointer != null)
                {
                    _pointer.Refresh();
                    bool active = _pointer.Capture.Active;
                    if (_pointerWasActive != active)
                    { _pointerWasActive = active; Trace(active ? "POINTER_CAPTURE Alt" : "POINTER_RELEASE Alt/context"); }
                    if (_overlay?.HandlePointer(active && !_probe.Session.Busy, Input.GetAxisRaw("Mouse X") * _mouseSensitivity.Value,
                        Input.GetAxisRaw("Mouse Y") * _mouseSensitivity.Value, Input.GetMouseButtonDown(0), Input.GetMouseButtonUp(0),
                        Input.GetMouseButtonDown(1), Input.mouseScrollDelta.y) == true)
                    { Trace("POINTER_CLICK handled"); changed = true; }
                    if(active && !_probe.Session.Busy && Down(_cyclePosition.Value) && _overlay?.CyclePointerPosition()==true)
                    { Trace("POINTER_POSITION "+_state.Identity); changed=true; }
                }
                if (_clicks.Busy) { Render(); return; }
                if (_probe.Session.Busy)
                {
                    if (armPressed) { Trace("STAGE ARM_REJECTED " + armInput + ": original request pending"); }
                    if (Down(_installKey.Value)) { AttemptInstall(armPressed); Render(); }
                    return;
                }
                if (Input.GetKeyDown(KeyCode.LeftArrow)) { Trace("KEY LeftArrow"); _state.NextGroup(-1); _notice = FocusNotice(); changed = true; }
                if (Input.GetKeyDown(KeyCode.RightArrow)) { Trace("KEY RightArrow"); _state.NextGroup(1); _notice = FocusNotice(); changed = true; }
                if (Input.GetKeyDown(KeyCode.UpArrow)) { Trace("KEY UpArrow"); _state.NextCandidate(-1); _notice = CandidateNotice(); changed = true; }
                if (Input.GetKeyDown(KeyCode.DownArrow)) { Trace("KEY DownArrow"); _state.NextCandidate(1); _notice = CandidateNotice(); changed = true; }
                if (Input.GetKeyDown(KeyCode.PageUp)) { Trace("KEY PageUp"); _state.NextSlot(-1); _notice = FocusNotice(); changed = true; }
                if (Input.GetKeyDown(KeyCode.PageDown)) { Trace("KEY PageDown"); _state.NextSlot(1); _notice = FocusNotice(); changed = true; }
                if (armPressed)
                {
                    if (_probe.Session.Blocked)
                    { _notice = "UNKNOWN: arming blocked until game restart."; Trace("STAGE ARM_REJECTED " + armInput + ": uncertainty latch"); }
                    else _notice = _state.Stage(armInput);
                    changed = true;
                }
                if (Down(_installKey.Value)) { AttemptInstall(armPressed); changed = true; }
                if (changed) Render();
                if (Time.unscaledTime >= _nextPoll)
                {
                    _nextPoll = Time.unscaledTime + 0.75f;
                    if (_state.Snapshot.Player != null && !_reader.Alive(_state.Snapshot.Player)) { Close(); _dismissedUntilRelease = true; return; }
                    if (_reader.HeldId() != _state.Snapshot.WeaponId) { Capture("held weapon changed"); Render(); }
                }
                if (_icons.Value && Time.unscaledTime >= _nextIconPoll)
                {
                    _nextIconPoll = Time.unscaledTime + 0.3f; _overlay?.PollIcons();
                }
            }
            catch (Exception e)
            {
                _read.Note("UI failure: " + e.GetBaseException().GetType().Name + ": " + e.GetBaseException().Message);
                Logger.LogError("Attachment prototype error: " + e);
                Close(); _dismissedUntilRelease = true;
            }
        }
        // Do not use KeyboardShortcut.IsPressed/IsDown here: BepInEx 5's
        // exact-combination checks reject the other keys used by this overlay.
        // Preserve configured modifiers, but tolerate additional held keys.
        private static bool Held(KeyboardShortcut shortcut)
        {
            return KeyChord.IsHeld(shortcut.MainKey, shortcut.Modifiers, Input.GetKey);
        }
        private static bool Down(KeyboardShortcut shortcut)
        {
            return KeyChord.IsDown(shortcut.MainKey, shortcut.Modifiers, Input.GetKey, Input.GetKeyDown);
        }
        private static bool WeaponNumberPressed()
        {
            for (int digit = 0; digit <= 9; digit++) if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha0 + digit))) return true;
            return false;
        }
        private async UniTaskVoid RunVisualLoop(CancellationToken cancellation)
        {
            try
            {
                while (true)
                {
                    // Toolkit injects this phase after ScriptRunBehaviourLateUpdate, before canvas rendering.
                    await UniTask.Yield(PlayerLoopTiming.LastPreLateUpdate, cancellation);
                    cancellation.ThrowIfCancellationRequested();
                    if (_visible || _pose.PresentationReturning) _pose.ApplyPresentationFrame();
                    if (_visible && _overlay != null)
                    {
                        _overlay.ExperimentalHexSelectors = _hexSelectors.Value;
                        _overlay.ExperimentalCrossSelectors = _crossSelectors.Value;
                        _overlay.UseStudioCrossLayout = _studioCross.Value;
                        _overlay.FollowWeapon = _followingCards.Value;
                        _overlay.PoseHeld = _pose.Held;
                        UpdateCrossPresentation();
                        _overlay.CameraFacingCards = _cameraFacingCards.Value;
                        _overlay.PerspectiveStrength = _cardPerspective.Value; _overlay.ClockwiseDegrees = _cardClockwise.Value;
                        _overlay.UpdateLayout();
                    }
                    // EarlyUpdate of the next frame precedes native animation/gameplay Update.
                    await UniTask.Yield(PlayerLoopTiming.EarlyUpdate, cancellation);
                    _pose.RestorePresentationFrame();
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            catch (Exception e)
            {
                Logger.LogError("Attachment layout stopped: " + e);
                Close("layout error"); _dismissedUntilRelease = true;
            }
            finally { _pose?.RestorePresentationFrame(); }
        }
        private string FocusNotice()
        {
            SlotViewModel? slot = _state.Current(_state.Group);
            string group = _state.Group == AttachmentGroup.Underbarrel ? "Foregrip" : _state.Group.ToString();
            if (slot == null) return group + ": no supported slot detected. Inventory unchanged.";
            return group + " / " + slot.Slot.Id + " [" + (_state.SlotIndex(_state.Group) + 1) + "/" +
                _state.GroupSlots(_state.Group).Count + "]: " + slot.Candidates.Count + " candidate(s). Enter stages only.";
        }
        private string CandidateNotice()
        {
            CandidateObservation? candidate = _state.Selected();
            return candidate == null ? "Input received: no candidates in this slot. Scan: " + _state.Snapshot.ScanScope + "." :
                "Browsing " + candidate.Item.Name + ": " + candidate.Detail;
        }
        private void Open()
        {
            Capture();
            if (_overlay==null)
            { _overlay=new AttachmentOverlay(_resources,_read); _overlay.ActionClicked+=RequestClick;
                _overlay.RefreshClicked+=()=> { if(!_clicks.Busy && !_replacement.Pending && !_probe.Session.Busy) { Capture("mouse refresh"); Render(); } }; }
            _visible = true; _overlay.SetVisible(true);
            EnsurePointer();
            Trace("KEY OPEN overlay");
            if (_probe.Session.Revision > 0) _notice = _probe.Session.Status;
            Render();
            if (_visualLoop == null)
            {
                if (!PlayerLoopHelper.IsInjectedUniTaskPlayerLoop()) throw new InvalidOperationException("UnityToolkit player loop is not ready");
                _visualLoop = new CancellationTokenSource();
                RunVisualLoop(_visualLoop.Token).Forget();
            }
        }
        private void Capture(string reason = "overlay opened")
        {
            if (_probe.Session.Busy) { _notice = _probe.Session.Status; return; }
            _notice = ""; _resources.ClearSession();
            _state.ReplaceSnapshot(_reader.Capture(_hints.Value, _unknown.Value), true, reason);
            if (_visible) EnsurePointer();
        }
        private void AttemptInstall(bool armedThisFrame)
        {
            Trace("REQUEST F7_RECEIVED");
            if (_clicks.Busy || _replacement.Pending) { Trace("CLICK_DUPLICATE F7 ignored"); return; }
            _clickStatus="";
            if (_pose.Active)
            {
                _pose.Release("F7 confirmation: return standalone pose first", true);
                RejectInstall("Pose returning. No inventory action queued. Wait for HANDS READY, then press F7 separately again.");
                return;
            }
            string denial = _state.ExecutionDenial(_liveInstall.Value, _probe.Session, armedThisFrame);
            if (denial.Length != 0) { RejectInstall(denial); return; }
            string stagedItem = _state.StagedItemId, stagedSlot = _state.StagedSlotPath;
            _resources.ClearSession();
            _state.ReplaceSnapshot(_reader.Capture(_hints.Value, _unknown.Value), true, "F7 fresh validation");
            bool same = _state.StagedItemId == stagedItem && _state.StagedSlotPath == stagedSlot &&
                _state.Selected()?.Item.Id == stagedItem && _state.Current(_state.Group)?.Slot.Path == stagedSlot;
            if (!same) { RejectInstall("Fresh validation invalidated arm: " + _state.StageReason); return; }
            _state.ClearStage("arm consumed by explicit F7 request; rearm required for any later request");
            TrackPoseResume(_state.Snapshot);
            _probe.TryInstall(_state.Snapshot, _state.Current(_state.Group), _state.Selected(), _liveInstall.Value, same);
            _notice = _probe.Session.Status;
        }
        private void RejectInstall(string reason)
        {
            _notice = "F7 REJECTED: " + reason;
            _probe.Session.RejectRequest(reason);
            Trace("REQUEST F7_REJECTED before probe: " + reason);
        }
        private void Render()
        {
            ApplyMenuControlHint();
            if (_overlay!=null) { _overlay.LiveActionsEnabled=_liveInstall.Value; _overlay.ExperimentalHexSelectors=_hexSelectors.Value; _overlay.ExperimentalCrossSelectors=_crossSelectors.Value; _overlay.UseStudioCrossLayout=_studioCross.Value; _overlay.CameraFacingCards=_cameraFacingCards.Value; }
            _overlay?.Render(_state, _icons.Value);
            UpdateCrossPresentation();
            UpdateActionStatus();
        }
        private void UpdateCrossPresentation()
        {
            _overlay?.SetCrossPresentation(CrossPresentation.HiddenReason(_poseActivation.Open, _pose.Active, _pose.Held,
                _clicks.Busy || _replacement.Pending, _probe.Session.Busy, _poseResume.Pending));
        }
        private void UpdateActionStatus()
        {
            ApplyMenuControlHint();
            _overlay?.SetActionStatus(_state, _probe.Session, _pose.Status.StartsWith("POSE FAULT", StringComparison.Ordinal),
                _replacement.Pending ? _replacement.Status : _clicks.Busy ? "RETURNING WEAPON - " + _clicks.Request!.Action.ToString().ToUpperInvariant() : _clickStatus);
        }
        private void Trace(string reason)
        {
            RaidSnapshot context = _state.Snapshot;
            if (context.Player == null)
            {
                object? player = _reader.MainPlayer();
                context = new RaidSnapshot { Player = player, Weapon = _reader.HeldWeapon(player),
                    Controller = _read.Get(player, "InventoryController", "_inventoryController") };
            }
            string entry = reason + " | " + _state.Identity + " | " + _probe.ReadHands(context).Evidence +
                " frame=" + Time.frameCount + " visible=" + _visible + " focused=" + Application.isFocused +
                " live=" + _liveInstall.Value + " phase=" + _probe.Session.Phase;
            _probe.RecordUi(entry); Logger.LogInfo(entry);
        }
        private void Export()
        {
            try
            {
                // Read the last displayed layout before rebinding the fresh scan; F10 never repacks cards.
                string layout = _overlay?.LayoutReport() ?? "Overlay unavailable; no layout observation.";
                layout += "\nInput capture: registered=" + (_pointer?.Registered == true) + "; active=" + (_pointer?.Capture.Active == true) +
                    "; suppressMouse=" + (_pointer?.Capture.SuppressMouse == true) + "; modifier=" + _mouseModifier.Value + "; fault=" + _pointer?.Fault;
                layout += "\nClick request: " + (_clicks.Request?.Identity ?? "none") + "; status=" + _clickStatus;
                layout += "\nReplacement: phase="+_replacement.Phase+"; pending="+_replacement.Pending+"; "+_replacement.Status;
                layout += "\nPose resume: pending=" + _poseResume.Pending + "; " + _poseResume.Status;
                // Export a fresh observation; preserve selection only when the exact item/slot still exists.
                if (!_probe.Session.Busy)
                {
                    _resources.ClearSession();
                    _state.ReplaceSnapshot(_reader.Capture(_hints.Value, _unknown.Value), true, "F10 report refresh");
                }
                Trace("REQUEST F10_REPORT blocked=" + _probe.Session.Blocked);
                Render(); // Fresh bindings are drawn in the next visual phase.
                string folder = _reports.Write(_state.Snapshot, _state, _il.Value, _probe.History, _liveInstall.Value, _probe.Session.Blocked,
                    layout, _pose.Report());
                Logger.LogInfo("Diagnostic report written: " + folder);
                _notice = "Report saved, including install-probe history. F10 did not initiate an inventory operation.";
            }
            catch (Exception e) { _notice = "Report export failed; check BepInEx/LogOutput.log."; Logger.LogError("Report export: " + e); }
            Render();
        }
        private void Close(string reason = "overlay closed")
        {
            _altMenu.Cancel(); _altPoseRequested = false;
            _legacyPoseActivation.Cancel();
            _poseOwnsOverlay = false;
            _poseActivation.Cancel();
            CancelPoseResume(reason);
            CancelClickRequest(reason);
            _pointer?.Capture.Cancel(); _overlay?.ResetPointer();
            if (_pointerWasActive) { _pointerWasActive = false; Trace("POINTER_RELEASE " + reason); }
            bool normalPoseClose = reason == "F5 toggled off" || reason == "F5 released" || reason == "Alt menu closed";
            _pose?.Release(reason, normalPoseClose);
            // Hide/release input now; retain only the bounded visual outro observer.
            if (_pose?.PresentationReturning != true) StopVisualLoop();
            if (!_visible && _state.Snapshot.Player == null && _state.Snapshot.WeaponId.Length == 0) return;
            Trace("INVALIDATE CLOSE " + reason + " (does not cancel pending request)");
            _state.ClearStage(reason);
            _visible = false; _overlay?.SetVisible(false); _overlay?.ClearLiveReferences(); _resources?.ClearSession();
            // Release live EFT references when closed. The plugin does not own or destroy any game item.
            _state.ReplaceSnapshot(new RaidSnapshot(), false, reason);
        }
        private void StopVisualLoop()
        {
            _visualLoop?.Cancel(); _visualLoop?.Dispose(); _visualLoop = null;
        }
        private void EnsurePointer()
        { EnsurePointer(_state.Snapshot); }
        private void EnsurePointer(RaidSnapshot s)
        {
            if (!_probe.ReadContext(s).Allowed) { DetachPointer(); _overlay?.ResetPointer(); return; }
            if (_pointer == null || !_pointer.Registered || !ReferenceEquals(_pointerPlayer,s.Player))
            {
                DetachPointer(); _pointer = gameObject.AddComponent<AttachmentPointerInput>();
                if (!_pointer.Attach(s.Player,_read)) { Logger.LogWarning("Attachment mouse input unavailable; keyboard controls retained."); DetachPointer(); return; }
                _pointer.LocalOwnership = PointerLocalOwnership;
                _pointer.Eligible = PointerEligible;
                _pointer.DesiredCapture = AltCaptureRequested;
                _pointer.BeforeGameplayCommand = OnGameplayCommand;
            }
            if (_pointerWeapon != null && !ReferenceEquals(_pointerWeapon,s.Weapon))
            { _altMenu.Cancel(); _pointer.Capture.Cancel(); _overlay?.ResetPointer(); }
            _pointerPlayer=s.Player; _pointerWeapon=s.Weapon; _pointerHands=_read.Get(s.Player,"HandsController");
            _pointerContext=s;
            _pointer.Modifier=_mouseModifier.Value;
        }
        private RaidSnapshot? _pointerContext;
        private void OnGameplayCommand(string command)
        {
            if (!_poseOwnsOverlay && !_pose.Active) return;
            Close("native input " + command);
            _dismissedUntilRelease = true;
            // Do not detach the input node while its owner's child list is traversed.
        }
        private bool PointerLocalOwnership() => isActiveAndEnabled && Application.isFocused &&
            !Cursor.visible && Cursor.lockState == CursorLockMode.Locked && _pointerPlayer != null &&
            _pointerContext != null && _probe.ReadContext(_pointerContext).Allowed &&
            ReferenceEquals(_reader.MainPlayer(),_pointerPlayer) &&
            ReadAccess.Bool(_read.Get(_read.Get(_pointerPlayer,"HealthController"),"IsAlive")) == true &&
            ReadAccess.Bool(_read.Get(_pointerPlayer,"IsInventoryOpened")) == false;
        private bool PointerEligible() => _enabled.Value && (_altMenu.Open || _visible && _overlay?.PointerReady == true) &&
            !_probe.Session.Blocked && !_pose.Status.StartsWith("POSE FAULT",StringComparison.Ordinal) &&
            (_altMenu.Open || _poseActivation.Open || Held(_open.Value) || (_toggle.Value && !_poseOwnsOverlay)) &&
            !Input.GetKeyDown(KeyCode.Escape) && !Input.GetKeyDown(KeyCode.Tab) && !Input.GetKeyDown(KeyCode.F12) &&
            (!_visible || ReferenceEquals(_pointerWeapon,_state.Snapshot.Weapon)) && ReferenceEquals(_reader.HeldWeapon(_pointerPlayer),_pointerWeapon) &&
            ReferenceEquals(_read.Get(_pointerPlayer,"HandsController"),_pointerHands);
        private void DetachPointer()
        {
            if (_pointer != null) { _pointer.Detach(); Destroy(_pointer); _pointer=null; }
            _pointerPlayer=_pointerWeapon=_pointerHands=null;
            _pointerContext=null;
        }
        private void OnDisable() { Close(); DetachPointer(); _attachmentSpeed?.End(); _pose?.Shutdown("component disabled"); }
        private void OnDestroy() { Close(); DetachPointer(); _attachmentSpeed?.End(); _pose?.Shutdown("component destroyed"); _overlay?.Dispose(); _overlay = null; }
    }
}
