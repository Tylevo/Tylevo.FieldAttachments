#!/usr/bin/env python3
"""Structural source checks only, NOT a C# compiler or runtime tests."""
from pathlib import Path
import hashlib, json, re, xml.etree.ElementTree as ET
from datetime import datetime, timezone
ROOT = Path(__file__).resolve().parents[1]
checks = []
def check(name, value):
    checks.append({'check': name, 'passed': bool(value)})
    if not value: raise AssertionError(name)
def code_only(source):
    # This source package uses ordinary C# quoted strings/chars, not raw/interpolated strings.
    out=[]; i=0; state='code'
    while i < len(source):
        c=source[i]; n=source[i+1] if i+1<len(source) else ''
        if state=='code':
            if c=='/' and n=='/': state='line';out.extend('  ');i+=2;continue
            if c=='/' and n=='*': state='block';out.extend('  ');i+=2;continue
            if c in ('"', "'"): state='string' if c=='"' else 'char';out.append(' ');i+=1;continue
            out.append(c);i+=1;continue
        if state=='line':
            out.append('\n' if c=='\n' else ' ')
            if c=='\n':state='code'
            i+=1;continue
        if state=='block':
            if c=='*' and n=='/':state='code';out.extend('  ');i+=2;continue
            out.append('\n' if c=='\n' else ' ');i+=1;continue
        if c=='\\':out.extend('  ');i+=2;continue
        if (state=='string' and c=='"') or (state=='char' and c=="'"):state='code'
        elif c=='\n':raise AssertionError('Unterminated quoted C# literal')
        out.append(' ');i+=1
    if state not in ('code','line'):raise AssertionError('Unterminated C# comment/literal')
    return ''.join(out)


sources = sorted(p for p in (ROOT/'src').rglob('*.cs') if 'obj' not in p.parts and 'bin' not in p.parts) + sorted((ROOT/'tests').glob('*.cs'))
for p in sources:
    text = code_only(p.read_text(encoding='utf-8'))
    stack=[]; pairs={')':'(', ']':'[', '}':'{'}
    for ch in text:
        if ch in '([{': stack.append(ch)
        elif ch in pairs:
            if not stack or stack.pop()!=pairs[ch]: raise AssertionError('Delimiters: '+str(p))
    check('Token/delimiter sanity: '+str(p.relative_to(ROOT)), not stack)
plugin=(ROOT/'src/Plugin.cs').read_text()
probe=(ROOT/'src/Runtime/NativeInstallProbe.cs').read_text()
gate=(ROOT/'src/Core/InstallGate.cs').read_text()
report=(ROOT/'src/Runtime/ReportWriter.cs').read_text()
policy=(ROOT/'src/Core/CandidatePolicy.cs').read_text()
check('Attachment changes default ON with the existing saved configuration key', '"Experiments", "EnableEmptySlotInstall", true' in plugin)
check('Arm inputs remain staging-only', '_notice = _state.Stage(armInput)' in plugin and 'returnDown || keypadDown || shortcutArm' in plugin)
check('Dedicated F7 path exists', 'new KeyboardShortcut(KeyCode.F7)' in plugin and 'if (Down(_installKey.Value)) { AttemptInstall(armPressed);' in plugin)
check('Attempt requires current staged IDs and fresh capture', '_state.ExecutionDenial(' in plugin and '_state.Selected()?.Item.Id == stagedItem' in plugin and '_state.ReplaceSnapshot(_reader.Capture' in plugin)
check('Task polling is before UI focus/config checks', plugin.index('_probe.Poll();') < plugin.index('if (!_enabled.Value'))
close = plugin[plugin.index('private void Close('):plugin.index('private void EnsurePointer()')]
check('UI close does not reset live probe', '_probe = ' not in close and '_probe.Session.' not in close and '_probe.TryInstall' not in close)
check('No exact chord calls return', not re.search(r'(?:shortcut|_open.Value|_report.Value|_refresh.Value)\.(IsPressed|IsDown)\(', code_only(plugin)))
check('Exact inspected game module guard', 'cc2d80b0-6d5b-4cb1-a581-6d2cc901d4c7' in probe and 'KnownBuild' in gate)
session_reader = (ROOT/'src/Runtime/LocalSessionReader.cs').read_text()
session_facts = (ROOT/'src/Core/LocalSessionFacts.cs').read_text()
check('Fika/live custom controllers fail closed', 'StartsWith("Fika"' in session_reader and 'SinglePlayerInventoryController' in session_facts and 'context.SupportedTypes && !context.FikaLoaded' in probe)
check('Range requires separate native inventory and active state', all(s in session_facts for s in ['InShootingRange != true','UpdatingInventory != false','InPatrol != false','FirearmsBlocked != false','!RangeInventoryMatches','!OriginalInventorySeparate','!RangeEquipmentSeparate']))
check('Range context loss retains pending uncertainty', '_requestContextLost = true' in probe and 'No retry or profile synchronization.' in probe)
check('Empty-slot supported attachment and carried-storage restrictions', 'SourceIsCarried' in gate and 'SupportedLeafAttachment' in gate and 'f.Empty != true' in gate)
check('Single native simulation invocation', probe.count('move.Invoke(')==1 and '(object)true' in probe)
check('No swap or direct add/remove binding in probe', not re.search(r'Exact\([^;]*"(?:Swap|Add|Remove)"', probe))
check('Native submission task inspected, not blocked on', 'submit.Invoke' in probe and 'task.IsCompleted' in probe and '.Wait(' not in code_only(probe))
check('No task retry or manual rollback call', 'RollBack(' not in code_only(probe) and 'Task.Run(' not in code_only(probe))
check('Uncertain completion latches', 'Blocked |= uncertain' in gate and 'Blocked = true;' in gate)
check('Only proven native fits shown despite legacy hint flags', 'if (native != true) continue;' in policy and 'AttachmentAssembly.Supports(slot.Group,item)' in policy)
check('Metadata exporter only reads methods', '.Invoke(' not in code_only((ROOT/'src/Runtime/TargetedApiMap.cs').read_text()))
check('Report and collector include probe outcomes', 'install-probe.txt' in report and 'install-probe.txt' in (ROOT/'tools/Collect-Feedback.ps1').read_text())
check('Report no longer unconditionally says executor absent', 'executor absent' not in report and 'No inventory operation was invoked' not in report)
check('New guard/lifecycle test cases present', all(x in (ROOT/'tests/Program.cs').read_text() for x in ['Unknown safety fact denies','Late completion never clears','Native filter rejection overrides']))
for p in [ROOT/'src/Tylevo.FieldAttachments.csproj',ROOT/'tests/FieldAttachments.Tests.csproj']:
    tree=ET.parse(p)
    for c in tree.findall('.//Compile'):
        include=c.attrib.get('Include','')
        check('Referenced test source exists: '+include, bool(list(p.parent.glob(include))))
