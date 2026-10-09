using BepInEx.Configuration;

namespace Tylevo.FieldAttachments.Runtime
{
    // ConfigurationManager reads this standard tag by class/field name. No new dependency.
    internal sealed class ConfigurationManagerAttributes
    {
        public bool? Browsable = false;
        public string? DispName;
        public string? Category;
        public bool? IsAdvanced = false;
        public int? Order;
    }

    internal static class SettingsPresentation
    {
        internal static ConfigDescription Describe(string section, string key, ConfigDescription original)
        {
            var tag = new ConfigurationManagerAttributes();
            string help = original.Description;
            switch (section + "/" + key)
            {
                case "General/Enabled":
                    Show(tag, "General", "Enabled", 10);
                    help = "Enable Field Attachments.";
                    break;
                case "Experiments/EnableEmptySlotInstall":
                    Show(tag, "General", "Allow attachment changes", 20);
                    help = "Allow clicks to install, remove or replace compatible carried attachments, including intact optic/mount assemblies. OFF keeps browsing available without moving items. Native raid restrictions and available storage still apply.";
                    break;
                case "Controls/MenuKey":
                    Show(tag, "Controls", "Attachment menu key", 1);
                    help = "Open the weapon presentation and cursor together. Default: Left Alt. Right Alt is also available; None disables this shortcut.";
                    break;
                case "Controls/MenuActivation":
                    Show(tag, "Controls", "Menu activation (Hold / Toggle)", 2);
                    help = "Hold: keep Alt held to use the menu; release to close. Toggle: tap Alt to open, then tap again to close. Alt+R or clicking while Alt is held keeps Toggle open. Right mouse closes LIST only. Closing cancels waiting requests but does not undo a submitted native operation.";
                    break;
                case "Controls/AttachmentPose":
                    Show(tag, "Controls", "Legacy attachment mode key", 10);
                    tag.IsAdvanced = true;
                    help = "Optional separate presentation key. Unbound by default. If you assign a key here, use the legacy cursor modifier separately. The main Alt menu already opens the presentation and cursor together.";
                    break;
                case "Controls/ToggleAttachmentPose":
                    Show(tag, "Controls", "Legacy pose toggle", 20);
                    tag.IsAdvanced = true;
                    help = "ON: press the attachment key to open, then again to close. OFF: hold the key. Closing cancels any attachment request that has not been submitted.";
                    break;
                case "Controls/MouseModifier":
                    Show(tag, "Controls", "Legacy cursor modifier", 30);
                    tag.IsAdvanced = true;
                    help = "Cursor modifier for an O-opened legacy session. The new Alt menu captures its cursor automatically. Default: Left Alt.";
                    break;
                case "Controls/CycleAttachmentPosition":
                    Show(tag, "Controls", "Cycle mounting position", 40);
                    help = "Cycle the open LIST or hovered card's mounting point while the cursor is active. The count and connection line identify the selected point. Default: Left Alt + R.";
                    break;
                case "Controls/MouseSensitivity":
                    Show(tag, "Controls", "Cursor speed", 50);
                    break;
                case "Controls/AttachmentAnimationSpeedMultiplier":
                    Show(tag, "Controls", "Attachment animation speed", 60);
                    break;
                case "Display/RequestNativeIcons":
                    Show(tag, "Display", "Show attachment thumbnails", 10);
                    help = "Show native item thumbnails. Names remain available when a thumbnail cannot be loaded.";
                    break;
                case "Display/WeaponFollowingCards":
                    Show(tag, "Display", "Follow weapon movement", 20);
                    help = "Let the attachment cards follow the weapon's movement and sway.";
                    break;
                case "Controls/ExportReport":
                    Show(tag, "Diagnostics", "Save diagnostic report", 10);
                    tag.IsAdvanced = true;
                    help = "Save the current attachment state and recent operation results for a bug report. Default: F10. This never moves an item.";
                    break;
            }
            // Keep all stored keys and their values, even when hidden from the menu.
            var tags = new object[original.Tags.Length + 1];
            original.Tags.CopyTo(tags, 0);
            tags[tags.Length - 1] = tag;
            return new ConfigDescription(help, original.AcceptableValues, tags);
        }

        private static void Show(ConfigurationManagerAttributes tag, string category, string name, int order)
        { tag.Browsable = true; tag.Category = category; tag.DispName = name; tag.Order = order; }
    }
}
