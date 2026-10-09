using System;
using Tylevo.FieldAttachments.Core;

internal static partial class Program
{
    private static void TestInstalledWeaponAliases()
    {
        var pairs = new[,] {
            { "62178be9d0050232da3485d9", "624c0b3340357b5f566e8766" },
            { "66d98233302686954b0c6f81", "624c0b3340357b5f566e8766" },
            { "675ea3d6312c0a5c4e04e317", "624c0b3340357b5f566e8766" },
            { "6217726288ed9f0845317459", "624c0b3340357b5f566e8766" },
            { "62178c4d4ecf221597654e3d", "624c0b3340357b5f566e8766" },
            { "66d9f1abb16d9aacf5068468", "624c0b3340357b5f566e8766" },

            { "657857faeff4c850222dff1b", "64637076203536ad5600c990" },
            { "52ce1b65b13e1035808c4fd2", "606587252535c57a13424cfd" },
            { "96f5c38a676e11e13544dfba", "606587252535c57a13424cfd" },
            { "93bcdfda236122e67c098847", "57dc2fa62459775949412633" },
            { "d672109946fe88b803449054", "5ac66cb05acfc40198510a10" },
            { "ffc95b9d143f52202a311820", "5ac66d015acfc400180ae6e4" },
            { "3dc691f607ffed3228bf6ca2", "6499849fc93611967b034949" },
            { "ed05294ed53c0400ae0e8a55", "57dc2fa62459775949412633" },
            { "939c742f7dad852286188029", "5839a40f24597726f856b511" },
            { "627c4fe34b0a558e8a3642a1", "583990e32459771419544dd2" },
            { "68580e9cea46c81b4db2221e", "583990e32459771419544dd2" },
            { "4b81488c78c8a8ac7d37f9b9", "5447a9cd4bdc2dbd208b4567" },
            { "52500592c7109667abb6cbeb", "583990e32459771419544dd2" },
            { "72bbf927bf5b1d4a0837485b", "5fbcc1d9016cce60e8341ab3" },
            { "8d59d8b10a4c2e85b871c317", "65290f395ae2ae97b80fdf2d" },
            { "e895575bcd1fa1de36d301b6", "5ac4cd105acfc40016339859" },
            { "57f28a7ffb22e277b0234219", "5644bd2b4bdc2d3b4c8b4572" },
            { "6850956dcf12c18a4d8ed9ab", "5a7ae0c351dfba0017554310" },
            { "68bc954419645c0da0e9ea05", "5926bb2186f7744b1c6c6e60" },
            { "0af5f6a5aa9712e11c733fb9", "5bfea6e90db834001b7347f3" },
        };
        for(int i=0;i<pairs.GetLength(0);i++)
        {
            string alias=pairs[i,0], donor=pairs[i,1];
            Check(InstalledWeaponAliases.Canonical(alias)==donor && InstalledWeaponAliases.Canonical(donor)==donor,"Audited prefab alias normalizes exactly once: "+alias);
            Check(CustomStmInspection.Requested(true,alias,false) && !CustomStmInspection.Requested(false,alias,false) && !CustomStmInspection.Requested(true,alias,true),"Alias preserves feature and learning gates: "+alias);
            string controller=CustomStmInspection.ControllerFor(donor), clip=CustomStmInspection.ClipFor(donor);
            Check(controller.Length>0 && CustomStmInspection.ControllerFor(alias)==controller && CustomStmInspection.NativeClipFor(alias)==CustomStmInspection.NativeClipFor(donor),"Alias retains exact native identities: "+alias);
            Check(CustomStmInspection.BundleFor(alias)==CustomStmInspection.BundleFor(donor) && CustomStmInspection.ClipFor(alias)==clip,"Alias retains authored bundle and clip: "+alias);
            Check(CustomStmInspection.ControllerMatches(alias,controller) && !CustomStmInspection.ControllerMatches(alias,controller+" changed"),"Alias does not broaden controller guard: "+alias);
            Check(CustomStmInspection.ValidClip(alias,clip,CustomStmInspection.Duration,false,false,0) && !CustomStmInspection.ValidClip(alias,clip,CustomStmInspection.Duration,false,false,1),"Alias retains clip event gate: "+alias);
            Check(Object.ReferenceEquals(AuditedWeaponPresentations.For(alias),AuditedWeaponPresentations.For(donor)),"Generated alias retains all exact native profiles: "+alias);
        }
        Check(InstalledWeaponAliases.Canonical("unverified-template")=="unverified-template" && !CustomStmInspection.Requested(true,"unverified-template",false),"Unknown templates remain unsupported");
        Check(InstalledWeaponAliases.Canonical("6850956dcf12c18a4d8ed9ab")==CustomStmInspection.Glock17Template,"Glock22 resolves before common bundle hash selection");
        Check(!CustomStmInspection.NativeMcxRequested(true,false,"72bbf927bf5b1d4a0837485b",false),"Standalone MCX alias does not broaden the legacy native MCX route");
        TestAdditionalWeaponPresentations();
    }

    private static void TestAdditionalWeaponPresentations()
    {
        string[] expected = { "6963f79ab66d6c6601a8b2d7", "6868377c7bb1c07772467ee7", "68cd937be06f1a3a720d6959", "620109578d82e67e7911abf2", "676bf44c5539167c3603e869", "624c0b3340357b5f566e8766" };
        Check(AdditionalWeaponPresentations.Entries.Length==expected.Length,"Six additional native rigs have independent fitted clips");
        foreach(string template in expected)
        {
            var entry=AuditedWeaponPresentations.For(template);
            Check(entry!=null && entry.Template==template && entry.Bundle=="gun_"+template+"_presentation","Additional recipient resolves its own pose: "+template);
            if(entry==null)continue;
            Check(entry.Sha256.Length==64 && CustomStmInspection.Requested(true,template,false),"Additional authored pose has an exact hash and enabled route: "+template);
            Check(CustomStmInspection.IsAuthoredClip("Tylevo.FieldAttachments."+entry.Bundle) && CustomStmInspection.ValidClip(template,"Tylevo.FieldAttachments."+entry.Bundle,CustomStmInspection.Duration,false,false,0),"Additional clip participates in exact authored identity guard: "+template);
            foreach(var profile in entry.Profiles)
            {
                Check(CustomStmInspection.ControllerMatches(template,profile.Controller) && !CustomStmInspection.ControllerMatches(template,profile.Controller+" changed"),"Additional recipient rejects changed controller: "+template);
                Check(CustomStmInspection.RecipientDenial(template,profile.Controller,profile.Clip,profile.Duration,false,false,0)=="" && CustomStmInspection.RecipientDenial(template,profile.Controller,profile.Clip,profile.Duration,false,false,1)!="","Additional recipient retains native metadata guards: "+template);
                Check(CustomStmInspection.EventDenial(template,profile.Clip,profile.Events)=="" && CustomStmInspection.EventDenial(template,profile.Clip,Array.Empty<McxInspectionEvent>())!="","Additional recipient retains exact event metadata: "+template);
                Check(CustomStmInspection.StateMatches(template,profile.Controller,profile.State) && !CustomStmInspection.StateMatches(template,profile.Controller,profile.State+1),"Additional recipient retains exact state guard: "+template);
            }
        }
        foreach(string settings in new[] { "639af924d0446708ee62294e", "639c3fbbd0446708ee622ee9" })
            Check(!CustomStmInspection.Requested(true,settings,false),"Underbarrel ghost settings do not become FN40GL aliases: "+settings);
    }
}
