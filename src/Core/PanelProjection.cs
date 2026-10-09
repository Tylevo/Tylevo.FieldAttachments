using System;

namespace Tylevo.FieldAttachments.Core
{
    // Perspective projection of a planar card. Coordinates use the canvas's top-left origin.
    // Three camera-projected corners plus camera depths retain perspective for every glyph/icon.
    public struct PanelPoint
    {
        public float X, Y, Depth;
        public PanelPoint(float x, float y, float depth) { X = x; Y = y; Depth = depth; }
    }

    public sealed class PanelProjection
    {
        private readonly PanelPoint _a, _b, _c;
        private float _dx, _dy;
        public float Left { get; private set; }
        public float Top { get; private set; }
        public float Right { get; private set; }
        public float Bottom { get; private set; }

        private PanelProjection(PanelPoint a, PanelPoint b, PanelPoint c) { _a = a; _b = b; _c = c; }
        public static bool TryCreate(PanelPoint a, PanelPoint b, PanelPoint c, out PanelProjection? projection)
        {
            projection = null;
            if (!Valid(a) || !Valid(b) || !Valid(c)) return false;
            float fourthDepth = b.Depth + c.Depth - a.Depth;
            if (!Finite(fourthDepth) || fourthDepth <= .02f) return false;
            var p = new PanelProjection(a, b, c);
            var d = p.Map(1, 1);
            // Reject collapsed/backwards planes, not just points behind the camera.
            float cross = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
            if (!Finite(d.X) || !Finite(d.Y) || !Finite(cross) || cross < 100) return false;
            p.Left = Math.Min(Math.Min(a.X, b.X), Math.Min(c.X, d.X));
            p.Right = Math.Max(Math.Max(a.X, b.X), Math.Max(c.X, d.X));
            p.Top = Math.Min(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y));
            p.Bottom = Math.Max(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y));
            projection = p; return true;
        }
        public PanelPoint Map(float u, float v)
        {
            float z = _a.Depth + (_b.Depth - _a.Depth) * u + (_c.Depth - _a.Depth) * v;
            return new PanelPoint((_a.X * _a.Depth + (_b.X * _b.Depth - _a.X * _a.Depth) * u +
                (_c.X * _c.Depth - _a.X * _a.Depth) * v) / z + _dx,
                (_a.Y * _a.Depth + (_b.Y * _b.Depth - _a.Y * _a.Depth) * u +
                (_c.Y * _c.Depth - _a.Y * _a.Depth) * v) / z + _dy, z);
        }
        // Hit testing must invert the displayed projective mesh, not its unwarped RectTransform.
        public bool TryUnmap(float x, float y, out float u, out float v)
        {
            u = v = 0;
            if (!Finite(x) || !Finite(y)) return false;
            double tx = x - _dx, ty = y - _dy;
            double a = _b.X * _b.Depth - _a.X * _a.Depth - tx * (_b.Depth - _a.Depth);
            double b = _c.X * _c.Depth - _a.X * _a.Depth - tx * (_c.Depth - _a.Depth);
            double c = _b.Y * _b.Depth - _a.Y * _a.Depth - ty * (_b.Depth - _a.Depth);
            double d = _c.Y * _c.Depth - _a.Y * _a.Depth - ty * (_c.Depth - _a.Depth);
            double e = tx * _a.Depth - _a.X * _a.Depth, f = ty * _a.Depth - _a.Y * _a.Depth;
            double determinant = a*d-b*c;
            if (Math.Abs(determinant) < .0000001) return false;
            u = (float)((e*d-b*f)/determinant); v = (float)((a*f-e*c)/determinant);
            if (!Finite(u) || !Finite(v) || u < -.0001f || u > 1.0001f || v < -.0001f || v > 1.0001f) return false;
            u = Math.Max(0, Math.Min(1, u)); v = Math.Max(0, Math.Min(1, v)); return true;
        }
        public bool Fit(float width, float height)
        {
            if (!Finite(width) || !Finite(height) || Right - Left > width - 48 || Bottom - Top > height - 288) return false;
            Translate(Math.Max(24 - Left, Math.Min(0, width - 24 - Right)),
                Math.Max(136 - Top, Math.Min(0, height - 152 - Bottom)));
            return true;
        }
        public void Translate(float x, float y)
        { _dx += x; _dy += y; Left += x; Right += x; Top += y; Bottom += y; }
        public bool TryPlace(float centerX, float centerY, float scale, out PanelProjection? panel)
        {
            panel = null;
            if (!Finite(centerX) || !Finite(centerY) || !Finite(scale) || scale <= 0 || scale > 1) return false;
            float x = (Left + Right) / 2, y = (Top + Bottom) / 2;
            PanelPoint Place(PanelPoint point) => new PanelPoint(centerX + (point.X - x) * scale,
                centerY + (point.Y - y) * scale, point.Depth);
            return TryCreate(Place(Map(0, 0)), Place(Map(1, 0)), Place(Map(0, 1)), out panel);
        }
        public bool Overlaps(PanelProjection other) => Left < other.Right + 12 && Right + 12 > other.Left &&
            Top < other.Bottom + 12 && Bottom + 12 > other.Top;

        // Stateless entry point for one-shot callers; the overlay retains its arrangement.
        public static bool Arrange(PanelProjection[] panels, float width, float height) =>
            new PanelArrangement().Arrange(panels, width, height);

        public bool LeaderStart(float x, float y, out PanelPoint start)
        {
            start = Map(.5f, .5f);
            var previous = Map(0, 0);
            float dx = x - start.X, dy = y - start.Y;
            for (int i = 0; i < 4; i++)
            {
                var next = i == 0 ? Map(1, 0) : i == 1 ? Map(1, 1) : i == 2 ? Map(0, 1) : Map(0, 0);
                float ex = next.X - previous.X, ey = next.Y - previous.Y;
                float cross = dx * ey - dy * ex;
                if (Math.Abs(cross) > .0001f)
                {
                    float ax = previous.X - start.X, ay = previous.Y - start.Y;
                    float t = (ax * ey - ay * ex) / cross, s = (ax * dy - ay * dx) / cross;
                    if (t >= 0 && t < 1 && s >= 0 && s <= 1)
                    { start = new PanelPoint(start.X + dx * t, start.Y + dy * t, 1); return true; }
                }
                previous = next;
            }
            return false; // Attachment is inside its own panel: no line across the cards.
        }
        private static bool Valid(PanelPoint p) => Finite(p.X) && Finite(p.Y) && Finite(p.Depth) && p.Depth > .02f;
        private static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
    }
}
