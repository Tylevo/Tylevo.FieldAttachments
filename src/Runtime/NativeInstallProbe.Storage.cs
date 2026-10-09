using System;
using System.Linq;

namespace Tylevo.FieldAttachments.Runtime
{
    public sealed partial class NativeInstallProbe
    {
        private string _storageLocation="";
        private object? FindStorageAddress(StorageGrid[] grids, Type itemType)
        {
            Type gridType=NeedType("EFT.InventoryLogic.Grid");
            var find=Exact(gridType,"FindLocationForItem",false,new[] {itemType});
            if (find.ReturnType!=NeedType("EFT.InventoryLogic.GridItemAddress"))
                throw new InvalidOperationException("Native storage placement signature changed.");
            foreach (StorageGrid binding in grids)
            {
                object grid=binding.NativeGrid;
                _sourceGrid=grid; // Carried storage side of either direction; never the weapon slot.
                if (SourceContainsItem()!=false) throw new InvalidOperationException("Installed item also found in carried storage or contents unknown.");
                // Local IL verified: for an item absent from this grid, this only
                // checks native filters/space and creates an address; it does not move it.
                object? address=find.Invoke(grid,new[] {_item});
                if (address==null) continue;
                if (!ReferenceEquals(_read.Get(address,"Container"),grid))
                    throw new InvalidOperationException("Native placement returned a different container.");
                _storageLocation=binding.Location;
                return address;
            }
            return null;
        }
    }
}