motion=(ROOT/'src/Runtime/MdrInspectionMotion.cs').read_text()
check('MDR cleanup never swaps back the live controller', 'PoseBindingLease<RuntimeAnimatorController, AnimationClip>' in motion and 'controller => animator.runtimeAnimatorController = controller' not in motion and 'property.SetValue(wrapper, created)' in motion)
check('MDR changes observe state without replaying or rebinding', 'VerifyUnchanged' in motion and not re.search(r'\.(?:Play|Rebind|Update|SetBool|SetInteger|SetFloat)\(', code_only(motion)))
check('MDR return requires next-frame observation', '_cleanup.Observe(Time.frameCount' in (ROOT/'src/Runtime/InspectionPose.cs').read_text())
pose=(ROOT/'src/Runtime/InspectionPose.cs').read_text()
mcx=(ROOT/'src/Runtime/InspectionPose.Mcx.cs').read_text()
check('MCX seek is consumed once and observed before pausing', mcx.count('_animator.Play(')==1 and mcx.index('_mcxEntry.TryIssue(')<mcx.index('_animator.Play(') and '_mcxEntry.Observe(Time.frameCount' in mcx and 'if (!TickCustomStm() || !TickGlobalMcx() || !TickMcxEntry()) return;' in pose)
check('MCX validates native events and preserves the natural outro', 'McxEventsMatch()' in mcx and '"_stateHashToEventsCollection"' in mcx and 'if (McxSegmentActive) shortReturn = false;' in pose and '_motionAttempted = learn || _customStm || _mcxNative' in pose)
check('MCX does not rewrite events, operation state, speed, controller or inventory', not re.search(r'\.(?:Update|Rebind|SetTrigger|TryInstall|ApplyOverrides)\(',code_only(mcx)) and not re.search(r'\.(?:speed|runtimeAnimatorController|events)\s*=(?!=)',code_only(mcx)))
check('Sprint reads player physical state without a weapon animation flag dependency', 'NativeSprintReader.Read' in pose and '"Physical"), "Sprinting"' in (ROOT/'src/Runtime/NativeSprintReader.cs').read_text() and 'BOOL_SPRINT' not in code_only(pose))
check('Cleanup captures visible idle before restoring clip', pose.index('bool wasIdle = expectIdle') < pose.index('if (!RestoreMotion()) return;'))
check('Version matches plugin/project/helper', '"1.0.0"' in plugin and '<Version>1.0.0</Version>' in (ROOT/'src/Tylevo.FieldAttachments.csproj').read_text() and "pluginVersion = '1.0.0'" in (ROOT/'tools/Build.ps1').read_text())
check('No game DLLs or uploaded diagnostic data in source', not list((ROOT/'src').glob('*.dll')) and not list(ROOT.rglob('snapshot*.json')) and not list(ROOT.rglob('api-map*.txt')))
overlay=(ROOT/'src/Runtime/AttachmentOverlay.cs').read_text()
check('Toolkit visual timing has one caller and cancellation except bounded pose outro', 'PlayerLoopTiming.LastPreLateUpdate, cancellation' in plugin and 'private void LateUpdate()' not in plugin and '_visualLoop?.Cancel(); _visualLoop?.Dispose(); _visualLoop = null;' in close and 'if (_pose?.PresentationReturning != true) StopVisualLoop();' in close and plugin.count('_overlay.UpdateLayout();') == 1)
check('Toolkit dependency is declared and not copied', '[BepInDependency("com.arys.unitytoolkit", "2.0.2")]' in plugin and '<Reference Include="UniTask">' in (ROOT/'src/Tylevo.FieldAttachments.csproj').read_text())
check('Render and F10 do not resample or repack visual geometry', 'UpdateLayout();' not in overlay and plugin.index('string layout = _overlay?.LayoutReport()') < plugin.index('_state.ReplaceSnapshot', plugin.index('private void Export()')))
check('Dedicated homes use one shared weapon projection without arrangement history', '_anchors.ProjectCardPlane(' in overlay and 'DedicatedCardLayout.TryPlace(' in overlay and '_arrangement' not in overlay and 'ProjectPanel(' not in overlay)
check('Only compact action feedback remains in HUD', 'TYLEVO FIELD ATTACHMENTS' in overlay and 'OverlayFeedback.Status' in overlay and all(x not in overlay for x in ['"Footer"', '"Controls"', '"ProjectionStatus"', '"InspectionPoseStatus"', '"Summary"', 'ARMED (F7)']))
pointer=(ROOT/'src/Runtime/AttachmentPointerInput.cs').read_text()
mouse=(ROOT/'src/Runtime/AttachmentOverlay.Pointer.cs').read_text()
check('Pointer uses local native child registration and module gate', 'InspectedGameMvid' in pointer and '_ownerChildren.Add(this)' in pointer and '_ownerChildren?.Remove(this)' in pointer)
check('Pointer does not change global cursor, camera or time scale', not re.search(r'(?:Cursor\.(?:visible|lockState)|Time\.timeScale)\s*=(?!=)', code_only(pointer + mouse + plugin)))
check('Dropdown emits explicit action; cannot submit or arm directly', 'ActionClicked?.Invoke(' in mouse and 'PointerKind.Uninstall' in mouse and '_pointerState.Highlight(' in mouse and 'TryUnmap(' in mouse and not any(x in mouse for x in ['TryInstall(', 'AttemptInstall(', '.Stage(']))
check('Native input samples modifier in callbacks and preserves release policy', 'Input.GetKey(Modifier)' in pointer and 'PointerInputPolicy.Block' in pointer and 'PointerInputPolicy.FilterAxes' in pointer)
check('Pointer releases during overlay close and component teardown', '_pointer?.Capture.Cancel()' in close and 'DetachPointer(); _attachmentSpeed?.End(); _pose?.Shutdown' in plugin)
click=(ROOT/'src/Plugin.ClickActions.cs').read_text()
pockets=(ROOT/'src/Runtime/NativeInstallProbe.Storage.cs').read_text()
check('Clicked identity is consumed once before fresh native validation', click.index('_clicks.Take(); _clickHands=null;') < click.index('request.Validate(fresh)') < click.index('_probe.TryInstall('))
check('Waiting click cancels on close, context, timeout and interruptions', 'CancelClickRequest(reason)' in close and all(x in click for x in ['_clicks.Expired(', 'PointerLocalOwnership()', 'NativeSprintReader.Read', 'Down(_refresh.Value)', 'WeaponNumberPressed()']))
check('Uninstall uses native compatible space in verified carried grids', 'FindLocationForItem' in pockets and 'SourceContainsItem()!=false' in pockets and 'new CarriedStorageReader(_read).Capture(controller)' in probe and 'ReferenceEquals(g.NativeGrid,_sourceGrid)' in probe)
check('Uninstall checks source and destination completion identities', 'f.Removing && (!f.InstalledItemMatches || f.Empty != false)' in gate and 'Equals(_read.Get(_item,"Parent"),_destinationAddress)' in probe)
check('Pending requests retain Alt input capture but disable pointer actions', '!_probe.Session.Busy' not in plugin[plugin.index('private bool PointerEligible()'):plugin.index('private void DetachPointer()')] and 'HandlePointer(active && !_probe.Session.Busy,' in plugin)
check('Legacy presentation is unbound by default and retains shared cancellation', '"AttachmentPose", new KeyboardShortcut(KeyCode.None)' in plugin and '_legacyPoseActivation.Sample(posePhysical,poseDown,_poseToggle.Value)' in plugin and '_poseActivation.Sample(legacyPoseHeld || menuHeld,false,false)' in plugin and '_poseActivation.Cancel();' in close)
alt=(ROOT/'src/Plugin.AltMenu.cs').read_text()
activation=(ROOT/'src/Core/AltMenuActivation.cs').read_text()
hint=(ROOT/'src/Runtime/AttachmentOverlay.Controls.cs').read_text()
check('Alt menu defaults LeftAlt/Hold with an F12 toggle option', '"MenuKey", AttachmentMenuKey.LeftAlt' in alt and '"MenuActivation", MenuActivationMode.Hold' in alt and 'enum MenuActivationMode { Hold, Toggle }' in activation)
check('Closed local input observer samples intent before native commands', 'MaintainAltInput();' in plugin and '_pointer.DesiredCapture = AltCaptureRequested;' in plugin and 'DesiredCapture?.Invoke(own)' in pointer and pointer.index('bool own = Refresh();') < pointer.index('PointerInputPolicy.Block(name,'))
check('Alt capture respects ownership, focus, identity and previous outro', all(s in alt for s in ['own && identity', 'Application.isFocused', '!_pose.PresentationReturning', '_legacyPoseActivation.Open && !_altPoseRequested']))
check('Toggle chords retain Alt R and pointer interactions', 'Held(_cyclePosition.Value) || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)' in alt and 'if (held && chordUsed) _closeOnRelease = false;' in activation and 'if (!held && _closeOnRelease)' in activation)
check('Alt close is after native completion polling and cancels requested lifetime', plugin.index('_probe.Poll();') < plugin.index('if (menuClose)') and '_altMenu.Cancel(); _altPoseRequested = false;' in close and '_legacyPoseActivation.Cancel();' in close)
check('Bottom control hint is separate, noninteractive and omits wheel advice', all(s in alt for s in ['HOLD MENU', 'TOGGLE MENU', 'LMB SELECT', 'RMB CLOSE LIST', 'ALT+R MOUNT']) and 'WHEEL' not in alt and 'Box(_root.transform, "MenuControlHint"' in hint and 'Label(_menuControlPlate, "Controls"' in hint and 'image.raycastTarget = false' in overlay and 'text.raycastTarget = false' in overlay and 'HideMenuControlHint();' in overlay)
scope=(ROOT/'src/Core/AttachmentScope.cs').read_text()
request=(ROOT/'src/Core/AttachmentRequest.cs').read_text()
check('Click UI and native adapter share intact assembly scope', 'AttachmentAssembly.Supports(slot.Group,item)' in request and 'AttachmentAssembly.Supports(chosenSlot?.Slot.Group,chosen)' in probe and 'ReadAccess.IsKind(_item, "EFT.InventoryLogic.Mod") && assemblyMatches' in probe)
check('Scope lists all four groups without generic Mod or Mount acceptance', all('case AttachmentGroup.'+group+':' in scope for group in ['Optic','Muzzle','Tactical','Underbarrel']) and 'runtimeType=="EFT.InventoryLogic.Mod"' not in scope and 'runtimeType=="EFT.InventoryLogic.Mount"' not in scope)
hexview=(ROOT/'src/Runtime/AttachmentOverlay.Hex.cs').read_text()
crossview=(ROOT/'src/Runtime/AttachmentOverlay.Cross.cs').read_text()
check('Experimental hex selectors default OFF and reset input on presentation change', '"HexAttachmentSelectors", false' in plugin and '_experimentalHexSelectors=value; ResetPointer();' in hexview and 'ApplySelectorVisibility();' in hexview and 'panel.Root.gameObject.SetActive(!ExperimentalCrossSelectors && !ExperimentalHexSelectors)' in crossview)
check('Hex clicks share pointer gesture and native action dispatch', 'if (ExperimentalHexSelectors) return HitHex(x,y);' in mouse and 'HexSelectorLayout.Contains(' in hexview and 'PointerKind.Uninstall' in hexview and not any(x in hexview for x in ['TryInstall(', '.Stage(', 'ActionClicked?.Invoke']))
check('Hex geometry and hit testing share dimensions', 'HexSelectorLayout.Width,HexSelectorLayout.Height' in hexview and 'HexSelectorLayout.TryPlane(plane!,out plane)' in overlay and 'local.x / _width, -local.y / _height' in (ROOT/'src/Runtime/ProjectedCardMesh.cs').read_text())
check('Hex diagnostics and cleanup are present', 'AppendHexReport(report)' in overlay and 'ClearHexItems();' in overlay and 'Selector style:' in overlay)
check('Current cross defaults ON with explicit precedence and pointer cancellation', '"CrossAttachmentSelectors", true' in plugin and '_experimentalCrossSelectors=value; ResetPointer();' in crossview and 'panel.Root.gameObject.SetActive(!ExperimentalCrossSelectors && ExperimentalHexSelectors)' in crossview)
check('Legacy cross retains shared weapon projection with per-group render/hit roots', '_anchors.ProjectCardPlane(' in crossview and 'CrossSelectorLayout.TryPlace(' in crossview and 'mesh.SetProjection(_crossProjection,panel.Root,_canvasRect,CrossSelectorLayout.Width,CrossSelectorLayout.Height)' in crossview and 'panel.Projection.TryUnmap(' in crossview)
check('Cross uses existing native action dispatch without submitting or arming', 'ActionDenial(' in crossview and 'PointerKind.Uninstall' in crossview and 'PointerKind.ChooseItem' in crossview and not any(x in crossview for x in ['TryInstall(', '.Stage(', 'ActionClicked?.Invoke']) and mouse.index('if (ExperimentalCrossSelectors) return HitCross(x,y);') < mouse.index('if (ExperimentalHexSelectors) return HitHex(x,y);'))
studio=(ROOT/'src/Runtime/AttachmentOverlay.Studio.cs').read_text()
check('Studio export defaults ON only within cross, cancels presses and preserves native leaders', '"StudioCrossLayout", true' in plugin and '_studioCrossLayout=value; ResetPointer(); _sampleFrame=-1;' in crossview and 'if(UseStudioCrossLayout) { UpdateStudioCrossLayout(' in crossview and '_anchors.Project(PreviewSlot(group.Group,group.Slot),width,height,out end)' in studio)
check('Studio render and hit share the authored group geometry', 'mesh.SetProjection(panel.Projection,panel.Root,_canvasRect,StudioCrossLayout.Width,StudioCrossLayout.Height)' in studio and 'StudioCrossLayout.Hit(px,py)' in crossview and 'StudioCrossLayout.Cell(index)' in crossview)
check('Studio has no animation/inventory writes', not any(x in studio for x in ['TryInstall(', '.Stage(', 'ActionClicked?.Invoke', 'Animator', '.Play(', '.speed']))
palette=(ROOT/'src/Runtime/TarkovUiPalette.cs').read_text()
check('Cross palette reads native Tarkov interaction and selection colors', all(x in palette for x in ['EFT.UI.InteractionButton','EFT.UI.DragAndDrop.ItemView','"NormalColor"','"DisabledColor"','"SelectedColor"','"DefaultSelectedColor"']) and 'ApplyCrossPalette();' in crossview and 'ExperimentalCrossSelectors ? _crossPalette.Disabled' in mouse)
check('Cross diagnostics and cleanup are present', 'AppendCrossReport(report)' in overlay and 'ClearCrossItems();' in overlay and 'Cross palette:' in crossview and 'Cross geometry:' in crossview)
anchor=(ROOT/'src/Runtime/WeaponAnchorReader.cs').read_text()
check('Camera-facing defaults ON, shares all layouts and cancels obsolete presses', '"CameraFacingCards", true' in plugin and '_cameraFacingCards = value; ResetPointer(); _sampleFrame = -1;' in overlay and 'out weaponPoint, CameraFacingCards)' in overlay and 'out plane,out weaponPoint,CameraFacingCards)' in crossview and 'cameraFacing=' in overlay)
check('Camera-facing surfaces retain native anchor/depth and bypass only the orientation basis', 'if (!faceCamera && !basisAvailable)' in anchor and 'if (!faceCamera && !nativePlane)' in anchor and 'screen.z' in anchor and 'out var cardRight, out var cardUp, faceCamera)' in anchor and not re.search(r'\.(?:position|rotation|localPosition|localRotation)\s*=(?!=)', code_only(anchor)))
check('Camera-facing movement reserves bounds with shared continuous sway', 'Math.Atan(' in (ROOT/'src/Core/DedicatedCardLayout.cs').read_text() and 'DedicatedCardLayout.SoftMotion(' in (ROOT/'src/Core/CrossSelectorLayout.cs').read_text())
resume=(ROOT/'src/Plugin.PoseResume.cs').read_text()
check('Pose resume uses the existing inspected Start path without inventory execution', '_pose.Start(snapshot,false);' in resume and 'TrackPoseResume(fresh);' in click and 'TrackPoseResume(_state.Snapshot);' in plugin and not any(x in resume for x in ['TryInstall(', 'ExamineWeapon', '.speed =', '.Play(']))
check('Pose resume cancels on close and logs transitions only', 'CancelPoseResume(reason);' in close and 'if (before!=_poseResume.Status)' in resume and 'Down(_report.Value)' in resume)
check('Pose resume polling follows native completion and menu/death checks', plugin.index('_probe.Poll();') < plugin.index('Close("player no longer alive")') < plugin.index('PollPoseResume(interruptPose);') < plugin.index('PollClickRequest();'))
storage=(ROOT/'src/Runtime/CarriedStorageReader.cs').read_text()
check('Scanner and executor share bounded accessible storage traversal', 'new CarriedStorageReader(_r).Capture(controller)' in (ROOT/'src/Runtime/RaidReader.cs').read_text() and 'new CarriedStorageReader(_read).Capture(controller)' in probe and 'result.Entries>2048' in storage and 'depth>8' in storage)
check('Carried storage uses native searched/unknown queries without revealing content', 'contract.GetMethod("IsSearched"' in storage and 'contract.GetMethod("ContainsUnknownItems"' in storage and not any(x in storage for x in ['SetItemAs', 'SearchContents(', 'TryInstall(']))
check('Fresh source identity includes native grid and address', 'ReferenceEquals(chosen!.SourceGrid,_sourceGrid)' in probe and 'Equals(chosen.SourceAddress,_sourceAddress)' in probe and 'Equals(_sourceAddress,now._sourceAddress)' in request and 'if(!SameBinding(now!))' in request)
globalmcx=(ROOT/'src/Runtime/InspectionPose.GlobalMcx.cs').read_text()
donor=(ROOT/'src/Runtime/McxInspectionDonor.cs').read_text()
check('Archived native MCX implementation retains its old guards and stored default', '"GlobalMcxInspection", false' in plugin and '!GlobalMcxInspectionEnabled || learn' in globalmcx and '_mcxNative || GlobalMcxInspectionEnabled || !MdrMotionEnabled' in pose)
check('Global replacement validates custom events and verifies next-frame clip before seek', globalmcx.index('GlobalMcxInspection.EventDenial(')<globalmcx.index('_motion.TryApply(') and 'Time.frameCount<=_mcxAppliedFrame' in globalmcx and 'McxEntryFault("global MCX clip read-back uncertain")' in globalmcx)
check('Global donor is local, fingerprinted and unloads only its own bundle', 'SHA256.Create()' in donor and 'Object.Instantiate(clips[0])' in donor and 'if(owned!=null) owned.Unload(true)' in donor and 'LoadAssetWithSubAssets<AnimationClip>' in donor)
check('Global UI and native inventory dispatch are not changed by animation', not any(x in globalmcx+donor for x in ['TryInstall(', 'ActionClicked', '.Stage(', 'SetTriggerPressed']))
globalcore=(ROOT/'src/Core/GlobalMcxInspection.cs').read_text()
check('Native MCX stays exact-three while donor profiles have bounded event reads', 'nativeMcx ? events.Count != 3 : events.Count < 3 || events.Count > maximumEvents' in mcx and 'int maximumEvents = GlobalMcxInspection.MaximumEvents' in mcx and 'events.Count<3 || events.Count>MaximumEvents' in globalcore)
check('Optional donor events remain guarded named audio with no template allowlist', 'case "GunFlip": case "GunFlip1": case "GunFlip2":' in globalcore and 'e.Name!="Sound" || e.Hash!=1554795451 || e.ParameterType!=3' in globalcore and 'sr25' not in globalcore.lower() and 'm700' not in globalcore.lower())
stm=(ROOT/'src/Runtime/InspectionPose.Stm.cs').read_text()
check('STM trial is opt-in and bypasses MCX only for its selected native path', '"StmPresentationTrial", false' in plugin and 'StmPresentation.Requested(StmPresentationEnabled, template)' in stm and 'learn || _stmTrial ||' in globalmcx)
check('STM marker/rig prepared before native inspection starts', pose.index('PrepareStm(')<pose.index('examine.Invoke(') and 'StmPresentation.MarkerDenial(' in stm and 'if (!_stmTrial || learn) return;' in stm)
check('STM render writes require owned native clip, hands, context and phase-specific speed', all(x in stm for x in ['!SameLiveAnimator()', '!OwnedOperation()', '!StartEventFired()', '!NeutralHands()', 'PosePhase.Held', 'clips[0].clip.name != "stm9_look"', '_animator!.speed != 0', '_animator.speed != _stmPlaybackSpeed']))
check('STM camera excluded and weapon/arms have one parent', all(x in stm for x in ['weapon.parent != root', 'left.parent != root', 'right.parent != root', 'cameraBone.parent != root', 'camera.transform.IsChildOf(weapon)', 'camera.transform.IsChildOf(left)', 'camera.transform.IsChildOf(right)']))
check('STM visual offset precedes card projection and restores before next native frame', plugin.index('_pose.ApplyPresentationFrame();')<plugin.index('_overlay.UpdateLayout();') and 'PlayerLoopTiming.EarlyUpdate, cancellation' in plugin and 'finally { _pose?.RestorePresentationFrame(); }' in plugin)
release=pose[pose.index('private void ReleaseLegacy('):pose.index('private bool SameLiveAnimator()')]
check('STM restores visual roots before speed and keeps native outro', release.index('RestorePresentationFrame()')<release.index('_speed.TryRelease()') and 'if (_stmTrial) shortReturn = false;' in release)
check('STM adds no native inventory, controller or camera writes', not any(x in code_only(stm) for x in ['TryInstall(', 'SetTriggerPressed(', 'ExamineWeapon(', '.Play(']) and not re.search(r'(?:\.runtimeAnimatorController|\.speed|\.localScale|_stmCamera\.transform\.(?:rotation|position))\s*=(?!=)',code_only(stm)))
stm_bypass=stm[stm.index('if (denial.Length != 0)',stm.index('private void ApplyLegacyPresentationFrame()')):stm.index('Vector3 pivot =',stm.index('private void ApplyLegacyPresentationFrame()'))]
check('STM visual refusal keeps native hold and restores the visual frame', 'RestorePresentationFrame(); _stmBypass = denial;' in stm_bypass and 'Release(' not in code_only(stm_bypass) and '_speed.' not in code_only(stm_bypass))
check('STM visual refusal reports once per opening without retries', '_stmBypass.Length != 0) return;' in stm and '_stmBypass = "";' in stm and 'Event("STM_BYPASS " + denial' in stm)
check('STM guard refusal names separate native and visual facts', all(x in stm for x in ['live animator/context:', 'hands not neutral:', 'paused speed ownership changed', 'bound FPS camera inactive', 'rig FOV scale:', 'native stm9_look clip not confirmed']))
check('STM FOV check uses the owned native scale root and current native depth', all(x in stm for x in ['"HandsHierarchy"', '"Self") as Transform != _stmRoot', '"RibcageScaleCurrent"', 'StmPresentation.ScaleDenial(', '_stmRoot.localToWorldMatrix']))
check('STM FOV check runs at preparation and before held writes', 'StmScaleDenial(snapshot.Player)' in stm and 'StmScaleDenial(_context.Player)' in stm and stm.index('string denial = StmFrameDenial();') < stm.index('_stmFrame.Apply(_stmValues);'))
check('STM rotates sibling roots in rig space before inherited native scale', all(x in stm for x in ['InverseTransformPoint(_stmPivot!.position)', 'InverseTransformDirection(_stmCamera!.transform.forward)', 'bone.localPosition - pivot', 'Rotation(turn * bone.localRotation)']))
framelease=(ROOT/'src/Core/PoseFrameLease.cs').read_text()
check('Frame ownership remains exact while STM restored rotations use angular equivalence', 'EqualityComparer<T>.Default.Equals(current, _written[i])' in framelease and '(i, a, b) => i % 2 == 0 ? a.Equals(b) : StmPresentation.RestoredRotation(' in stm)
check('Rejected restore read-back retains ownership and first fault detail', '_written[i] = restored;' in framelease and 'expected=' in framelease and 'observed=' in framelease and 'presentation restore pending at component ' in framelease and 'if (Fault.Length == 0) Fault = reason;' in framelease)
check('STM frame fault prevents another visual apply and diagnostics retain precision', stm.index('if (_stmFrame.Fault.Length != 0) { Release(') < stm.index('_stmFrame.Apply(_stmValues);') and 'ToString("R", CultureInfo.InvariantCulture)' in stm)
check('STM rise waits for stable native start and blends against the saved marker', '&& !_stmApplied && !StmEntryReady()) return;' in stm and '_stmBlend.Entry(Time.unscaledTime' in stm and '_targetMarker!.Time, _state.Phase == PosePhase.Held)' in stm)
check('STM normal close keeps native speed restoration and cannot restart return', release.index('PosePhase.Returning && !_speed.Active') < release.index('if (normalKeyRelease) BeginStmReturn();') < release.index('_speed.TryRelease()') and 'if (!normalKeyRelease) StopStmReturn(reason);' in release)
check('STM return observes safety before cleanup even with hidden overlay', 'TickStmReturn(enabled && StmPresentationEnabled && Application.isFocused && !interrupt);' in pose and 'string denial = !allowed ? "focus/disable/gameplay interruption" : StmFrameDenial();' in stm and 'if (_visible || _pose.PresentationReturning) _pose.ApplyPresentationFrame();' in plugin)
check('Hidden visual observer stops when fade ends and never retains pointer input', 'if (!_visible && !_pose.PresentationReturning) StopVisualLoop();' in plugin and close.index('_pointer?.Capture.Cancel();') < close.index('_pose?.Release(reason, normalPoseClose);') and '_visible = false;' in close)
custom=(ROOT/'src/Runtime/InspectionPose.CustomStm.cs').read_text()
customclip=(ROOT/'src/Runtime/CustomStmClip.cs').read_text()
check('Custom attachment animations are always enabled before presentation updates', '_pose.CustomStmAnimationEnabled = true;' in plugin and '_customStmAnimation' not in plugin and '"CustomStmAnimation"' not in plugin and plugin.index('_pose.CustomStmAnimationEnabled = true;') < plugin.index('_pose.Tick(') < plugin.index('_pose.Start('))
check('Archived authored/native implementation retains preparation order', 'CustomStmInspection.Requested(CustomStmAnimationEnabled, template, learn)' in custom and pose.index('PrepareCustomStm(')<pose.index('examine.Invoke('))
check('Custom STM requires native rig/events, early window and next-frame clip confirmation', all(s in custom for s in ['MissingRigPath(', 'CustomStmInspection.EventDenial(_template, nativeClip, events)', 'CustomStmInspection.EarlyEnough(', 'Time.frameCount <= _customStmAppliedFrame', '!CustomStmClipMatches() || !OwnedOperation() || !StartEventFired()']))
check('Custom STM never seeks, rebinds or touches inventory', not any(s in code_only(custom) for s in ['.Play(', '.Rebind(', 'TryInstall(', 'TryUninstall(', 'SetTriggerPressed(', 'CrossFadeInFixedTime(']))
check('Authored exit is observed before native idle cleanup, interruptions cancel it', pose.index('if (WaitCustomStmReturn(mayCancel)) return;')<pose.index('if (_idleReturn.Active && sameLiveContext') and 'if (!normalKeyRelease) _customStmReturn.Cancel();' in pose and 'CustomStmReturnResult.BlendToIdle) TryShortReturn(true);' in custom)
check('Custom uncertainty retains the existing session latch and restore path', '_motion.LatchFault(reason); Release(reason); RestoreMotion();' in custom and 'if (_customStmAwaiting || _customStmReturn.Pending) CustomStmFault(' in pose)
check('Event lookup uses actual Hands state binding instead of serialized behaviour hash', 'GetBehaviours(stateHash, 1)' in mcx and 'matches.Length != 1' in mcx and 'hash == PoseMarker.SharedState' not in mcx)
customcore=(ROOT/'src/Core/CustomStmInspection.cs').read_text()
audited=(ROOT/'src/Core/AuditedWeaponPresentations.cs').read_text()
check('Native event read bound is exact clip/template scoped before override', 'CustomStmInspection.EventLimit(_template,nativeClip)' in custom and
      'CustomMcxBackport.EventCountFor(template)' in customcore and 'AuditedWeaponPresentations.EventLimit(template,clip)' in customcore and
      'Where(p=>p.Clip==clip)' in audited and custom.index('CustomStmInspection.EventDenial(') < custom.index('_motion.TryApply('))
