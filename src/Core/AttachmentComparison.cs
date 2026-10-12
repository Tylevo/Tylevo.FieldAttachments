using System;

namespace Tylevo.FieldAttachments.Core
{
    // Read-only display values. A missing observation remains missing, never zero.
    public sealed class WeaponComparisonValues
    {
        public float? Ergonomics { get; }
        public float? VerticalRecoil { get; }
        public float? HorizontalRecoil { get; }
        public float? Weight { get; }
        public WeaponComparisonValues(float? ergonomics=null,float? verticalRecoil=null,float? horizontalRecoil=null,float? weight=null)
        {
            Ergonomics=Finite(ergonomics); VerticalRecoil=Finite(verticalRecoil);
            HorizontalRecoil=Finite(horizontalRecoil); Weight=Finite(weight);
        }
        private static float? Finite(float? value)=>value.HasValue && !float.IsNaN(value.Value) && !float.IsInfinity(value.Value) ? value : null;
    }

    public sealed class AttachmentComparison
    {
        public WeaponComparisonValues Current { get; }
        public WeaponComparisonValues Preview { get; }
        public string Label { get; }
        public string UnavailableReason { get; }
        public AttachmentComparison(WeaponComparisonValues current,WeaponComparisonValues preview,string label="",string unavailableReason="")
        { Current=current ?? new WeaponComparisonValues(); Preview=preview ?? new WeaponComparisonValues(); Label=label ?? ""; UnavailableReason=unavailableReason ?? ""; }
    }

    // Both values share one scale. The delta covers only the interval between them.
    public readonly struct AttachmentComparisonBar
    {
        public readonly bool HasCurrent, HasPreview;
        public readonly float CurrentFill, PreviewFill, DeltaStart, DeltaWidth;
        public readonly float? Delta;
        public readonly int Benefit; // +1 improvement, -1 worsening, 0 unchanged/unavailable.
        public AttachmentComparisonBar(float? current,float? preview,float scale,bool higherIsBetter)
        {
            HasCurrent=Finite(current); HasPreview=Finite(preview);
            float maximum=Finite(scale) && scale>0 ? scale : 1;
            if(HasCurrent) maximum=Math.Max(maximum,Math.Max(0,current!.Value));
            if(HasPreview) maximum=Math.Max(maximum,Math.Max(0,preview!.Value));
            CurrentFill=HasCurrent ? Math.Max(0,current!.Value)/maximum : 0;
            PreviewFill=HasPreview ? Math.Max(0,preview!.Value)/maximum : 0;
            Delta=HasCurrent && HasPreview ? preview!.Value-current!.Value : (float?)null;
            DeltaStart=HasCurrent && HasPreview ? Math.Min(CurrentFill,PreviewFill) : 0;
            DeltaWidth=HasCurrent && HasPreview ? Math.Abs(PreviewFill-CurrentFill) : 0;
            Benefit=Delta.HasValue && Math.Abs(Delta.Value)>.00001f ? Math.Sign(Delta.Value)*(higherIsBetter ? 1 : -1) : 0;
        }
        private static bool Finite(float? value)=>value.HasValue && !float.IsNaN(value.Value) && !float.IsInfinity(value.Value);
    }
}
