namespace Tylevo.FieldAttachments.Core
{
    // Presentation only: never changes a pose, transaction or its resume policy.
    public static class CrossPresentation
    {
        public static string HiddenReason(bool poseOpen, bool poseActive, bool held,
            bool clickWaiting, bool inventoryPending, bool resumePending)
        {
            if (clickWaiting) return "returning weapon for attachment change";
            if (inventoryPending) return "native attachment change pending";
            if (resumePending) return "waiting for held pose to resume";
            if ((poseOpen || poseActive) && !held) return "weapon entering or leaving held pose";
            return ""; // Standalone F8 browsing remains available without an inspection pose.
        }
    }
}
