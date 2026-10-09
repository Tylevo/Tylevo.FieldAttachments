using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Tylevo.FieldAttachments.Core;

internal static partial class Program
{
    private static void TestCustomMcxBackport()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("mcx-backport.json")!;
        using var fixture = JsonDocument.Parse(stream);
        var data = fixture.RootElement;
        var clip = data.GetProperty("clip");
        string name = clip.GetProperty("name").GetString()!, template = McxInspectionSegment.Template;
        string controller = data.GetProperty("controller").GetString()!;
        float length = clip.GetProperty("stop").GetSingle() - clip.GetProperty("start").GetSingle();
        Check(data.GetProperty("actualState").GetInt32() == PoseMarker.SharedState && data.GetProperty("layer").GetInt32() == 1 &&
            data.GetProperty("storedBehaviourHash").GetInt32() != PoseMarker.SharedState,
            "Actual WTT MCX controller binds Hands.LOOK while serialized behaviour hash is stale");
        Check(CustomStmInspection.RecipientDenial(template, controller, name, length, clip.GetProperty("legacy").GetBoolean(), false,
            clip.GetProperty("events").GetArrayLength()) == "", "Exact installed WTT MCX source clip accepts authored presentation");
        Check(CustomStmInspection.ValidClip(template, CustomStmInspection.ClipFor(template), CustomStmInspection.Duration, false, false, 0),
            "Longer backport native clip still uses the pinned authored MCX asset and hold timeline");
        foreach (string bad in new[] { "template", "controller", "name", "duration", "legacy", "human", "events", "nan" })
            Check(CustomStmInspection.RecipientDenial(bad == "template" ? CustomStmInspection.M4Template : template,
                bad == "controller" ? StmPresentation.Controller : controller, bad == "name" ? name + "_copy" : name,
                bad == "duration" ? 3.65f : bad == "nan" ? float.NaN : length, bad == "legacy", bad == "human", bad == "events" ? 1 : 0).Length != 0,
                "Backport metadata does not waive " + bad);
        Check(CustomStmInspection.RecipientDenial(template, controller, name, CustomStmInspection.Duration, false, false, 0).Length != 0 &&
            CustomStmInspection.RecipientDenial(template, controller, "mcx_look", length, false, false, 0).Length != 0,
            "Native and WTT clip identities cannot borrow each other's duration");
        var events = LongRifleEvents(data);
        Check(CustomStmInspection.EventDenial(template, name, events) == "", "Actual nine-event WTT MCX signature is accepted only on authored branch");
        Check(GlobalMcxInspection.NativeEventDenial(events).Length != 0 && !McxInspectionSegment.EventsMatch(events),
            "Native/global MCX seek checks remain strict; nonzero native start is not permission to seek");
        Check(CustomStmInspection.EventDenial(CustomStmInspection.M4Template, name, events).Length != 0 &&
            CustomStmInspection.EventDenial(template, "mcx_look", events).Length != 0 &&
            CustomStmInspection.EventDenial(template, name, McxEvents()).Length != 0,
            "Backport event signature cannot be mixed with another template/clip/profile");
        var before = events.Select(e => (e.Name, e.Hash, e.Time, e.Text, e.ParameterType, e.Enabled, e.Conditions, e.Boolean, e.Integer, e.Float)).ToArray();
        CustomStmInspection.EventDenial(template, name, events);
        Check(before.SequenceEqual(events.Select(e => (e.Name, e.Hash, e.Time, e.Text, e.ParameterType, e.Enabled, e.Conditions, e.Boolean, e.Integer, e.Float))),
            "Backport compatibility leaves native start/audio events byte-value equivalent");
        for (int i = 0; i < events.Count; i++)
        foreach (string bad in new[] { "name", "hash", "time", "nan", "text", "type", "disabled", "conditional", "bool", "int", "float" })
        {
            var changed = LongRifleEvents(data); var e = changed[i];
            switch (bad)
            {
                case "name": e.Name = "AddAmmoInChamber"; break;
                case "hash": e.Hash++; break;
                case "time": e.Time += .001f; break;
                case "nan": e.Time = float.NaN; break;
                case "text": e.Text = "BoltRelease"; break;
                case "type": e.ParameterType++; break;
                case "disabled": e.Enabled = false; break;
                case "conditional": e.Conditions = 1; break;
                case "bool": e.Boolean = true; break;
                case "int": e.Integer = 1; break;
                case "float": e.Float = float.NaN; break;
            }
            Check(CustomStmInspection.EventDenial(template, name, changed).Length != 0, "Changed WTT event rejected: row " + i + " " + bad);
        }
        events.RemoveAt(0);
        Check(CustomStmInspection.EventDenial(template, name, events).Length != 0, "Missing WTT native start cannot be fabricated");
        events = LongRifleEvents(data); events.Add(Sound(.95f, "HandOn"));
        Check(CustomStmInspection.EventDenial(template, name, events).Length != 0, "Additional WTT event rejects exact profile");
        events = LongRifleEvents(data); events.Reverse();
        Check(CustomStmInspection.EventDenial(template, name, events).Length != 0, "Reordered WTT events reject exact profile");
        Check(CustomStmInspection.EventDenial(template, "mcx_look", McxEvents()) == "", "Original MCX profile remains supported");
    }
}