check('Different native duration requires audited custom metadata', 'CustomMcxBackport.Matches(template, target.name) || AuditedWeaponPresentations.MetadataMatches(template,original.name,target.name,target.length)' in motion and
      motion.index('CustomStmInspection.ValidClip(') < motion.index('lease.Acquire(') and motion.index('CustomStmInspection.RecipientDenial(') < motion.index('lease.Acquire('))
check('Custom animation loader pins SHA256 and caches success/failure per family', '_clips.TryGetValue(key, out var cached)' in customclip and '_clips[key] = clip; _statuses[key] = Status;' in customclip and 'sha.ComputeHash(stream)' in customclip and '!= expectedHash' in customclip)
check('Custom clip identity is recipient-specific at load, playback and override', 'ValidClip(template, clips[0].name' in customclip and 'ValidClip(_template, _customStmClip.name' in custom and 'ValidClip(template, mcxDonor.name' in motion)
check('Category loader retains original compiled assets after bundle unload', 'clip = clips.Single();' in customclip and 'clip.hideFlags = HideFlags.DontUnloadUnusedAsset;' in customclip and 'bundle.Unload(false)' in customclip and 'Instantiate(' not in code_only(customclip))
check('Authored MCX selection bypasses the native marker and seek path', '_mcxNative = CustomStmInspection.NativeMcxRequested(' in pose and 'CustomStmAnimationEnabled, template, learn);' in pose and 'if (_mcxNative && !learn)' in pose)
check('Custom recipient controller and clip are checked before native override mutation',
      'CustomStmInspection.ControllerMatches(template,animator.runtimeAnimatorController.name)' in custom and
      motion.index('CustomStmInspection.RecipientDenial(') < motion.index('property.SetValue(wrapper, created);') and
      motion.index('CustomStmInspection.RecipientDenial(') < motion.index('lease.Acquire('))
