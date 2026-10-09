using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Tylevo.FieldAttachments.Runtime
{
    // Only UI resources and label localization are invoked. No inventory operation is invoked.
    public sealed class NativeResources
    {
        private readonly ReadAccess _read;
        private MethodInfo? _localized;
        private bool _localizerSearched;
        private Type? _iconFactory;
        private bool _iconFactoryAttempted;
        private readonly Dictionary<string, object?> _icons = new Dictionary<string, object?>();
        public NativeResources(ReadAccess read) { _read = read; }

        public string Localize(string key)
        {
            if (string.IsNullOrEmpty(key)) return key;
            if (!_localizerSearched)
            {
                _localizerSearched = true;
                var matches = new List<MethodInfo>();
                foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    string n = a.GetName().Name ?? "";
                    if (n != "Assembly-CSharp" && n != "Comfort" && n != "CommonExtensions") continue;
                    foreach (Type t in ReadAccess.SafeTypes(a))
                    {
                        if (!t.IsAbstract || !t.IsSealed) continue;
                        foreach (MethodInfo m in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                        {
                            if (m.Name != "Localized" || m.ReturnType != typeof(string) || m.ContainsGenericParameters) continue;
                            ParameterInfo[] p = m.GetParameters();
                            if (p.Length >= 1 && p[0].ParameterType == typeof(string) && p.Skip(1).All(x => x.IsOptional)) matches.Add(m);
                        }
                    }
                }
                if (matches.Count == 1) { _localized = matches[0]; _read.Note("LABEL ADAPTER: " + _localized.DeclaringType?.FullName + "." + _localized); }
                else _read.Note("LABEL ADAPTER unresolved/ambiguous (" + matches.Count + "); raw names retained.");
            }
            if (_localized == null) return key;
            try
            {
                ParameterInfo[] p = _localized.GetParameters();
                var args = new object?[p.Length]; args[0] = key;
                for (int i = 1; i < p.Length; i++) args[i] = p[i].DefaultValue;
                return _localized.Invoke(null, args) as string ?? key;
            }
            catch (Exception e) { _read.Note("Localization failed: " + e.GetBaseException().GetType().Name); return key; }
        }
        public Sprite? Icon(object? item, string id)
        {
            if (item == null || string.IsNullOrEmpty(id)) return null;
            if (!_icons.TryGetValue(id, out object? wrapper))
            {
                if (_icons.Count >= 128) return null;
                if (!_iconFactoryAttempted)
                {
                    _iconFactoryAttempted = true;
                    _iconFactory = _read.UniqueGameType("ItemViewFactory");
                }
                if (_iconFactory == null) return null;
                List<MethodInfo> methods = ReadAccess.Methods(_iconFactory, "LoadItemIcon", true).Where(m => {
                    ParameterInfo[] p = m.GetParameters();
                    return !m.ContainsGenericParameters && p.Length >= 1 && p[0].ParameterType.IsInstanceOfType(item) && p.Skip(1).All(x => x.IsOptional);
                }).ToList();
                if (methods.Count != 1) { _read.Note("LoadItemIcon overload unresolved/ambiguous; no icon invocation."); _icons[id] = null; return null; }
                try
                {
                    MethodInfo m = methods[0]; ParameterInfo[] p = m.GetParameters();
                    var args = new object?[p.Length]; args[0] = item;
                    for (int i = 1; i < p.Length; i++) args[i] = p[i].DefaultValue;
                    wrapper = m.Invoke(null, args);
                    _read.Note("ICON ADAPTER: " + m.DeclaringType?.FullName + "." + m);
                }
                catch (Exception e) { _read.Note("Icon request failed: " + e.GetBaseException().GetType().Name); }
                _icons[id] = wrapper;
            }
            return wrapper as Sprite ?? _read.Get(wrapper, "Sprite") as Sprite;
        }
        // Shared game icon objects are NOT destroyed/disposed by this prototype.
        public void ClearSession() { _icons.Clear(); if (_iconFactory == null) _iconFactoryAttempted = false; }
    }
}
