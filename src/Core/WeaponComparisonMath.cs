using System;

namespace Tylevo.FieldAttachments.Core
{
    // EFT 40743 Weapon getters and native CharacteristicsPanel attribute delegates.
    // Mod ergonomics are additive points; recoil is a percentage of EACH base axis.
    public static class WeaponComparisonMath
    {
        public static float Ergonomics(float basis,float modifiers) => basis*(1+modifiers/Math.Max(1,basis));
        public static float Recoil(float basis,float modifiers) => basis+basis*(modifiers/100);
        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static bool Matches(float a,float b) => Finite(a) && Finite(b) && Math.Abs(a-b)<=Math.Max(.0001f,Math.Max(Math.Abs(a),Math.Abs(b))*.00001f);
        public static WeaponComparisonValues Values(float ergonomics,float vertical,float horizontal,float ergonomicsModifiers,float recoilModifiers,float weight)
        {
            float ergo=Ergonomics(ergonomics,ergonomicsModifiers), up=Recoil(vertical,recoilModifiers), back=Recoil(horizontal,recoilModifiers);
            if(!Finite(ergo) || !Finite(up) || !Finite(back) || !Finite(weight) || weight<0) return new WeaponComparisonValues();
            return new WeaponComparisonValues(Math.Max(0,Math.Min(100,ergo)),up,back,weight);
        }
    }
}
