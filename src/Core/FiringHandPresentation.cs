using System;
using System.Collections.Generic;

namespace Tylevo.FieldAttachments.Core
{
    // Audited handguns and newly fitted handheld launchers need a displayed firing arm.
    // Previously supported long guns retain their established support-arm-only path.
    public static class FiringHandPresentation
    {
        private static readonly HashSet<string> Templates = new HashSet<string>(StringComparer.Ordinal)
        {
            "5448bd6b4bdc2dfc2f8b4569", // weapon_izhmeh_pm_9x18pm
            "56d59856d2720bd8418b456a", // weapon_sig_p226r_9x19
            "56e0598dd2720bb5668b45a6", // weapon_tochmash_pb_9x18pm
            "571a12c42459771f627b58a0", // weapon_toz_tt_762x25tt
            "576a581d2459771e7b1bc4f1", // weapon_izhmeh_mp443_9x19
            "579204f224597773d619e051", // weapon_izhmeh_pm_treaded_9x18pm
            "59f98b4986f7746f546d2cef", // weapon_tochmash_sr1mp_9x21
            "5a17f98cfcdbcb0980087290", // weapon_molot_aps_9x18pm
            "5abccb7dd8ce87001773e277", // weapon_toz_apb_9x18pm
            "5b1fa9b25acfc40018633c01", // weapon_glock_glock_18c_gen3_9x19
            "5d3eb3b0a4b93615055e84d2", // weapon_fn_five_seven_57x28
            "5d67abc1a4b93614ec50137f", // weapon_fn_five_seven_57x28_fde
            "5e81c3cbac2bb513793cdc75", // weapon_colt_m1911a1_1143x23
            "5f36a0e5fbf956000b716b65", // weapon_colt_m45a1_1143x23
            "602a9740da11d6478d5a06dc", // weapon_izhmash_pl15_9x19
            "6193a720f8ee7e52e42109ed", // weapon_hk_usp_45
            "61a4c8884f95bc3b2c5dc96f", // weapon_chiappa_rhino_50ds_9x33R
            "624c2e8614da335f1e034d8c", // weapon_chiappa_rhino_200ds_9x19
            "63088377b5cd696784087147", // weapon_glock_glock_19x_9x19
            "633ec7c2a6918cb895019c6c", // weapon_kbp_rsh_12_127x55
            "66015072e9f84d5680039678", // weapon_ussr_pd_20x1mm
            "668fe5a998b5ad715703ddd6", // weapon_magnum_research_desert_eagle_mk19_127x33
            "669fa39b48fc9f8db6035a0c", // weapon_magnum_research_desert_eagle_l6_127x33
            "669fa3d876116c89840b1217", // weapon_magnum_research_desert_eagle_l6_tiger_127x33
            "669fa3f88abd2662d80eee77", // weapon_magnum_research_desert_eagle_l5_127x33
            "669fa409933e898cce0c2166", // weapon_magnum_research_desert_eagle_l5_9x33r
            "5b3b713c5acfc4330140bd8d", // TT Gold
            "5a7ae0c351dfba0017554310", // Glock 17
            "5cadc190ae921500103bb3b6", // Beretta M9A3
            "6850956dcf12c18a4d8ed9ab", // ECOT Glock 22
            "6868377c7bb1c07772467ee7", // ECOT FNX-45
            "620109578d82e67e7911abf2", // ZiD SP-81 signal pistol
            "676bf44c5539167c3603e869", // RShG-2: authored grip independent of native ready stance
            "624c0b3340357b5f566e8766", // RSP-30 yellow: independently fitted native flare grip
            "62178be9d0050232da3485d9", // ROP-30 white: identical audited flare rig and grip
            "66d98233302686954b0c6f81", // RSP-30 blue
            "675ea3d6312c0a5c4e04e317", // RSP-30 firework
            "6217726288ed9f0845317459", // RSP-30 green
            "62178c4d4ecf221597654e3d", // RSP-30 red
            "66d9f1abb16d9aacf5068468", // RSP-30 special yellow
        };
        public static bool RequiresFiringHand(string template) => Templates.Contains(template);
    }
}
