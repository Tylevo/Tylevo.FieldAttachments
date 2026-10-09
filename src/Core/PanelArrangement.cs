using System;

namespace Tylevo.FieldAttachments.Core
{
    // Retain separation sides, not old screen positions: the live plane still follows the gun.
    public sealed class PanelArrangement
    {
        private byte[,] _sides = new byte[0, 0];
        private float _width, _height;
        public int Reflows { get; private set; }
        public void Reset() { _sides = new byte[0, 0]; Reflows = 0; }

        public bool Arrange(PanelProjection[] panels, float width, float height)
        {
            if (_sides.GetLength(0) != panels.Length || width != _width || height != _height)
            { Reset(); _sides = new byte[panels.Length, panels.Length]; _width = width; _height = height; }
            for (int i = 0; i < panels.Length; i++)
            {
                var p = panels[i];
                if (!p.Fit(width, height)) return false;
                float best = float.PositiveInfinity, bestX = 0, bestY = 0;
                int fewestChanges = int.MaxValue;
                for (int xi = 0; xi <= i * 2; xi++)
                    for (int yi = 0; yi <= i * 2; yi++)
                    {
                        float x = xi == 0 ? 0 : xi % 2 == 1 ? panels[(xi - 1) / 2].Left - 12 - p.Right : panels[(xi - 1) / 2].Right + 12 - p.Left;
                        float y = yi == 0 ? 0 : yi % 2 == 1 ? panels[(yi - 1) / 2].Top - 12 - p.Bottom : panels[(yi - 1) / 2].Bottom + 12 - p.Top;
                        if (p.Left + x < 23.95f || p.Right + x > width - 23.95f || p.Top + y < 135.95f || p.Bottom + y > height - 151.95f) continue;
                        bool overlap = false;
                        int changes = 0;
                        for (int j = 0; j < i; j++)
                        {
                            byte sides = Separation(p, panels[j], x, y);
                            overlap |= sides == 0;
                            if (_sides[i, j] != 0 && (sides & _sides[i, j]) == 0) changes++;
                        }
                        float cost = x * x + y * y;
                        // A cheaper move alone must never flip a panel to another side.
                        if (!overlap && (changes < fewestChanges || (changes == fewestChanges && cost < best)))
                        { fewestChanges = changes; best = cost; bestX = x; bestY = y; }
                    }
                if (float.IsPositiveInfinity(best)) return false;
                p.Translate(bestX, bestY);
            }
            // Commit only a complete layout; a rejected frame cannot poison side memory.
            for (int i = 0; i < panels.Length; i++)
                for (int j = 0; j < i; j++)
                {
                    byte sides = Separation(panels[i], panels[j], 0, 0);
                    if ((_sides[i, j] & sides) != 0) continue;
                    if (_sides[i, j] != 0) Reflows++;
                    // Pick one stable separating axis. Prefer vertical stacking when both fit.
                    _sides[i, j] = (byte)((sides & 4) != 0 ? 4 : (sides & 8) != 0 ? 8 : (sides & 1) != 0 ? 1 : 2);
                }
            return true;
        }
        private static byte Separation(PanelProjection p, PanelProjection other, float x, float y)
        {
            int sides = 0;
            if (p.Right + x + 11.9f <= other.Left) sides |= 1;
            if (p.Left + x >= other.Right + 11.9f) sides |= 2;
            if (p.Bottom + y + 11.9f <= other.Top) sides |= 4;
            if (p.Top + y >= other.Bottom + 11.9f) sides |= 8;
            return (byte)sides;
        }
    }
}
