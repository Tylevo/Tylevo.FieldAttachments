using System;
using System.Collections.Generic;

namespace Tylevo.FieldAttachments.Core
{
    // Local 4.1.5 MCX asset audit: Hands.LOOK, mcx_look (3.300001 seconds).
    // Only the calibrated MCX itself is supported; this is not a donor override.
    public sealed class McxInspectionEvent
    {
        public string Name = "", Text = "";
        public float Time, Float;
        public int Hash, ParameterType, Integer, Conditions;
        public bool Enabled, Boolean;
        public McxInspectionCondition[] ConditionData = Array.Empty<McxInspectionCondition>();
    }
    public sealed class McxInspectionCondition
    {
        public string Parameter = "";
        public int Type, Mode, Integer;
        public float Float;
        public bool Boolean;
    }

    public enum McxSeekResult { Pending, Confirmed, Fault }

    public sealed class McxInspectionSegment
    {
        public const string Template = "5fbcc1d9016cce60e8341ab3";
        public const string Controller = "weapon_sig_mcx_gen1_762x35";
        public const float LeadIn = 0.06f;
        public bool Consumed { get; private set; }
        public bool Pending { get; private set; }
        public float Entry { get; private set; }
        public float Hold { get; private set; }
        private int _frame;
        private float _started;

        public static string MarkerDenial(PoseMarker? saved, string template, string controller)
        {
            if (template != Template) return "MCX .300 BLK only";
            if (saved == null || !saved.Matches(template, controller, 1, PoseMarker.SharedState))
                return "save a native MCX pose with Shift+F5, then F4";
            if (!PoseMarker.Finite(saved.Time) || saved.Time < 0.45f || saved.Time > 0.8f)
                return "MCX marker must be in the late 45%-80% inspection segment";
            return "";
        }

        public static bool EventsMatch(IReadOnlyList<McxInspectionEvent> events)
        {
            if (events.Count != 3) return false;
            string[] names = { "StartUtilityOperation", "Sound", "Sound" };
            string[] text = { "", "HandOff", "HandOn" };
            float[] times = { 0, 0.0407191962f, 0.9338974357f };
            for (int i = 0; i < 3; i++)
            {
                var e = events[i];
                if (e == null || e.Name != names[i] || e.Text != text[i] || !e.Enabled || e.Conditions != 0 ||
                    !PoseMarker.Finite(e.Time) || Math.Abs(e.Time - times[i]) > 0.00001f ||
                    e.Hash != (i == 0 ? 1134400241 : 1554795451) || e.ParameterType != (i == 0 ? 0 : 3) ||
                    e.Boolean || e.Integer != 0 || e.Float != 0) return false;
            }
            return true;
        }

        public void Reset() { Consumed = Pending = false; Entry = Hold = 0; }
        public bool TryIssue(float current, float hold, int frame, float now, out string denial)
        {
            denial = Consumed ? "MCX entry already consumed" :
                !PoseMarker.Finite(current) || current <= 0 || current > 0.15f ? "MCX entry was not observed early enough" :
                !PoseMarker.Finite(hold) || hold < 0.45f || hold > 0.8f ? "MCX late marker invalid" :
                !PoseMarker.Finite(now) ? "MCX clock invalid" : "";
            if (denial.Length != 0) return false;
            Entry = hold - LeadIn; Hold = hold; _frame = frame; _started = now;
            Consumed = Pending = true; // Consume BEFORE the caller's one native seek.
            return true;
        }
        public McxSeekResult Observe(int frame, float now, float time, bool sameStateAndClip)
        {
            if (!Pending) return McxSeekResult.Fault;
            if (!PoseMarker.Finite(now) || !PoseMarker.Finite(time) || now < _started || now - _started > 0.5f)
            { Pending = false; return McxSeekResult.Fault; }
            if (frame <= _frame) return McxSeekResult.Pending;
            Pending = false;
            return sameStateAndClip && time >= Entry - 0.005f && time <= Hold + 0.12f
                ? McxSeekResult.Confirmed : McxSeekResult.Fault;
        }
        public void Cancel() { Pending = false; } // Cancellation never rearms a consumed request.
    }
}