check('Audited native override maps are verified and preserved before wrapper assignment', motion.index('AuditedWeaponPresentations.OverrideMatches(')<motion.index('new AnimatorOverrideController(baseController)')<motion.index('property.SetValue(wrapper, created);') and
      'created.ApplyOverrides(originalPairs)' in motion and 'originalPairs.Count==clips.Count' in motion and 'baseController is AnimatorOverrideController' in motion)
check('Variant clip lease restores the original source value through the original binding key', 'Key=keys[0].Key' in motion and 'owned.Controller[pair.Key] ?? pair.Key' in motion and
      'new KeyValuePair<AnimationClip, AnimationClip>(pair.Key, clip)' in motion and 'lease.Acquire(owned.Controller, pair.Value, donor' in motion)
check('Conditional audio stays exact with native event dispatch untouched', 'a.Conditions!=a.ConditionData.Length' in audited and
      all(s in audited for s in ['x.Parameter!=y.Parameter','x.Type!=y.Type','x.Mode!=y.Mode','x.Integer!=y.Integer','x.Float!=y.Float','x.Boolean!=y.Boolean']) and
      'ConditionData=conditionData.ToArray()' in mcx.replace(' ',''))
support=(ROOT/'src/Runtime/InspectionPose.SupportHand.cs').read_text()
handpatch=(ROOT/'src/Runtime/AuthoredSupportHandPatch.cs').read_text()
check('Support correction changes only the audited body Hand_Left return value', '__0 != Left' in handpatch and 'GetCurveValue' in handpatch and 'cc2d80b0-6d5b-4cb1-a581-6d2cc901d4c7' in handpatch and 'postfix:' in handpatch and not any(t in code_only(handpatch+support) for t in ['SetFloat(', 'SetLayerWeight(', 'SetTriggerPressed(', 'Time.timeScale', '.weight =']))
check('Support correction requires owned local first-person custom clip and neutral hands', all(t in support for t in ['ReferenceEquals(player, _context?.Player)', '!_learning', '!_customStmAwaiting', 'SameLiveAnimator()', 'OwnedOperation()', 'StartEventFired()', 'CustomStmClipMatches()', 'NeutralHands()', 'Application.isFocused', '_customStmReturn.Pending']))
check('Support gate uses the native curve getter and retains raw body diagnostics', '_supportFirstPerson = localPlayer.GetCurveValue(AuthoredSupportHandPatch.FirstPerson);' in code_only(support) and '_supportBodyFirstPerson = _supportBody!.GetFloat(AuthoredSupportHandPatch.FirstPerson);' in code_only(support) and 'AuthoredSupportHand.Apply(native, _supportFirstPerson, seconds, true)' in code_only(support) and 'rawBodyFirstPerson=' in support and 'firstPersonSource=Player.GetCurveValue' in support)
check('Support ownership clears on cleanup, callback errors retain native result', 'ClearSupportHand();' in pose[pose.index('private bool RestoreMotion()'):] and '_owner = null; Fault =' in handpatch and 'AuthoredSupportHandPatch.Release(FilterSupportHand)' in support)
check('Support weights are reported at hold and F10 without per-frame logging', 'Event("HELD " + SupportHandReport)' in pose and '"\\n" + SupportHandReport' in pose and 'Event(' not in support and 'Log' not in handpatch)
idle=(ROOT/'src/Runtime/InspectionPose.Idle.cs').read_text()
follow=(ROOT/'src/Runtime/AttachmentOverlay.WeaponFollow.cs').read_text()
check('Authored return bypasses native shortcut preference but retains exact idle/operation guards', 'if (!ShortReturnEnabled && !authoredReturn) return;' in pose and 'TryShortReturn(true);' in custom and 'SameLiveAnimator() || !OwnedOperation() || !StartEventFired() || !NeutralHands()' in pose)
check('Early close waits for native start without a new seek or replacement', 'bool early = _state.Phase == PosePhase.Playing;' in custom and 'StartEventFired() &&' in custom and '_customStmReturn.Begin(Time.unscaledTime, early)' in custom)
check('Resume readiness reuses Start visible idle check before consuming single attempt', '_idleHash = StableIdleHash(hands, animator);' in pose and '_pose.VisibleIdle(snapshot)' in resume and '!animator.IsInTransition(1)' in idle and 'GetCurrentAnimatorStateInfo(1).fullPathHash == hash' in idle)
check('Legacy cross attaches only when held and retains corners across same weapon refresh', 'current && PoseHeld' in follow and 'FollowCrossPanel(0,width,height,current,ref _crossProjection);' in crossview and '_overlay.PoseHeld = _pose.Held;' in plugin)
check('Studio uses bounded exported projection directly for rendering and hits without recapturing weapon-local corners', 'StudioCrossLayout.TryProject(group.Group,width,height,weaponPoint.x,weaponPoint.y,following,out panel.Projection);' in studio and 'group.Projection=panel.Projection;' in studio and not any(s in code_only(studio) for s in ['FollowCrossPanel(', 'AttachPanel(', 'ProjectPanel(']))
check('Cross world following reprojects real weapon transform with no scene writes', 'InverseTransformPoint(' in anchor and '_frame.TransformPoint(Unity(p))' in anchor and 'ViewportToWorldPoint' in anchor and 'panel.Project(_frame' in anchor)
check('Cross visibility spans click wait, native transaction and resumed pose', 'CrossPresentation.HiddenReason(_poseActivation.Open, _pose.Active, _pose.Held,' in plugin and '_clicks.Busy || _replacement.Pending, _probe.Session.Busy, _poseResume.Pending)' in plugin and plugin.index('UpdateCrossPresentation();') < plugin.index('_overlay?.HandlePointer('))
check('Hidden cross clears graphics, leaders and hit geometry without clearing attachments', all(s in follow for s in ['ResetPointer(); HideCrossPresentation();', '_crossRoot.gameObject.SetActive(false)', 'panel.Projection = null', 'group.Projection = null; group.Leader.Show(false);']) and 'ClearCrossAttachments(' not in follow[follow.index('private void HideCrossPresentation()'):follow.index('private void ClearCrossAttachments()')])
check('Both cross layouts and mouse capture obey the same visibility gate', crossview.index('if (CrossPresentationHidden) { HideCrossPresentation(); return; }') < crossview.index('if(UseStudioCrossLayout) { UpdateStudioCrossLayout') and 'if (CrossPresentationHidden) return null;' in crossview and 'active && !CrossPresentationHidden' in (ROOT/'src/Runtime/AttachmentOverlay.Pointer.cs').read_text())
check('Cross hide reason is exported without frame logging or gameplay writes', 'Cross visibility:' in crossview and 'Log(' not in follow and 'Event(' not in follow and not re.search(r'\.(?:Play|Rebind|Update|TryInstall|TryUninstall)\(',code_only(follow)))
check('Session bindings have no historical eight-weapon quota and prune only destroyed animators', '_bindings.Count >= 8' not in motion and '_bindings[i].Animator == null' in motion and 'SingleOrDefault(b => b.Animator == animator)' in motion)
check('All blend bindings participate in restore and fault handling', 'foreach(var lease in _extraLeases)restored=lease.TryRestore() && restored;' in motion and 'targets.Count>4' in motion and 'if (!TryRestore()) Status +=' in motion)
check('Custom hold, event read and short return use audited native state identity', 'State = _animator.GetCurrentAnimatorStateInfo(1).fullPathHash' in custom and 'CustomInspectionStateMatches()' in custom and '_customStm ? !CustomInspectionStateMatches()' in pose and '!= stateName' in mcx)
replacement=(ROOT/'src/Plugin.Replacement.cs').read_text()
replacement_state=(ROOT/'src/Core/AttachmentReplacement.cs').read_text()
check('Replacement uses the same native adapter after exact continuation consumption', replacement.index('_replacement.PrepareInstall(')<replacement.index('_probe.TryInstall(') and 'TrackPoseResume(fresh);' in replacement and 'request.Action==AttachmentAction.Replace' in click)
check('Replacement requires native success, expected revisions and a bounded idle wait', all(x in replacement_state for x in ['native.Blocked','native.Revision!=_revision+3','!native.Submitted','InstallPhase.Succeeded','_deadline=now+8','Phase=ReplacementPhase.Installing']))
check('Replacement revalidates selected source and removed item storage identity', 'Removal?.Validate(fresh)' in request and 'if(!SameBinding(next!))' in request and 'Removed attachment could not be verified in carried storage.' in request)
check('Replacement is cancelled by UI close and cannot use another hands controller', 'StopReplacement(reason);' in click and 'ReferenceEquals(_read.Get(request.Player,"HandsController"),_replacementHands)' in replacement and 'PointerLocalOwnership()' in replacement)
check('Replacement waits below the held pose until final install and blocks parallel input', plugin.index('PollReplacement(interruptPose);')<plugin.index('PollPoseResume(interruptPose);') and 'if (_clicks.Busy || _replacement.Pending)' in plugin and '_replacement.Pending || _probe.Session.Busy' in click and '_pose.VisibleIdle(_state.Snapshot)' in replacement)
check('Occupied mouse choices explicitly dispatch replacement across all layouts', 'target.Action' in mouse and 'AttachmentRequest.ChoiceAction(slot.Slot)' in mouse and 'AttachmentRequest.ChoiceAction(vm!.Slot)' in crossview and 'AttachmentRequest.ChoiceAction(vm!.Slot)' in hexview)
check('Child bindings checked before simulation, after rollback and completion', 'chosen.Assembly?.Same(_assembly)' in probe and '_assembly?.Same(new AttachmentAssemblyReader(_read).Capture(_item,_assemblyController))' in probe and '_hasChildren==now._hasChildren' in request)
check('Alt R cycles exact hovered/open mount without a position menu', 'new KeyboardShortcut(KeyCode.R,KeyCode.LeftAlt)' in plugin and 'CyclePointerPosition()' in mouse and 'MenuButton("Positions"' not in mouse and 'panel.HeaderTarget=null;' in crossview)
check('Small thumbnail picker hides unproven choices', 'c.Evidence==CandidateEvidence.NativeFilterPass' in mouse and 'QuickSwapMenu.Cell(i)' in mouse and 'Width=224' in (ROOT/'src/Core/QuickSwapMenu.cs').read_text())
settings=(ROOT/'src/Runtime/SettingsPresentation.cs').read_text()
check('F12 metadata keeps stored keys and slider ranges intact', 'Config.Bind(section, key, value, SettingsPresentation.Describe' in (ROOT/'src/Plugin.Settings.cs').read_text() and 'original.AcceptableValues' in settings and 'public bool? Browsable = false;' in settings)
check('F12 active settings have readable labels without legacy trial categories', 'Allow attachment changes' in settings and 'Attachment menu key' in settings and 'Menu activation (Hold / Toggle)' in settings and 'case "Experiments/MdrMotionTrial"' not in settings)
check('Custom animation toggle is absent from F12', 'case "Experiments/CustomStmAnimation"' not in settings and 'Use authored weapon poses' not in settings)
report_setting=settings[settings.index('case "Controls/ExportReport":'):settings.index('// Keep all stored keys')]
check('Diagnostic report is available only as an advanced F12 setting', 'Save diagnostic report' in report_setting and 'tag.IsAdvanced = true;' in report_setting)
standalone=(ROOT/'src/Runtime/InspectionPose.Standalone.cs').read_text()
playback=(ROOT/'src/Runtime/StandalonePosePlayback.cs').read_text()
timeline=(ROOT/'src/Core/StandalonePoseTimeline.cs').read_text()
standalone_start=standalone[standalone.index('public void Start('):standalone.index('private string StandaloneDenial()')]
all_pose_code=code_only('\n'.join(p.read_text() for p in (ROOT/'src/Runtime').glob('InspectionPose*.cs')))
all_runtime_code=code_only('\n'.join(p.read_text() for p in (ROOT/'src/Runtime').glob('*.cs')))
check('Public attachment Start uses only the standalone sampler and independent clock',
      'public void Start(' in standalone and '_standalonePlayback.Prepare(animator, clip, template);' in standalone_start and
      '_standaloneClock.Start(Time.unscaledTime);' in standalone_start and not any(t in code_only(standalone_start) for t in
      ['examine.Invoke(', 'ExamineWeapon(', 'PrepareCustomStm(', 'PrepareSupportHand(', 'PrepareGlobalMcx(', 'PrepareStm(', 'StartLegacy(', '_motion.TryApply(', '_speed.Acquire(']))
