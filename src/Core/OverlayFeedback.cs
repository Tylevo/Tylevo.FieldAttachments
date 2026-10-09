namespace Tylevo.FieldAttachments.Core
{
    // Compact action state only. Controls, tips, pose progress and scan diagnostics stay out of the HUD.
    public static class OverlayFeedback
    {
        public static string Status(bool armed, InstallSession session, bool poseFault)
        {
            if (session.Blocked) return "UNKNOWN - RESTART REQUIRED";
            if (session.Busy) return session.Phase == InstallPhase.Pending ? "INSTALL PENDING" : "VALIDATING";
            if (poseFault) return "POSE FAULT";
            if (armed) return "ARMED";
            return session.Phase == InstallPhase.Succeeded ? "INSTALLED" :
                session.Phase == InstallPhase.Rejected ? "INSTALL REJECTED" : "";
        }
    }
}
