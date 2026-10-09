using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Tylevo.FieldAttachments.Runtime
{
    public sealed class ReadAccess
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        private readonly Dictionary<string, MemberInfo?> _members = new Dictionary<string, MemberInfo?>();
        private readonly Dictionary<string, Type?> _types = new Dictionary<string, Type?>();
        private readonly HashSet<string> _seen = new HashSet<string>();
        public readonly List<string> Evidence = new List<string>();

        public void Note(string text) { if (_seen.Add(text) && Evidence.Count < 1200) Evidence.Add(text); }
        public Type? FindType(string fullName)
        {
            if (_types.TryGetValue(fullName, out Type? cached) && cached != null) return cached;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type? type = assembly.GetType(fullName, false);
                if (type != null) { _types[fullName] = type; return type; }
            }
            return null; // Do not cache absence: EFT/Comfort may load later.
        }
        public Type? UniqueGameType(string shortName)
        {
            string key = "short:" + shortName;
            if (_types.TryGetValue(key, out Type? cached) && cached != null) return cached;
            var hits = new List<Type>();
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = assembly.GetName().Name ?? "";
                if (name != "Assembly-CSharp" && name != "ItemComponent.Types" && name != "Comfort" && name != "Comfort.Unity") continue;
                foreach (Type type in SafeTypes(assembly)) if (type.Name == shortName) hits.Add(type);
            }
            if (hits.Count != 1) { Note("Type lookup " + shortName + ": " + hits.Count + " matches; not guessing."); return null; }
            _types[key] = hits[0];
            Note("Resolved " + shortName + " => " + hits[0].FullName);
            return hits[0];
        }
        public static IEnumerable<Type> SafeTypes(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null).Cast<Type>(); }
            catch { return Array.Empty<Type>(); }
        }
        public object? Get(object? target, params string[] names)
        {
            if (target == null) return null;
            Type type = target as Type ?? target.GetType();
            bool isStatic = target is Type;
            foreach (string name in names)
            {
                string key = type.AssemblyQualifiedName + "|" + name + "|" + isStatic;
                if (!_members.TryGetValue(key, out MemberInfo? member))
                {
                    for (Type? t = type; t != null && member == null; t = t.BaseType)
                    {
                        PropertyInfo? p = t.GetProperty(name, Flags);
                        if (p != null && p.GetIndexParameters().Length == 0 && p.GetGetMethod(true)?.IsStatic == isStatic) member = p;
                        FieldInfo? f = t.GetField(name, Flags);
                        if (member == null && f != null && f.IsStatic == isStatic) member = f;
                    }
                    _members[key] = member;
                    Note((member == null ? "MISSING " : "READ ") + type.FullName + "." + name);
                }
                if (member == null) continue;
                try
                {
                    object? value = member is PropertyInfo property ? property.GetValue(isStatic ? null : target, null) : ((FieldInfo)member).GetValue(isStatic ? null : target);
                    if (value != null) return value;
                }
                catch (Exception e) { Note("Getter failed " + type.FullName + "." + name + ": " + e.GetBaseException().GetType().Name); }
            }
            return null;
        }
        public IEnumerable<object> Items(object? value, int limit = 2048)
        {
            if (!(value is IEnumerable sequence) || value is string) yield break;
            IEnumerator? enumerator = null;
            try { enumerator = sequence.GetEnumerator(); } catch { }
            if (enumerator == null) yield break;
            try
            {
                for (int i = 0; i < limit; i++)
                {
                    object? current = null;
                    bool next;
                    try { next = enumerator.MoveNext(); if (next) current = enumerator.Current; }
                    catch { next = false; Note("Enumeration changed or failed; snapshot may be partial."); }
                    if (!next) yield break;
                    if (current != null) yield return current;
                }
                Note("Enumeration cap reached; report is partial.");
            }
            finally { (enumerator as IDisposable)?.Dispose(); }
        }
        public static string Text(object? value) { return value?.ToString() ?? ""; }
        public static bool? Bool(object? value) { return value is bool b ? b : (bool?)null; }
        public static bool IsKind(object? value, string fullName)
        {
            if (value == null) return false;
            for (Type? type = value.GetType(); type != null; type = type.BaseType)
                if (type.FullName == fullName) return true;
            return false;
        }
        public static List<MethodInfo> Methods(Type type, string name, bool? isStatic = null)
        {
            return type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                .Where(m => m.Name == name && (!isStatic.HasValue || m.IsStatic == isStatic.Value)).ToList();
        }
    }
}