check('Legacy native presentation entry points have no callers', all(len(re.findall(r'\b'+name+r'\s*\(',all_pose_code))==1
      for name in ['StartLegacy','TickLegacy','MarkLegacy','ReleaseLegacy','LegacyReport','ApplyLegacyPresentationFrame','RestoreLegacyPresentationFrame']))
check('Standalone attachment route never initializes the legacy support-hand callback',
      'AuthoredSupportHandPatch.Initialize(' not in all_runtime_code and 'PrepareSupportHand(' not in code_only(standalone))
check('Animation sampling targets only a private shadow hierarchy with its own Animator',
      len(re.findall(r'\.SampleAnimation\s*\(',all_runtime_code))==1 and '_clip.SampleAnimation(_shadow, seconds);' in code_only(playback) and
      '_shadow = new GameObject(' in code_only(playback) and 'HideFlags.HideAndDontSave' in playback and 'Object.Instantiate(' not in code_only(playback) and
      '_shadowAnimator = _shadow.AddComponent<Animator>();' in code_only(playback) and
      '_shadowAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;' in code_only(playback) and
      '_shadowAnimator.applyRootMotion = false;' in code_only(playback) and
      len(re.findall(r'\.AddComponent<Animator>\s*\(',code_only(playback)))==1)
check('Standalone runtime cannot invoke native inspection, controller replacement, animator playback or speed writes',
      not any(t in code_only(standalone+playback) for t in ['ExamineWeapon(', 'SetTriggerPressed(', 'CrossFadeInFixedTime(', '.Play(', '.Rebind(', '_motion.TryApply(', '_speed.Acquire(']) and
      not re.search(r'\.(?:runtimeAnimatorController|speed|avatar)\s*=(?!=)',code_only(standalone+playback)) and
      not re.search(r'\b(?:_animator|animator)\.(?:\w+\s*=(?!=)|(?:Play|Rebind|Update|Set\w+)\s*\()',code_only(standalone+playback)))
