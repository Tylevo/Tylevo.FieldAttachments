using System;

namespace Tylevo.FieldAttachments.Core
{
    // Canvas-space layout and projection validation; independent of Unity/game state.
    public static class OverlayLayout
    {
        public const float PanelWidth = 432, PanelHeight = 190, Margin = 24, FooterHeight = 104;

        public static (float x, float y) Panel(AttachmentGroup group, float width, float height)
        {
            float x, y;
            switch (group)
            {
                case AttachmentGroup.Optic: x = width - PanelWidth - Margin; y = height * .22f; break;
                case AttachmentGroup.Muzzle: x = Margin; y = height * .39f; break;
                case AttachmentGroup.Tactical: x = width * .40f - PanelWidth / 2; y = height * .15f; break;
                default: x = width * .30f - PanelWidth / 2; y = height * .66f; break;
            }
            return (Clamp(x, Margin, width - PanelWidth - Margin),
                Clamp(y, 136, height - FooterHeight - PanelHeight - Margin * 2));
        }

        public static bool Project(float screenX, float screenY, float depth, float pixelWidth, float pixelHeight,
            float canvasWidth, float canvasHeight, out float x, out float y)
        {
            x = y = 0;
            if (!Finite(screenX) || !Finite(screenY) || !Finite(depth) || depth <= 0 ||
                !Finite(pixelWidth) || !Finite(pixelHeight) || pixelWidth <= 0 || pixelHeight <= 0 ||
                !Finite(canvasWidth) || !Finite(canvasHeight) || canvasWidth <= 0 || canvasHeight <= 0 ||
                screenX < 0 || screenX > pixelWidth || screenY < 0 || screenY > pixelHeight) return false;
            x = screenX / pixelWidth * canvasWidth;
            y = (1 - screenY / pixelHeight) * canvasHeight;
            return true;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(Math.Max(min, max), value));
    }
}
