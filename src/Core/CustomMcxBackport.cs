using System;
using System.Collections.Generic;

namespace Tylevo.FieldAttachments.Core
{
    // Exact local WTT replacements, first introduced for MCX; not native seek permission.
    // The authored path waits for native Ready before replacement; all remaining events
    // are audio. Each source duration remains pinned before using the authored timeline.
    public static class CustomMcxBackport
    {
        public const string NativeClip = "Armature_Armature_mcx_look_0_Base Layer";
        public const string M4NativeClip = "Armature_Armature_m4a1_look_0_Base Layer";
        public const string M870NativeClip = "Armature_Armature_m870_look_0_Base Layer";
        public const float NativeDuration = 3.640000105f;
        public const int EventCount = 9;
        public static bool Matches(string template, string clip) =>
            (template == McxInspectionSegment.Template && clip == NativeClip) ||
            (template == CustomStmInspection.M4Template && clip == M4NativeClip) ||
            (template == CustomStmInspection.M870Template && clip == M870NativeClip);
        public static float NativeDurationFor(string template) => template == CustomStmInspection.M870Template ? 4.800000191f : NativeDuration;
        public static int EventCountFor(string template) => template == CustomStmInspection.M870Template ? 8 : EventCount;
        // M4 and MCX have byte-value-identical native event signatures in these local assets.
        private static readonly float[] Times = { .00336322864f, .0190582965f, .104260087f, .107623316f,
            .478699565f, .523542583f, .77929467f, .8566553f, .865756571f };
        private static readonly string[] Sounds = { "", "HandOff", "GunFlip1", "HandOn", "GunFlip4", "HandOn", "HandOff", "GunFlip2", "HandOn" };
        private static readonly float[] M870Times = { 0f, .0643185303f, .26952526f, .336906582f,
            .580398142f, .609494627f, .831546724f, .880551279f };
        private static readonly string[] M870Sounds = { "", "LookFlip1", "LookFlip2", "LookFlip3", "GunFlip2", "LookFlipMiddle", "BackToIdle", "HandOn" };
        public static string EventDenial(string template, IReadOnlyList<McxInspectionEvent> events)
        {
            if (template != McxInspectionSegment.Template && template != CustomStmInspection.M4Template && template != CustomStmInspection.M870Template)
                return "WTT inspection template outside audited set";
            var times = template == CustomStmInspection.M870Template ? M870Times : Times;
            var sounds = template == CustomStmInspection.M870Template ? M870Sounds : Sounds;
            if (events.Count != times.Length) return "WTT inspection event count changed";
            for (int i = 0; i < times.Length; i++)
            {
                var e = events[i];
                if (e == null || !PoseMarker.Finite(e.Time) || Math.Abs(e.Time - times[i]) > .000001f ||
                    !e.Enabled || e.Conditions != 0 || e.Boolean || e.Integer != 0 || e.Float != 0 ||
                    e.Name != (i == 0 ? "StartUtilityOperation" : "Sound") ||
                    e.Hash != (i == 0 ? 1134400241 : 1554795451) || e.ParameterType != (i == 0 ? 0 : 3) || e.Text != sounds[i])
                    return "WTT inspection event signature changed at " + i;
            }
            return "";
        }
    }
}