check('Standalone lifecycle preserves native idle and context validation before sampling',
      'StableIdleHash(hands, animator) == 0' in standalone_start and '_probe.ReadHands(snapshot).Denial' in standalone_start and
      'HandsReadiness.IdleOperation' in standalone and 'SameLiveAnimator()' in standalone and 'NeutralHands()' in standalone)
check('Mapped gameplay input restores presentation only after pointer suppression permits it',
      pointer.index('PointerInputPolicy.Block(name, Capture.Active, Capture.SuppressMouse)') < pointer.index('StandalonePoseInputPolicy.ShouldInterrupt(name)') <
      pointer.index('BeforeGameplayCommand?.Invoke(name)') < pointer.index('return ETranslateResult.Ignore;') and
      'return ETranslateResult.Block;' in pointer and '_pointer.BeforeGameplayCommand' in plugin)
check('Standalone command policy preserves ADS release and F10 while cancelling mapped ADS press',
      '"ToggleAlternativeShooting"' in timeline and '"EndAlternativeShooting"' not in timeline and '"F10"' not in timeline and
      'StringComparer.Ordinal' in timeline)
check('Standalone pose remains active through restoration and uses its own held/return state',
      '_standaloneClock.Active' in pose and '_standalonePlayback.FrameActive' in pose and '_standaloneClock.Held' in pose and
      '_standaloneClock.Returning' in stm and '_standaloneClock.Abort();' in standalone and '_standalonePlayback.Restore();' in standalone)
