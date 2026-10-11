using System;
using System.Linq;

namespace Tylevo.FieldAttachments.Core
{
    // Standalone-only data, not permission to replace native inspection clips.
    public sealed class AuthoredPackPresentationEntry
    {
        public readonly string Template, Bundle, Sha256;
        public readonly string[] Controllers;
        public readonly bool RequiresFiringHand, PreserveNativeLeftHandMarker;
        public AuthoredPackPresentationEntry(string template, string bundle, string sha256,
            string[] controllers, bool requiresFiringHand, bool preserveNativeLeftHandMarker)
        {
            Template = template; Bundle = bundle; Sha256 = sha256; Controllers = controllers;
            RequiresFiringHand = requiresFiringHand;
            PreserveNativeLeftHandMarker = preserveNativeLeftHandMarker;
        }
    }

    public static class AuthoredPackPresentations
    {
        public static readonly AuthoredPackPresentationEntry[] Entries = GeneratedAuthoredPackPresentations.Entries;
        public static AuthoredPackPresentationEntry? For(string template)
        {
            string canonical = InstalledWeaponAliases.Canonical(template);
            return Entries.FirstOrDefault(e => e.Template == canonical);
        }
        public static bool ControllerMatches(string template, string controller) =>
            For(template)?.Controllers.Contains(controller, StringComparer.Ordinal) == true;
        public static bool IsAuthoredClip(string? name) => name != null &&
            Entries.Any(e => name == "Tylevo.FieldAttachments." + e.Bundle);
    }
}
