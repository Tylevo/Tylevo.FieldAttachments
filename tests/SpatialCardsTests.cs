using System;
using Tylevo.FieldAttachments.Core;

internal static partial class Program
{
    private static PanelProjection FlatCard(float x, float y, float width = 390, float height = 171)
    {
        if (!PanelProjection.TryCreate(new PanelPoint(x, y, 1), new PanelPoint(x + width, y, 1),
            new PanelPoint(x, y + height, 1), out PanelProjection? panel)) throw new Exception("Invalid test card");
        return panel!;
    }
    private static void TestSpatialCards()
    {
        var flat = FlatCard(100, 200);
        Check(flat.Map(0, 0).X == 100 && flat.Map(1, 1).Y == 371, "Flat projection retains opposite card corners");
        Check(flat.Map(.5f, .5f).X == 295 && flat.Map(.5f, .5f).Y == 285.5f, "Flat projection retains glyph and icon alignment");
        // Camera plane: x=200+100u, y=200+100v, z=1+u. Project x/z, y/z.
        Check(PanelProjection.TryCreate(new PanelPoint(200, 200, 1), new PanelPoint(150, 100, 2),
            new PanelPoint(200, 300, 1), out _) == false, "Backwards plane is rejected instead of mirroring text");
        // Positive area with perspective: x=100+400u, y=200+200v, z=1+u.
        Check(PanelProjection.TryCreate(new PanelPoint(100, 200, 1), new PanelPoint(250, 100, 2),
            new PanelPoint(100, 400, 1), out PanelProjection? tilted), "Forward perspective plane is accepted");
        var center = tilted!.Map(.5f, .5f);
        Check(Math.Abs(center.X - 200) < .001 && Math.Abs(center.Y - 200) < .001,
            "Panel interior uses camera depth, not affine corner interpolation");
        Check(tilted.Map(1, 1).X == 250 && tilted.Map(1, 1).Y == 200, "Fourth corner follows the same 3D plane");
        var before = tilted.Map(.23f, .71f);
        tilted.Translate(37, -12);
        var after = tilted.Map(.23f, .71f);
        Check(Math.Abs(after.X - before.X - 37) < .001 && Math.Abs(after.Y - before.Y + 12) < .001 && after.Depth == before.Depth,
            "Screen correction translates all content without altering perspective depth");
        Check(!PanelProjection.TryCreate(new PanelPoint(0, 0, -.1f), new PanelPoint(400, 0, 1), new PanelPoint(0, 200, 1), out _),
            "Behind-camera card corner fails closed");
        Check(!PanelProjection.TryCreate(new PanelPoint(0, 0, 3), new PanelPoint(400, 0, 1), new PanelPoint(0, 200, 1), out _),
            "Unprovided fourth corner crossing the camera plane also fails closed");
        Check(!PanelProjection.TryCreate(new PanelPoint(float.NaN, 0, 1), new PanelPoint(400, 0, 1), new PanelPoint(0, 200, 1), out _) &&
            !PanelProjection.TryCreate(new PanelPoint(0, 0, 1), new PanelPoint(400, 0, float.PositiveInfinity), new PanelPoint(0, 200, 1), out _),
            "Nonfinite geometry is rejected");
        Check(!PanelProjection.TryCreate(new PanelPoint(0, 0, 1), new PanelPoint(400, 0, 1), new PanelPoint(800, 0, 1), out _),
            "Edge-on collapsed plane is rejected");
        Check(flat.LeaderStart(600, 285.5f, out var start) && start.X == 490 && start.Y == 285.5f,
            "Leader meets the visible right edge");
        Check(flat.LeaderStart(295, 100, out start) && start.X == 295 && start.Y == 200, "Leader meets the visible top edge");
        Check(!flat.LeaderStart(295, 285.5f, out _) && !flat.LeaderStart(110, 210, out _), "Anchor inside a card never draws through its content");
        Check(tilted.LeaderStart(400, 150, out start) && Math.Abs(start.X - 287) < .001, "Tilted leader uses the projected edge, not the original rectangle");
        foreach (float aspect in new[] { 4f / 3, 16f / 10, 16f / 9, 21f / 9, 32f / 9 })
        {
            float width = (float)Math.Sqrt(1920 * 1080 * aspect), height = width / aspect;
            var cards = new[] { FlatCard(width - 150, 230), FlatCard(-150, height * .5f),
                FlatCard(width * .5f - 200, 40), FlatCard(width * .5f - 200, height - 200) };
            Check(PanelProjection.Arrange(cards, width, height), "Weapon cards fit aspect " + aspect);
            bool bounded = true, overlap = false;
            for (int i = 0; i < cards.Length; i++)
            {
                bounded &= cards[i].Left >= 23.99 && cards[i].Right <= width - 23.99 && cards[i].Top >= 135.99 && cards[i].Bottom <= height - 151.99;
                for (int j = 0; j < i; j++) overlap |= cards[i].Overlaps(cards[j]);
            }
            Check(bounded && !overlap, "Weapon card bounds protect header/footer and avoid overlaps at aspect " + aspect);
        }
        var coincident = new[] { FlatCard(700, 400), FlatCard(700, 400), FlatCard(700, 400), FlatCard(700, 400) };
        Check(PanelProjection.Arrange(coincident, 1920, 1080), "Coincident slot projections separate into available space");
        bool separated = true;
        for (int i = 0; i < coincident.Length; i++) for (int j = 0; j < i; j++) separated &= !coincident[i].Overlaps(coincident[j]);
        Check(separated, "Separation considers every earlier card, not only the nearest neighbor");
        Check(!PanelProjection.Arrange(new[] { FlatCard(10, 10, 1000) }, 800, 600), "Insufficient canvas space requests the existing flat fallback");
        Check(!flat.Fit(float.NaN, 1080), "Invalid screen size cannot authorize a projection");
        Check(PanelProjection.Arrange(new[] { flat }, 1920, 1080) && flat.Map(0, 0).X == 100 && flat.Map(0, 0).Y == 200,
            "Unobstructed card remains at its weapon-relative position");
    }
}