check('Legacy key is unbound while inactive native calibration remains documented',
      '"AttachmentPose", new KeyboardShortcut(KeyCode.None)' in plugin and 'Unbound by default.' in plugin and
      'F4 pose marking and saved native inspection markers are inactive' in plugin)
check('Standalone UI ownership clears on close, covers an already-open overlay and F7 uses normal return',
      '_poseOwnsOverlay = false;' in close and
      re.search(r'if\s*\(!_visible\)\s*Open\(\);\s*_poseOwnsOverlay = true;\s*_pose.Start\(',code_only(plugin)) and
      '_pose.Release("F7 confirmation: return standalone pose first", true);' in plugin)
support_visual=(ROOT/'src/Runtime/SupportHandPresentation.cs').read_text()
support_binding=(ROOT/'src/Runtime/InspectionPose.SupportPresentation.cs').read_text()
support_visual_code=code_only(support_visual)
check('Support presentation defaults to left-arm slots and maps opt-in arm paths uniquely',
      '=> Prepare(renderers, leftRoot, rightRoot, authoredLeftRoot, null);' in support_visual and
      'if (!support && (authoredRightRoot == null || (body != rightRoot && !body.IsChildOf(rightRoot)))) continue;' in support_visual and
      'Transform source = FindUnique(sourceRoot, path);' in support_visual and
      'FindUnique(bodyRoot, path) != cursor' in support_visual and 'source.IsChildOf(rightRoot)' in support_visual and
      'presented[binding.Slots[i]] = binding.Bones[i].Proxy;' in support_visual)
check('Support presentation moves private bones while actual body/source transforms and mesh data remain untouched',
      'renderer.bones = bones' in support_visual_code and 'bone.Proxy.SetPositionAndRotation(' in support_visual_code and
      'bone.Proxy.localScale = bone.Body.localScale;' in support_visual_code and
      not re.search(r'\b(?:bone\.(?:Body|Source)|_leftRoot|_rightRoot|_sourceRoot|_rightSourceRoot)\.(?:\w+\s*=(?!=)|(?:Set\w+|Translate|Rotate)\s*\()',support_visual_code) and
      not any(t in support_visual_code for t in ['.vertices', '.boneWeights', '.isReadable', '.BakeMesh(', '.SetFloat(', '.SetLayerWeight(', '.SetIK']) and
      not re.search(r'\.(?:enabled|sharedMesh|rootBone|material|materials|sharedMaterials)\s*=(?!=)',support_visual_code))
check('Support presentation validates all bindings before live writes and records ownership before a failing setter',
      support_visual.index('if (!RendererMatches(binding) || !Same(') < support_visual.index('_writeBones(binding.Renderer, presented);') and
      support_visual.index('binding.Touched = true;') < support_visual.index('_writeBones(binding.Renderer, presented);') and
      'if (!Same(_readBones(binding.Renderer), presented))' in support_visual and 'if (!Finite(blend) || blend < 0 || blend > 1)' in support_visual)
check('Support restoration preserves external slots and retains referenced proxies before cleanup',
      'if (proxy != null && current[slot] == proxy)' in support_visual and 'current[slot] = binding.Original[slot];' in support_visual and
      'binding.Touched = ContainsProxy(restored);' in support_visual and 'if (!Restore()) return false;' in support_visual and
      support_visual.index('if (!Restore()) return false;') < support_visual.index('UnityEngine.Object.Destroy(bone.Proxy.gameObject);') and
      'if (_fault.Length == 0) _fault = reason;' in support_visual)
check('Support binding checks local body, hands skeleton, palm and exact clothing LOD identities',
      all(t in support_binding for t in ['"PlayerBody", "_playerBody"', '"SkeletonHands"', '"Hands"', '"LeftPalm"',
      '"_lods"', '"_skinnedMeshRenderer"', '"_skeleton"', 'ReferenceEquals(', 'IsChildOf(', 'SupportPresentationDenial()']) and
      '_supportPlayerBody' in support_binding and 'PrepareSupportPresentation(snapshot, animator);' in standalone_start)
check('Support rendering follows the authored weapon frame and restores before weapon transform cleanup',
      standalone.index('_standalonePlayback.Apply(') < standalone.index('_supportPresentation.Apply(') and
      standalone.index('_supportPresentation.Restore();') < standalone.index('_standalonePlayback.Restore();') and
      standalone.index('_supportPresentation.Release();') < standalone.index('_standalonePlayback.Release();') and
      '_supportPresentation.FrameActive' in pose and '_supportPresentation.Prepared' in pose and
      'if (!supportReleased || !playbackReleased)' in standalone and 'ClearSupportPresentationBinding();' in standalone and
      '_supportPresentation.Fault.Length != 0' in standalone)
