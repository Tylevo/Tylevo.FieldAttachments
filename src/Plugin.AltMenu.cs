using BepInEx.Configuration;
using Tylevo.FieldAttachments.Core;
using Tylevo.FieldAttachments.Runtime;
using UnityEngine;

namespace Tylevo.FieldAttachments
{
    public enum AttachmentMenuKey
    {
        LeftAlt = (int)KeyCode.LeftAlt,
        RightAlt = (int)KeyCode.RightAlt,
        None = (int)KeyCode.None
    }

    public sealed partial class Plugin
    {
        private ConfigEntry<AttachmentMenuKey> _menuKey = null!;
        private ConfigEntry<MenuActivationMode> _menuActivationMode = null!;
        private readonly AltMenuActivation _altMenu = new AltMenuActivation();
        private readonly PoseActivation _legacyPoseActivation = new PoseActivation();
        private AttachmentMenuKey? _lastMenuKey;
        private bool _altPoseRequested;
        private KeyCode MenuKeyCode => _menuKey.Value == AttachmentMenuKey.LeftAlt ? KeyCode.LeftAlt :
            _menuKey.Value == AttachmentMenuKey.RightAlt ? KeyCode.RightAlt : KeyCode.None;

        private void BindAltMenu()
        {
            _menuKey = BindSetting("Controls", "MenuKey", AttachmentMenuKey.LeftAlt,
                "Open the attachment presentation and cursor together with Left Alt (default) or Right Alt. None disables this shortcut.");
            _menuActivationMode = BindSetting("Controls", "MenuActivation", MenuActivationMode.Hold,
                "Hold keeps the menu open while Alt is held. Toggle opens on Alt-down; a later plain Alt tap closes on release. Alt+R or a pointer click while Alt is held keeps Toggle open. Right mouse closes LIST only. Closing cancels waiting requests, not submitted native operations.");
        }

        // Called before native input translation. This only updates input intent;
        // inventory capture and standalone pose entry remain in Plugin.Update.
        private bool AltCaptureRequested(bool own)
        {
            if (_lastMenuKey.HasValue && _lastMenuKey.Value != _menuKey.Value) _altMenu.Cancel();
            _lastMenuKey = _menuKey.Value;
            bool held = MenuKeyCode != KeyCode.None && Input.GetKey(MenuKeyCode);
            bool down = MenuKeyCode != KeyCode.None && Input.GetKeyDown(MenuKeyCode);
            bool identity = ReferenceEquals(_reader.HeldWeapon(_pointerPlayer), _pointerWeapon) &&
                ReferenceEquals(_read.Get(_pointerPlayer, "HandsController"), _pointerHands);
            bool allowed = _enabled.Value && own && identity && MenuKeyCode != KeyCode.None && !_probe.Session.Blocked &&
                (_altPoseRequested || !_pose.PresentationReturning) &&
                !_pose.Status.StartsWith("POSE FAULT", System.StringComparison.Ordinal);
            // An existing O session keeps its original Alt cursor-only behavior.
            if (_legacyPoseActivation.Open && !_altPoseRequested) allowed = false;
            bool close = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.F12);
            bool chordUsed = Held(_cyclePosition.Value) || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
            return _altMenu.Sample(held, down, _menuActivationMode.Value, allowed, Application.isFocused, close, chordUsed, Time.frameCount);
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
            // Keep a local input observer registered before the opening Alt frame.
            EnsurePointer(context);
        }

        private void ApplyMenuControlHint()
        {
            if (_overlay == null) return;
            string key = _menuKey.Value == AttachmentMenuKey.LeftAlt ? "LEFT ALT" :
                _menuKey.Value == AttachmentMenuKey.RightAlt ? "RIGHT ALT" : "MENU DISABLED";
            _overlay.MenuControlHint = key + (_menuActivationMode.Value == MenuActivationMode.Hold ? " HOLD MENU" : " TOGGLE MENU") +
                " | LMB SELECT | RMB CLOSE LIST | ALT+R MOUNT";
        }

        private string AltCloseReason()
        {
            if (!_enabled.Value || !Application.isFocused) return "plugin disabled or focus lost";
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.F12)) return "native menu opened";
            if (!PointerLocalOwnership()) return "menu ownership changed";
            return "Alt menu closed";
        }
    }
}
