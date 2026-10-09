using System;
using System.Collections.Generic;

namespace Tylevo.FieldAttachments.Core
{
    // Inclusive chord matching: only the configured keys are required.
    // Other held keys do not cancel a chord. This is intentional: F8 is a held
    // overlay activator while arrows, F9 and F10 remain usable at the same time.
    // The default key value represents an unbound key (Unity KeyCode.None = 0).
    // Injected key readers keep this policy testable without Unity/BepInEx.
    public static class KeyChord
    {
        public static bool IsHeld<TKey>(TKey mainKey, IEnumerable<TKey>? modifiers,
            Func<TKey, bool> isHeld) where TKey : struct
        {
            if (isHeld == null) throw new ArgumentNullException(nameof(isHeld));
            if (EqualityComparer<TKey>.Default.Equals(mainKey, default) || !isHeld(mainKey)) return false;
            if (modifiers != null)
                foreach (TKey modifier in modifiers)
                    if (!isHeld(modifier)) return false;
            return true;
        }

        public static bool IsDown<TKey>(TKey mainKey, IEnumerable<TKey>? modifiers,
            Func<TKey, bool> isHeld, Func<TKey, bool> wentDown) where TKey : struct
        {
            if (wentDown == null) throw new ArgumentNullException(nameof(wentDown));
            return IsHeld(mainKey, modifiers, isHeld) && wentDown(mainKey);
        }
    }
}
