using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Tylevo.FieldAttachments.Runtime
{
    // The audited body-animator curve consumer, not the weapon Animator.
    // No field/parameter writes, skipped native methods or persistent IK settings.
    internal static class AuthoredSupportHandPatch
    {
        internal static readonly int Left = Animator.StringToHash("Hand_Left");
        internal static readonly int FirstPerson = Animator.StringToHash("First_Person_Curve_Weight");
        private static bool _installed;
        private static Func<object, float, float>? _owner;
        internal static string Fault { get; private set; } = "";

        internal static void Acquire(Func<object, float, float> owner)
        {
            if (_owner != null || Fault.Length != 0) throw new InvalidOperationException("support-hand owner busy or faulted");
            Initialize();
            _owner = owner;
        }

        internal static void Initialize()
        {
            if (!_installed)
            {
                MethodInfo method = typeof(EFT.Player).GetMethod("GetCurveValue", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, new[] { typeof(int) }, null) ?? throw new InvalidOperationException("native curve reader unavailable");
                if (method.ReturnType != typeof(float) || method.Module.ModuleVersionId != new Guid("cc2d80b0-6d5b-4cb1-a581-6d2cc901d4c7"))
                    throw new InvalidOperationException("native hand-weight reader build/signature changed");
                new Harmony("com.tylevo.fieldattachments.support-hand").Patch(method,
                    postfix: new HarmonyMethod(typeof(AuthoredSupportHandPatch).GetMethod(nameof(AfterCurve), BindingFlags.Static | BindingFlags.NonPublic)));
                _installed = true;
            }
        }

        internal static void Release(Func<object, float, float> owner)
        { if (_owner == owner) _owner = null; }

        private static void AfterCurve(object __instance, int __0, ref float __result)
        {
            if (__0 != Left || _owner == null) return;
            try { __result = _owner(__instance, __result); }
            catch (Exception e)
            { _owner = null; Fault = e.GetBaseException().Message; } // Leave the native result untouched.
        }
    }
}
