using System;
using Tylevo.FieldAttachments.Core;
using Vector3 = Tylevo.FieldAttachments.Core.CardVector;

internal static partial class Program
{
    private static PanelProjection SharedCard(float tilt = 0)
    {
        WeaponCardBasis.TryCreate(new Vector3(-.4863f, -.0615f + tilt, .2822f),
            new Vector3(-.0732f, .9932f, .0902f), out var nativeRight, out var nativeUp);
        WeaponCardBasis.TryTune(nativeRight, nativeUp, .55f, 5, out var right, out var up);
        var center = new Vector3(0, 0, 1);
        var a = center - right * .216f + up * .095f;
        PanelPoint Project(Vector3 v) => new PanelPoint(960 + v.X / v.Z * 900, 540 - v.Y / v.Z * 900, v.Z);
        if (!PanelProjection.TryCreate(Project(a), Project(a + right * .432f), Project(a - up * .19f), out var panel))
            throw new Exception("Invalid shared card fixture");
        return panel!;
    }
    private static PanelProjection[] HomeCards(PanelProjection plane, float width, float height, float x, float y)
    {
        var groups = new[] { AttachmentGroup.Optic, AttachmentGroup.Muzzle, AttachmentGroup.Tactical, AttachmentGroup.Underbarrel };
        var panels = new PanelProjection[4];
        for (int i = 0; i < groups.Length; i++)
        {
            if (!DedicatedCardLayout.TryPlace(plane, groups[i], width, height, x, y, out var placed))
                throw new Exception("Home layout failed at " + width + "x" + height);
            panels[i] = placed!;
        }
        return panels;
    }
    private static void TestDedicatedCards()
    {
        var plane = SharedCard();
        var original = plane.Map(.3f, .7f);
        var first = HomeCards(plane, 1920, 1080, 1152, 777.6f);
        Check(first[2].Right < first[0].Left && first[1].Top > first[2].Bottom && first[3].Top > first[1].Bottom,
            "Tactical upper-left, optic upper-right, muzzle middle-left and foregrip lower-left have dedicated homes");
        bool sameShape = true;
        foreach (var p in first)
        {
            var a = p.Map(0, 0); var source = first[0].Map(0, 0);
            foreach (var uv in new[] { (1f, 0f), (1f, 1f), (0f, 1f), (.37f, .62f) })
            {
                var point = p.Map(uv.Item1, uv.Item2); var expected = first[0].Map(uv.Item1, uv.Item2);
                sameShape &= Math.Abs(point.X - a.X - (expected.X - source.X)) < .001 &&
                    Math.Abs(point.Y - a.Y - (expected.Y - source.Y)) < .001 && point.Depth == expected.Depth;
            }
        }
        Check(sameShape, "Foregrip and all other cards have identical slope, skew, scale and interior projection");
        Check(plane.Map(.3f, .7f).X == original.X && plane.Map(.3f, .7f).Y == original.Y,
            "Placing groups does not mutate the shared source plane");
        bool bounded = true, separated = true, fixedRows = true;
        foreach (float aspect in new[] { 4f / 3, 16f / 10, 16f / 9, 21f / 9, 32f / 9 })
        {
            float width = (float)Math.Sqrt(1920 * 1080 * aspect), height = width / aspect;
            foreach (float extreme in new[] { -10000f, 10000f })
            {
                var cards = HomeCards(plane, width, height, extreme, extreme);
                fixedRows &= cards[2].Right < cards[0].Left && cards[1].Top > cards[2].Bottom && cards[3].Top > cards[1].Bottom;
                for (int i = 0; i < cards.Length; i++)
                {
                    bounded &= cards[i].Left >= 23.99 && cards[i].Top >= DedicatedCardLayout.Top - .01 &&
                        cards[i].Right <= width - 23.99 && cards[i].Bottom <= height - 23.99;
                    for (int j = 0; j < i; j++) separated &= !cards[i].Overlaps(cards[j]);
                }
            }
        }
        Check(bounded, "Dedicated homes stay within screen/title bounds at 4:3 through 32:9 and both motion limits");
        Check(separated, "Shared-plane homes never overlap at tested aspect ratios and motion limits");
        Check(fixedRows, "Motion and aspect changes cannot swap the categories' assigned rows or sides");
        bool historyIndependent = true;
        for (int opening = 0; opening < 20; opening++)
        {
            // Simulate different entrance samples, including flat fallback before the final pose.
            HomeCards(SharedCard((opening % 5 - 2) * .04f), 1920, 1080, 700 + opening * 23, 850);
            HomeCards(FlatCard(0, 0, 432, 190), 1920, 1080, 1000, 900);
            var reopened = HomeCards(SharedCard(), 1920, 1080, 1152, 777.6f);
            for (int i = 0; i < 4; i++) historyIndependent &= Math.Abs(reopened[i].Left - first[i].Left) < .001 &&
                Math.Abs(reopened[i].Top - first[i].Top) < .001 && reopened[i].Map(1, 1).Depth == first[i].Map(1, 1).Depth;
        }
        Check(historyIndependent, "Twenty reopening histories converge to identical homes at the same weapon pose");
        var moved = HomeCards(plane, 1920, 1080, 1192, 797.6f);
        bool follows = true;
        for (int i = 0; i < 4; i++) follows &= Math.Abs(moved[i].Left - first[i].Left - 10) < .001 &&
            Math.Abs(moved[i].Top - first[i].Top - 5) < .001;
        Check(follows, "Bounded weapon motion translates all category homes together");
        var tilted = HomeCards(SharedCard(.1f), 1920, 1080, 1152, 777.6f);
        Check(Math.Abs((tilted[3].Map(1, 0).Y - tilted[3].Map(0, 0).Y) -
            (first[3].Map(1, 0).Y - first[3].Map(0, 0).Y)) > 5, "Dedicated placement preserves animated tilt instead of freezing the cards");
        var flat = HomeCards(FlatCard(0, 0, 432, 190), 1920, 1080, 1152, 777.6f);
        Check(flat[3].Top > flat[1].Bottom && flat[2].Right < flat[0].Left && flat[3].Map(0, 0).Y == flat[3].Map(1, 0).Y,
            "Flat fallback uses the same category assignments without hidden packing history");
        Check(!DedicatedCardLayout.TryPlace(plane, AttachmentGroup.Optic, 300, 200, 100, 100, out _), "Unusable tiny viewport rejects unreadable home geometry");
        Check(!DedicatedCardLayout.TryPlace(plane, AttachmentGroup.Optic, float.NaN, 1080, 100, 100, out _) &&
            !DedicatedCardLayout.TryPlace(plane, AttachmentGroup.Optic, 1920, 1080, float.PositiveInfinity, 100, out _),
            "Invalid screen or weapon coordinates cannot reach home placement");

        var session = new InstallSession();
        Check(OverlayFeedback.Status(false, session, false) == "", "Idle HUD adds no instruction or diagnostic text");
        Check(OverlayFeedback.Status(true, session, false) == "ARMED", "Armed intent remains visible after footer removal");
        session.TryBegin(); session.MarkSubmitted();
        Check(OverlayFeedback.Status(true, session, false) == "INSTALL PENDING", "Pending native request takes precedence over stale armed presentation");
        session.TimedOut();
        Check(OverlayFeedback.Status(true, session, false) == "UNKNOWN - RESTART REQUIRED", "Uncertainty latch remains visible without tooltips");
        var result = new InstallSession(); result.TryBegin(); result.Finish("done", false, true);
        Check(OverlayFeedback.Status(false, result, false) == "INSTALLED" && OverlayFeedback.Status(true, result, false) == "ARMED",
            "Completed request is visible and a later arm replaces the previous result");
        result.RejectRequest("guard");
        Check(OverlayFeedback.Status(false, result, false) == "INSTALL REJECTED" && OverlayFeedback.Status(false, result, true) == "POSE FAULT",
            "Rejected request and pose faults retain concise action feedback");
    }
}
