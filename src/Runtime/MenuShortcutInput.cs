using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Tylevo.FieldAttachments.Core;
using UnityEngine;

namespace Tylevo.FieldAttachments.Runtime
{
    // Reads physical input through injected predicates. It never invokes gameplay
    // commands, and keeps an acquired mouse press even if the setting changes.
    public sealed class MenuShortcutInput
    {
        private readonly AltMenuActivation _activation = new AltMenuActivation();
        private readonly HashSet<KeyCode> _releaseKeys = new HashSet<KeyCode>();
        private readonly HashSet<KeyCode> _ownedMouseKeys = new HashSet<KeyCode>();
        private KeyboardShortcut _binding;
        private bool _hasBinding;
        private int _frame = -2, _mouseReleaseFrame = -1;

        public bool Open => _activation.Open;
        public bool BindingChanged { get; private set; }
        public bool PhysicalMainHeld { get; private set; }
        public bool OwnedMouseHeld => _ownedMouseKeys.Count != 0;
        public bool SuppressLookTail => !Open && (OwnedMouseHeld || _mouseReleaseFrame == _frame);

        public bool Sample(KeyboardShortcut binding, MenuActivationMode mode, bool eligible, bool focused,
            bool close, bool chordUsed, int frame, Func<KeyCode, bool> held, Func<KeyCode, bool> down)
        {
            if (held == null) throw new ArgumentNullException(nameof(held));
            if (down == null) throw new ArgumentNullException(nameof(down));
            _frame = frame;
            BindingChanged = _hasBinding && !SameBinding(_binding, binding);
            if (BindingChanged)
            {
                if (_binding.MainKey != KeyCode.None && held(_binding.MainKey)) _releaseKeys.Add(_binding.MainKey);
                _activation.Cancel();
            }
            if (!_hasBinding || BindingChanged)
            {
                _binding = new KeyboardShortcut(binding.MainKey, Modifiers(binding).ToArray());
                _hasBinding = true;
            }
            // Loss of focus can make Unity report every key up without a physical
            // release. Keep ownership until a focused sample confirms the release.
            if (focused)
            {
                _releaseKeys.RemoveWhere(key => !held(key));
                if (_ownedMouseKeys.RemoveWhere(key => !held(key)) != 0) _mouseReleaseFrame = frame;
            }
            PhysicalMainHeld = (binding.MainKey != KeyCode.None && held(binding.MainKey)) || _releaseKeys.Count != 0;

            bool supported = MenuShortcutSetting.IsSupported(binding) && binding.MainKey != KeyCode.None;
            bool chordHeld = supported && KeyChord.IsHeld(binding.MainKey, Modifiers(binding), held);
            bool pressed = chordHeld && down(binding.MainKey);
            bool modifierOnly = MenuShortcutSetting.IsModifier(binding.MainKey) && !Modifiers(binding).Any();
            bool wasOpen = Open;
            bool open = _activation.Sample(chordHeld, pressed, mode, eligible && supported, focused,
                close, chordUsed, frame, PhysicalMainHeld, modifierOnly);

            // Own both the opening press and an explicit closing press. A denied
            // first press never acquires a gameplay button merely because it is held.
            if (chordHeld && (open || wasOpen && pressed && eligible && focused && !close))
            {
                _releaseKeys.Add(binding.MainKey);
                if (MenuShortcutSetting.IsMouse(binding.MainKey)) _ownedMouseKeys.Add(binding.MainKey);
            }
            return open;
        }

        public bool OwnMouseShortcut(KeyboardShortcut shortcut, bool eligible, bool focused,
            Func<KeyCode, bool> held, Func<KeyCode, bool> down)
        {
            if (held == null)
            {
                throw new ArgumentNullException(nameof(held));
            }
            if (down == null)
            {
                throw new ArgumentNullException(nameof(down));
            }
            if (!eligible || !focused || !MenuShortcutSetting.IsMouse(shortcut.MainKey) ||
                !MenuShortcutSetting.IsSupported(shortcut) ||
                MenuShortcutSetting.ClaimsKey(_binding, shortcut.MainKey, held) ||
                !KeyChord.IsDown(shortcut.MainKey, Modifiers(shortcut), held, down))
            {
                return false;
            }

            // Layer shortcuts share the menu's mouse-release drain even when the
            // menu closes or either binding changes before the button is released.
            _releaseKeys.Add(shortcut.MainKey);
            PhysicalMainHeld = true;
            return _ownedMouseKeys.Add(shortcut.MainKey);
        }

        private static bool SameBinding(KeyboardShortcut a, KeyboardShortcut b) =>
            a.MainKey == b.MainKey && Modifiers(a).SequenceEqual(Modifiers(b));

        private static IEnumerable<KeyCode> Modifiers(KeyboardShortcut binding) =>
            binding.Modifiers ?? Enumerable.Empty<KeyCode>();

        public void Cancel() => _activation.Cancel();
    }
}
