using System;
using System.Reflection;
using System.Text.Json;
using Tylevo.FieldAttachments.Core;

internal static partial class Program
{
    private static void TestFiringHandPresentation()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("all-presentations.json")!;
        using var document = JsonDocument.Parse(stream);
        int pistols = 0, unchanged = 0;
        foreach (var row in document.RootElement.GetProperty("recipients").EnumerateArray())
        {
            string template = row.GetProperty("template").GetString()!;
            bool pistol = row.GetProperty("category").GetString() == "pistol";
            Check(FiringHandPresentation.RequiresFiringHand(template) == pistol,
                (pistol ? "Audited pistol receives firing-hand presentation: " : "Existing nonpistol keeps support-arm-only rendering: ") + template);
            if (pistol) pistols++; else unchanged++;
        }
        Check(pistols == 27 && unchanged == 126, "Pistol fix is limited to the 27 handguns in the original broad catalog");
        using var familyStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("family-presentations.json")!;
        using var families = JsonDocument.Parse(familyStream);
        foreach (var row in families.RootElement.GetProperty("recipients").EnumerateArray())
        {
            string template = row.GetProperty("template").GetString()!;
            Check(FiringHandPresentation.RequiresFiringHand(template) == (row.GetProperty("family").GetString() == "pistol"),
                "Family recipient preserves its handgun/nonpistol rendering policy: " + template);
        }
        Check(FiringHandPresentation.RequiresFiringHand(CustomStmInspection.Glock17Template), "Dedicated Glock 17 receives its authored firing-hand pose");
        foreach (string template in new[] { StmPresentation.Template, CustomStmInspection.M4Template, McxInspectionSegment.Template,
            CustomStmInspection.M700Template, CustomStmInspection.M870Template })
            Check(!FiringHandPresentation.RequiresFiringHand(template), "Established long-gun donor remains unchanged: " + template);
        Check(FiringHandPresentation.RequiresFiringHand("6850956dcf12c18a4d8ed9ab") &&
            FiringHandPresentation.RequiresFiringHand("6868377c7bb1c07772467ee7"), "Audited ECOT Glock 22 and FNX-45 use the pistol rendering policy");
        Check(FiringHandPresentation.RequiresFiringHand("620109578d82e67e7911abf2") &&
            FiringHandPresentation.RequiresFiringHand("676bf44c5539167c3603e869"),
            "SP-81 and the newly fitted RShG-2 display a firing hand matching their authored grip");
        foreach (string template in new[] { "624c0b3340357b5f566e8766", "62178be9d0050232da3485d9",
            "66d98233302686954b0c6f81", "675ea3d6312c0a5c4e04e317", "6217726288ed9f0845317459",
            "62178c4d4ecf221597654e3d", "66d9f1abb16d9aacf5068468" })
            Check(FiringHandPresentation.RequiresFiringHand(template), "Verified flare grip displays its authored firing hand: " + template);
        Check(!FiringHandPresentation.RequiresFiringHand("") && !FiringHandPresentation.RequiresFiringHand("unknown-pistol") &&
            !FiringHandPresentation.RequiresFiringHand(CustomStmInspection.Glock17Template.ToUpperInvariant()),
            "Unknown names and altered template identities cannot enable firing-hand retargeting");
    }
}