firing_policy=(ROOT/'src/Core/FiringHandPresentation.cs').read_text()
flare_ids={'624c0b3340357b5f566e8766','62178be9d0050232da3485d9','66d98233302686954b0c6f81',
           '675ea3d6312c0a5c4e04e317','6217726288ed9f0845317459','62178c4d4ecf221597654e3d',
           '66d9f1abb16d9aacf5068468'}
check('Firing-arm opt-in is an exact handgun and new RShG-2/flare policy; previous long guns retain the four-argument route',
      'new HashSet<string>(StringComparer.Ordinal)' in firing_policy and
      'RequiresFiringHand(string template) => Templates.Contains(template);' in firing_policy and
      '6a15ae2ae5267ba21c07f98f' not in firing_policy and '5df24cf80dee1b22f862e9bc' not in firing_policy and
      '620109578d82e67e7911abf2' in firing_policy and '676bf44c5539167c3603e869' in firing_policy and
      len(set(re.findall(r'"([a-f0-9]{24})"',firing_policy)))==40 and
      flare_ids.issubset(set(re.findall(r'"([a-f0-9]{24})"',firing_policy))) and
      re.search(r'_authoredFiringHand = FiringHandPresentation.RequiresFiringHand\(_template\);\s*if \(_authoredFiringHand\)',support_binding) and
      '"RightPalm"), rightPalm)' in support_binding and
      '_supportPresentation.Prepare(_supportRenderers, _supportLeft, _supportRight, authored, authoredRight);' in support_binding and
      'else _supportPresentation.Prepare(_supportRenderers, _supportLeft, _supportRight, authored);' in support_binding)
check('Optional firing-arm presentation shares one renderer ownership batch and retains both private roots through restoration',
      'BodyRoot = bodyRoot, SourceRoot = sourceRoot' in support_visual and
      '(bone.Body != bone.BodyRoot && !bone.Body.IsChildOf(bone.BodyRoot))' in support_visual and
      '(bone.Source != bone.SourceRoot && !bone.Source.IsChildOf(bone.SourceRoot))' in support_visual and
      support_visual.count('_writeBones(binding.Renderer, presented);')==1 and
      support_visual.count('if (changed) _writeBones(binding.Renderer, current);')==1 and
      support_visual.index('if (!Restore()) return false;') < support_visual.index('UnityEngine.Object.Destroy(_rightProxyRoot);') and
      '_rightSourceRoot.parent == _rightSourceParent && _rightProxyRoot.transform.parent == _rightParent' in support_visual)
baseline=json.loads((ROOT/'tools/animation-baseline-0.25.1.json').read_text())
baseline_pins=baseline['authoredAnimationHashes']
check('All 123 original animation bundle bytes preserve the confirmed rifle/sniper baseline',
      baseline['schema']==1 and baseline['baselineVersion']=='0.25.1' and len(baseline_pins)==123 and
      all(re.fullmatch(r'Animation/[a-z0-9_]+_presentation\.bundle', rel) and
          hashlib.sha256((ROOT/'assets'/rel).read_bytes()).hexdigest().upper()==pin
          for rel,pin in baseline_pins.items()))
additional=json.loads((ROOT/'assets/Animation/additional-presentations.json').read_text())
additional_code=(ROOT/'src/Core/AdditionalWeaponPresentations.cs').read_text()
alias_code=(ROOT/'src/Core/InstalledWeaponAliases.cs').read_text()
additional_ids={'6963f79ab66d6c6601a8b2d7','6868377c7bb1c07772467ee7','68cd937be06f1a3a720d6959',
                '620109578d82e67e7911abf2','676bf44c5539167c3603e869','624c0b3340357b5f566e8766'}
check('Six additional exact-template authored assets match the catalog and pinned bundle bytes',
      additional['schema']==1 and additional['presentation']=='MCX-angle/STM-withdrawn-arm-v1' and
      len(additional['recipients'])==6 and {r['template'] for r in additional['recipients']}==additional_ids and
      {m[0] for m in re.findall(r'new WeaponPresentationEntry\("([a-f0-9]{24})","([^"]+)","([A-F0-9]{64})"',additional_code)}==additional_ids and
      all(('new WeaponPresentationEntry("'+r['template']+'","'+r['bundle']+'","'+r['sha256']+'"') in additional_code and
          re.fullmatch(r'gun_[a-f0-9]{24}_presentation',r['bundle']) and
          hashlib.sha256((ROOT/'assets/Animation'/(r['bundle']+'.bundle')).read_bytes()).hexdigest().upper()==r['sha256']
          for r in additional['recipients']))
check('Twenty-six installed aliases are exact mappings with recorded prefab hashes and unchanged unknown fallback',
      len(additional['aliases'])==26 and len({r['template'] for r in additional['aliases']})==26 and
      dict(re.findall(r'case "([a-f0-9]{24})": return "([a-f0-9]{24})";',alias_code))==
          {r['template']:r['donor'] for r in additional['aliases']} and
      all(r['prefabFiles'] and
          all(re.fullmatch(r'[A-F0-9]{64}',f['sha256']) for f in r['prefabFiles']) for r in additional['aliases']) and
      'default: return template;' in alias_code and 'AdditionalWeaponPresentations.Entries.FirstOrDefault' in audited and
      'InstalledWeaponAliases.Canonical(template)' in customcore)
historical_aliases=[r for r in additional['aliases'] if r['verification']=='historical-prefab-hash']
pktm_aliases=[r for r in additional['aliases'] if r['verification']=='exact-current-prefab-and-factory']
flare_aliases=[r for r in additional['aliases'] if r['verification']=='exact-native-rig-controller-clips']
check('Alias proof types distinguish historical matches from current factory and flare rig evidence',
      len(historical_aliases)==19 and all(r['historicalNativeHashMatch'] is True for r in historical_aliases) and
      len(pktm_aliases)==1 and pktm_aliases[0]['template']=='657857faeff4c850222dff1b' and
      pktm_aliases[0]['donor']=='64637076203536ad5600c990' and 'MachineGun' in pktm_aliases[0]['nativeFactory'] and
      pktm_aliases[0]['historicalNativeHashMatch'] is False and
      {f['historicalNativeHashMatch'] for f in pktm_aliases[0]['prefabFiles']}=={True,False} and
      len(flare_aliases)==6 and {r['template'] for r in flare_aliases}==flare_ids-{'624c0b3340357b5f566e8766'} and
      all(r['donor']=='624c0b3340357b5f566e8766' and r['historicalNativeHashMatch'] is False and
          r['requiredPaths']==55 and re.fullmatch(r'[A-F0-9]{64}',r['requiredRigSha256']) and
          re.fullmatch(r'[A-F0-9]{64}',r['controllerSerializedSha256']) and
          {c['state'] for c in r['idleLookClipHashes']}=={'Hands.IDLE','Hands.LOOK'} and
          all(re.fullmatch(r'[A-F0-9]{64}',c['serializedSha256']) for c in r['idleLookClipHashes'])
          for r in flare_aliases))
build_source=(ROOT/'tools/Build.ps1').read_text()
check('Build includes all six added bundles and preserves the 123 original hashes before packaging',
      "assets/Animation/additional-presentations.json" in build_source and
      'foreach ($entry in $AdditionalManifest.recipients)' in build_source and '$AnimationHashes.Count -ne 129' in build_source and
      'Original animation baseline changed:' in build_source and
      build_source.index('Original animation baseline changed:') < build_source.index("$PackageDir = Join-Path $Out") and
      'authoredTemplateCount = 194; additionalAliasCount = 26; original123AnimationHashesPreserved = $true;' in build_source)
result={'version'  :'1.0.0','checkedUtc':datetime.now(timezone.utc).isoformat(),'kind':'static structural checks only','checks':checks,
        'notes':'Static checks only. Actual C# build/test results are recorded separately in dist/build.log and docs/PROGRESS_1.0.0.md.'}
(ROOT/'STRUCTURAL_CHECKS.json').write_text(json.dumps(result,indent=2)+'\n')
print('Structural checks passed. This script does not establish C# compilation, test execution or Unity/SPT behavior.')
