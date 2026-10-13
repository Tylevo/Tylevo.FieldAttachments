using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Tylevo.FieldAttachments.Core;
using UnityEngine;

namespace Tylevo.FieldAttachments.Runtime
{
    public static class MenuShortcutSetting
    {
        public const string Help = "Open the attachment menu and control its cursor. Default: Left Alt. Choose an unused keyboard key, middle mouse or a side button. To set a combination, hold its modifiers, press and release the main key or button, then release the modifiers. Left/right click, Escape, Tab and F12 are reserved for menu controls. Clear disables the shortcut.";

        public static ConfigEntry<KeyboardShortcut> Bind(ConfigFile config) => config.Bind("Controls", "MenuKey",
            new KeyboardShortcut(KeyCode.LeftAlt), SettingsPresentation.Describe("Controls", "MenuKey",
                new ConfigDescription(Help, new MenuShortcutValues())));

        public static bool IsModifier(KeyCode key) => key==KeyCode.LeftAlt || key==KeyCode.RightAlt ||
            key==KeyCode.LeftControl || key==KeyCode.RightControl || key==KeyCode.LeftShift || key==KeyCode.RightShift ||
            key==KeyCode.LeftCommand || key==KeyCode.RightCommand || key==KeyCode.LeftWindows || key==KeyCode.RightWindows || key==KeyCode.AltGr;

        public static bool IsMouse(KeyCode key) => key>=KeyCode.Mouse2 && key<=KeyCode.Mouse6;

        public static bool ClaimsKey(KeyboardShortcut shortcut, KeyCode key, Func<KeyCode,bool> held) =>
            key!=KeyCode.None && shortcut.MainKey==key && IsSupported(shortcut) &&
            KeyChord.IsHeld(shortcut.MainKey,shortcut.Modifiers,held);

        public static bool IsSupported(KeyboardShortcut shortcut)
        {
            var modifiers=shortcut.Modifiers ?? Enumerable.Empty<KeyCode>();
            KeyCode key=shortcut.MainKey;
            if(key==KeyCode.None) return !modifiers.Any();
            if(!Enum.IsDefined(typeof(KeyCode),key) || key<KeyCode.Backspace || key>KeyCode.Mouse6 ||
                key==KeyCode.Mouse0 || key==KeyCode.Mouse1 || key==KeyCode.Escape || key==KeyCode.Tab || key==KeyCode.F12) return false;
            return modifiers.All(k=>IsModifier(k) && k!=key);
        }

        public static string Format(KeyboardShortcut shortcut)
        {
            if(shortcut.MainKey==KeyCode.None) return "";
            var keys=new List<string>();
            if(shortcut.Modifiers!=null) foreach(var key in shortcut.Modifiers) keys.Add(KeyLabel(key));
            keys.Add(KeyLabel(shortcut.MainKey));
            return string.Join(" + ",keys);
        }

        private static string KeyLabel(KeyCode key)
        {
            if(key==KeyCode.Mouse0) return "LMB";
            if(key==KeyCode.Mouse1) return "RMB";
            if(key==KeyCode.Mouse2) return "MMB";
            if(IsMouse(key)) return "MOUSE "+((int)key-(int)KeyCode.Mouse0+1);
            if(key>=KeyCode.Alpha0 && key<=KeyCode.Alpha9) return ((int)key-(int)KeyCode.Alpha0).ToString();
            string name=key.ToString().Replace("Control","Ctrl").Replace("Command","Cmd");
            return System.Text.RegularExpressions.Regex.Replace(name,"([a-z])([A-Z0-9])","$1 $2").ToUpperInvariant();
        }

        private sealed class MenuShortcutValues : AcceptableValueBase
        {
            public MenuShortcutValues() : base(typeof(KeyboardShortcut)) { }
            public override bool IsValid(object value) => value is KeyboardShortcut shortcut && IsSupported(shortcut);
            public override object Clamp(object value) => IsValid(value) ? value : new KeyboardShortcut(KeyCode.None);
            public override string ToDescriptionString() => "# Use a keyboard key or Mouse2-Mouse6, with optional modifiers. Mouse0, Mouse1, Escape, Tab and F12 are reserved. Unsupported bindings are disabled.";
        }
    }
}
