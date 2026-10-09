using System;
using Tylevo.FieldAttachments.Core;

internal static partial class Program
{
    private static void TestPanelArrangement()
    {
        // Reproduce the old nearest-space flip: a fifth of a pixel changes above to below.
        var before = new[] { FlatCard(700, 400), FlatCard(700, 399.9f) };
        var after = new[] { FlatCard(700, 400), FlatCard(700, 400.1f) };
        PanelProjection.Arrange(before, 1920, 1080); PanelProjection.Arrange(after, 1920, 1080);
        Check(before[1].Bottom < before[0].Top && after[1].Top > after[0].Bottom,
            "Regression fixture reproduces the former per-frame above/below jump");

        var layout = new PanelArrangement();
        bool stable = true, bounded = true, follows = true;
        float previousLeft = 0, previousTop = 0;
        for (int frame = 0; frame < 120; frame++)
        {
            float motion = frame * .4f, jitter = frame % 2 == 0 ? -.1f : .1f;
            var cards = new[] { FlatCard(700 + motion, 400 + motion), FlatCard(700 + motion, 400 + motion + jitter) };
            stable &= layout.Arrange(cards, 1920, 1080) && cards[1].Bottom < cards[0].Top;
            bounded &= !cards[1].Overlaps(cards[0]) && cards[1].Top >= 136 && cards[1].Right <= 1896;
            if (frame > 0) follows &= Math.Abs(cards[1].Left - previousLeft - .4f) < .01f &&
                Math.Abs(cards[1].Top - previousTop - .4f) < .01f;
            previousLeft = cards[1].Left; previousTop = cards[1].Top;
        }
        Check(stable && layout.Reflows == 0, "Subpixel noise cannot flip a retained separation side across 120 frames");
        Check(bounded, "Stable panels remain separated and within usable screen bounds");
        Check(follows, "Side memory retains live weapon translation instead of freezing screen coordinates");

        var unconstrained = new[] { FlatCard(200, 250), FlatCard(700, 600) };
        Check(new PanelArrangement().Arrange(unconstrained, 1920, 1080) && unconstrained[0].Left == 200 && unconstrained[1].Top == 600,
            "Unobstructed panels keep their native projected positions");
        var edge = new[] { FlatCard(700, 136), FlatCard(700, 135.9f) };
        Check(layout.Arrange(edge, 1920, 1080) && edge[1].Top > edge[0].Bottom && layout.Reflows == 1,
            "Screen edge permits a necessary reflow when the retained side has no room");
        bool edgeStable = true;
        for (int frame = 0; frame < 40; frame++)
        {
            var cards = new[] { FlatCard(700, 140), FlatCard(700, 140 + (frame % 2 == 0 ? -.1f : .1f)) };
            edgeStable &= layout.Arrange(cards, 1920, 1080) && cards[1].Top > cards[0].Bottom;
        }
        Check(edgeStable && layout.Reflows == 1, "Necessary reflow becomes the stable side instead of oscillating back");

        // A late failure must not commit a different side for an earlier card.
        var failed = new[] { FlatCard(700, 757), FlatCard(700, 757), FlatCard(300, 400, 3000) };
        var atomic = new PanelArrangement();
        var seed = new[] { FlatCard(700, 400), FlatCard(700, 400.1f), FlatCard(50, 250) };
        Check(atomic.Arrange(seed, 1920, 1080), "Three-panel seed can be arranged");
        Check(!atomic.Arrange(failed, 1920, 1080) && atomic.Reflows == 0, "Failed full layout does not commit partial side changes");
        seed = new[] { FlatCard(700, 400), FlatCard(700, 399.9f), FlatCard(50, 250) };
        Check(atomic.Arrange(seed, 1920, 1080) && seed[1].Top > seed[0].Bottom,
            "Last successful side survives a rejected frame");

        layout.Reset();
        before = new[] { FlatCard(700, 400), FlatCard(700, 399.9f) };
        Check(layout.Arrange(before, 1920, 1080) && before[1].Bottom < before[0].Top && layout.Reflows == 0,
            "Close or weapon/slot identity reset starts a fresh layout");
        after = new[] { FlatCard(700, 400), FlatCard(700, 400.1f) };
        Check(layout.Arrange(after, 1921, 1080) && after[1].Top > after[0].Bottom,
            "Canvas resize discards stale separation choices");

        var four = new PanelArrangement();
        bool allFit = true, allSeparate = true, noJumps = true;
        var lastY = new float[4];
        for (int frame = 0; frame < 90; frame++)
        {
            var cards = new PanelProjection[4];
            for (int i = 0; i < 4; i++) cards[i] = FlatCard(700 + frame * .2f, 400 + (frame % 2 == 0 ? -.1f : .1f) * i);
            allFit &= four.Arrange(cards, 1920, 1080);
            for (int i = 0; i < 4; i++)
            {
                if (frame > 0) noJumps &= Math.Abs(cards[i].Top - lastY[i]) < 1;
                lastY[i] = cards[i].Top;
                for (int j = 0; j < i; j++) allSeparate &= !cards[i].Overlaps(cards[j]);
            }
        }
        Check(allFit && allSeparate, "Four coincident panels resolve together throughout a moving sequence");
        Check(noJumps && four.Reflows == 0, "Four-panel separation does not cascade subpixel changes into jumps");

        PanelProjection.TryCreate(new PanelPoint(500, 400, 1), new PanelPoint(750, 300, 2),
            new PanelPoint(500, 580, 1), out var tilted);
        var origin = tilted!.Map(.37f, .63f); var corner = tilted.Map(0, 0);
        Check(new PanelArrangement().Arrange(new[] { FlatCard(500, 350), tilted }, 1920, 1080), "Tilted card can be separated");
        var moved = tilted.Map(.37f, .63f); var movedCorner = tilted.Map(0, 0);
        Check(moved.Depth == origin.Depth && Math.Abs(moved.X - origin.X - (movedCorner.X - corner.X)) < .001f &&
            Math.Abs(moved.Y - origin.Y - (movedCorner.Y - corner.Y)) < .001f,
            "Persistent separation preserves the live perspective and internal glyph alignment");
    }
}
