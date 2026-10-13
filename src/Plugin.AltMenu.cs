using BepInEx.Configuration;
using System.Collections.Generic;
using Tylevo.FieldAttachments.Core;
using Tylevo.FieldAttachments.Runtime;
using UnityEngine;

namespace Tylevo.FieldAttachments
{
    public sealed partial class Plugin
    {
        private ConfigEntry<KeyboardShortcut> _menuKey = null!;
        private ConfigEntry<MenuActivationMode> _menuActivationMode = null!;
        private readonly MenuShortcutInput _altMenu = new MenuShortcutInput();
        private readonly PoseActivation _legacyPoseActivation = new PoseActivation();
        private bool _altPoseRequested;
        private KeyCode MenuKeyCode => _menuKey.Value.MainKey;

        private void BindAltMenu()
        {
            _menuKey = MenuShortcutSetting.Bind(Config);
            _menuActivationMode = BindSetting("Controls", "MenuActivation", MenuActivationMode.Hold,
                "Hold keeps the menu open while the shortcut is held. Toggle opens on one press and closes on the next. When using a modifier key on its own, tap it without another control to close.");
        }

        // Called before native input translation. This only updates input intent;
        // inventory capture and standalone pose entry remain in Plugin.Update.
        private bool AltCaptureRequested(bool own)
        {
            bool identity = ReferenceEquals(_reader.HeldWeapon(_pointerPlayer), _pointerWeapon) &&
                ReferenceEquals(_read.Get(_pointerPlayer, "HandsController"), _pointerHands);
            bool allowed = _enabled.Value && own && identity && MenuKeyCode != KeyCode.None && !_probe.Session.Blocked &&
                (_altPoseRequested || !_pose.PresentationReturning) &&
                !_pose.Status.StartsWith("POSE FAULT", System.StringComparison.Ordinal);
            // An existing O session keeps its original Alt cursor-only behavior.
            if (_legacyPoseActivation.Open && !_altPoseRequested) allowed = false;
            bool close = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.F12);
            bool chordUsed = Held(_cyclePosition.Value) || Held(_cycleLayer.Value) || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
            bool wasOpen = _altMenu.Open;
            bool requested=_altMenu.Sample(_menuKey.Value,_menuActivationMode.Value,allowed,Application.isFocused,
                close,chordUsed,Time.frameCount,Input.GetKey,Input.GetKeyDown);
            _altMenu.OwnMouseShortcut(_cycleLayer.Value, allowed && (requested || wasOpen), Application.isFocused,
                Input.GetKey, Input.GetKeyDown);
            if(_altMenu.BindingChanged) { _pointer?.Capture.Cancel(); _overlay?.ResetPointer(); }
            return requested;
        }

        private void MaintainAltInput()
        {
            if (_visible) return;
            if (!_enabled.Value || !Application.isFocused)
            { _altMenu.Cancel(); _pointer?.Refresh(); return; }
            object? player = _reader.MainPlayer();
            var context = new RaidSnapshot { Player = player, Weapon = _reader.HeldWeapon(player),
                Controller = _read.Get(player, "InventoryController") };
            if (!_probe.ReadContext(context).Allowed)
            {
                _altMenu.Cancel(); _pointer?.Refresh();
                if (_pointer?.Capture.SuppressMouse != true) DetachPointer();
                return;
            }
            // Keep a local input observer registered before the opening shortcut frame.
            EnsurePointer(context);
        }

        private void ApplyMenuControlHint()
        {
            if (_overlay == null) return;
            string key = MenuShortcutSetting.Format(_menuKey.Value);
            var hints = new List<ControlHint> {
                new ControlHint(key, key.Length == 0 ? "Menu key disabled" :
                    _menuActivationMode.Value == MenuActivationMode.Hold ? "Hold for menu" : "Toggle menu"),
                new ControlHint("LMB", "Select"),
                new ControlHint("RMB", "Close list"),
                new ControlHint("WHEEL", "Browse parts")
            };
            if (_cyclePosition.Value.MainKey != KeyCode.None)
                hints.Add(new ControlHint(MenuShortcutSetting.Format(_cyclePosition.Value), "Mounting point"));
            if (_cycleLayer.Value.MainKey != KeyCode.None)
                hints.Add(new ControlHint(MenuShortcutSetting.Format(_cycleLayer.Value), "Switch layer"));
            _overlay.SetMenuControlHints(hints);
        }

        private void CloseAltMenu()
        {
            if (!_enabled.Value || !Application.isFocused) Close(PoseReleaseIntent.Abort, "plugin disabled or focus lost");
            else if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.F12))
                Close(PoseReleaseIntent.Abort, "native menu opened");
            else if (!PointerLocalOwnership()) Close(PoseReleaseIntent.Abort, "menu ownership changed");
            else Close(PoseReleaseIntent.MenuClose, "attachment menu closed");
        }
    }
}
