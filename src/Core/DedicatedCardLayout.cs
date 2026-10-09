using System;

namespace Tylevo.FieldAttachments.Core
{
    // Category homes are deterministic across openings, scans, slots and animation history.
    // A shared projection keeps every surface identical; only its display position changes.
    public static class DedicatedCardLayout
    {
        public const float Top = 112, Bottom = 24, Gap = 24, MotionX = 24, MotionY = 16;
        internal const float FacingMotionX = 48, FacingMotionY = 32;

        public static bool TryPlace(PanelProjection plane, AttachmentGroup group, float width, float height,
            float weaponX, float weaponY, out PanelProjection? panel, bool faceCamera = false)
        {
            panel = null;
            if (!Finite(width) || !Finite(height) || !Finite(weaponX) || !Finite(weaponY)) return false;
            float motionX = faceCamera ? FacingMotionX : MotionX, motionY = faceCamera ? FacingMotionY : MotionY;
            float band = (height - Top - Bottom - Gap * 2) / 3;
            float availableWidth = width / 2 - OverlayLayout.Margin - Gap / 2 - motionX * 2;
            float scale = Math.Min(1, Math.Min(availableWidth / (plane.Right - plane.Left),
                (band - motionY * 2) / (plane.Bottom - plane.Top)));
            if (!Finite(scale) || scale < .5f) return false;
            float halfWidth = (plane.Right - plane.Left) * scale / 2;
            bool optic = group == AttachmentGroup.Optic;
            int row = group == AttachmentGroup.Muzzle ? 1 : group == AttachmentGroup.Underbarrel ? 2 : 0;
            float desiredX = width * (optic ? .76f : group == AttachmentGroup.Muzzle ? .16f : .30f);
            float minX = (optic ? width / 2 + Gap / 2 : OverlayLayout.Margin) + halfWidth + motionX;
            float maxX = (optic ? width - OverlayLayout.Margin : width / 2 - Gap / 2) - halfWidth - motionX;
            float dx = weaponX - width * .60f, dy = weaponY - height * .72f;
            float x = Clamp(desiredX, minX, maxX) + (faceCamera ? SoftMotion(dx, motionX) : Clamp(dx * .25f, -motionX, motionX));
            float y = Top + band / 2 + row * (band + Gap) + (faceCamera ? SoftMotion(dy, motionY) : Clamp(dy * .25f, -motionY, motionY));
            return plane.TryPlace(x, y, scale, out panel);
        }
        // Continuous bounded sway: distant/off-center weapons never hit a hard stationary plateau.
        // Stateless so opening animation timing cannot shift the category homes.
        internal static float SoftMotion(float delta, float limit) => (float)(limit * 2 / Math.PI * Math.Atan(delta * .25 * Math.PI / (2 * limit)));
        private static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
