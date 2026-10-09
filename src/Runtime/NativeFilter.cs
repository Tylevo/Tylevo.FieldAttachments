using System;
using System.Linq;
using System.Reflection;

namespace Tylevo.FieldAttachments.Runtime
{
    // This predicate was identified in the user's exported 4.1.5 IL. It is NOT
    // Move/Swap(simulate), does not authorize an inventory transaction, and does
    // not test slot occupancy, conflicts elsewhere, grid space or hands state.
    public sealed class NativeFilter
    {
        private readonly ReadAccess _read;
        private MethodInfo? _method;
        public NativeFilter(ReadAccess read) { _read = read; }
        public bool? Accepts(object? slot, object? item)
        {
            if (slot == null || item == null) return null;
            object? filters = _read.Get(slot, "Filters");
            if (filters == null) { _read.Note("Native filter: Filters unavailable; no acceptance inferred."); return null; }
            try
            {
                if (_method == null)
                {
                    Type? type = _read.FindType("EFT.InventoryLogic.ItemFilterExtension");
                    Type? itemType = _read.FindType("EFT.InventoryLogic.Item");
                    Type? filterType = _read.FindType("EFT.InventoryLogic.ItemFilter");
                    if (type == null || itemType == null || filterType == null) return null;
                    MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                        .Where(m => m.Name == "CheckItemFilter" && !m.ContainsGenericParameters && m.ReturnType == typeof(bool) &&
                            m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType == filterType.MakeArrayType() &&
                            m.GetParameters()[1].ParameterType == itemType).ToArray();
                    if (methods.Length != 1) { _read.Note("Native filter: expected exact CheckItemFilter(ItemFilter[], Item), matches=" + methods.Length); return null; }
                    _method = methods[0];
                    _read.Note("NATIVE FILTER: " + _method.DeclaringType?.FullName + "." + _method);
                }
                if (!_method.GetParameters()[0].ParameterType.IsInstanceOfType(filters) ||
                    !_method.GetParameters()[1].ParameterType.IsInstanceOfType(item)) return null;
                return _method.Invoke(null, new[] { filters, item }) is bool accepted ? accepted : (bool?)null;
            }
            catch (Exception e) { _read.Note("Native filter failed: " + e.GetBaseException().Message); return null; }
        }
    }
}
