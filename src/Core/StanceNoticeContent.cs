namespace Tylevo.FieldAttachments.Core
{
    public enum StanceAvailability { NotDetected, MissingCommonLibrary, Ready, Unavailable }

    public sealed class StanceNoticeContent
    {
        public const string ForgePage = "https://sp-mod.com/mod/3136/spt-stance-overhaul";
        private const string OptionalDisclaimer = "Stance Overhaul is fully optional, but recommended for smoother animations. All Field Attachments features work without it.";
        public string Heading { get; }
        public string Detail { get; }
        public string ForgeUrl { get; }
        public string LinkLabel { get; }
        public bool Ready { get; }

        private StanceNoticeContent(string heading, string detail, bool ready, bool link, string linkLabel = "View on Forge")
        { Heading = heading; Detail = detail + "\n\n" + OptionalDisclaimer; Ready = ready; ForgeUrl = link ? ForgePage : ""; LinkLabel = linkLabel; }

        public static StanceNoticeContent Create(StanceAvailability availability, bool fluidEnabled)
        {
            if (availability == StanceAvailability.NotDetected)
                return new StanceNoticeContent("STANCE OVERHAUL NOT DETECTED",
                    "You can find the mod and its requirements on Fontaine's Forge page.", false, true);
            if (availability == StanceAvailability.MissingCommonLibrary)
                return new StanceNoticeContent("STANCE OVERHAUL DETECTED",
                    "Animation support is unavailable: Realism Common Library is missing or not loaded. Check the requirements on Fontaine's Forge page. Normal attachment animations still work.", false, true, "View requirements");
            if (availability != StanceAvailability.Ready)
                return new StanceNoticeContent("STANCE OVERHAUL DETECTED",
                    "Animation support is unavailable for this installation. Normal attachment animations are active. Check that your mod versions are supported.", false, true, "View requirements");
            return fluidEnabled
                ? new StanceNoticeContent("STANCE OVERHAUL DETECTED", "Fluid attachment transitions are ready. Attachment animations will follow your stance.", true, false)
                : new StanceNoticeContent("STANCE OVERHAUL DETECTED", "Fluid attachment transitions are turned off. Enable them in F12 > Compatibility if you'd like to use them.", false, false);
        }
    }

    // A dismissal lasts for this launch, including menu re-entry after a raid.
    public sealed class StanceNoticeSession
    {
        public bool Dismissed { get; private set; }
        public bool LinkOpened { get; private set; }
        public bool CanShow(bool mainMenu, bool enabled, bool headless) => mainMenu && enabled && !headless && !Dismissed;
        public void Dismiss() { Dismissed = true; }
        public bool TryOpenLink(bool mainMenu, bool enabled, bool headless, string forgeUrl)
        {
            if (!CanShow(mainMenu, enabled, headless) || LinkOpened || forgeUrl != StanceNoticeContent.ForgePage) return false;
            LinkOpened = true;
            return true;
        }
        public void LinkFailed() { LinkOpened = false; }
    }
}
