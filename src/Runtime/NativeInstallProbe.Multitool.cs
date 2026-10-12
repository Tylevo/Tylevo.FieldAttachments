using System;
using System.Linq;
using System.Reflection;
using Tylevo.FieldAttachments.Core;

namespace Tylevo.FieldAttachments.Runtime
{
    public sealed partial class NativeInstallProbe
    {
        private NativeToolMoveBinding? _toolBinding;
        private RaidSnapshot? _toolSnapshot;
        private MultitoolAccess? _toolExpected;
        private object? _toolHands, _toolSlotParent;
        private MethodInfo? _toolExamined;

        private void PrepareToolScope(RaidSnapshot snapshot,object? hands,AttachmentGroup group,Type itemType)
        {
            if(!AttachmentLayers.IsAdvanced(group) || !NativeToolMoveScope.Ready ||
                !snapshot.MultitoolScanComplete || snapshot.Multitool==null || hands==null ||
                _item==null || _slot==null || _sourceAddress==null || _destinationAddress==null || snapshot.Controller==null)
                throw new InvalidOperationException("Advanced attachment authorization unavailable.");
            _toolSnapshot=snapshot; _toolExpected=snapshot.Multitool; _toolHands=hands;
            _toolSlotParent=_read.Get(_slot,"ParentItem");
            _toolExamined=Exact(snapshot.Controller.GetType(),"Examined",false,new[]{itemType});
            if(_toolSlotParent==null || _toolExamined.ReturnType!=typeof(bool))
                throw new InvalidOperationException("Advanced attachment ownership/examination unavailable.");
            _toolBinding=new NativeToolMoveBinding(_item,snapshot.Controller,_slot,_sourceAddress,_destinationAddress,
                ValidateToolMove,requiredReplacement:_toolRequiredReplacement!=null);
            if(!_toolBinding.Validate(false)) throw new InvalidOperationException(_toolBinding.Denial);
        }

        private string ValidateToolMove()
        {
            var snapshot=_toolSnapshot;
            if(snapshot==null || _toolExpected==null || !NativeToolMoveScope.Ready ||
                !ReadContext(snapshot).Allowed || !ReferenceEquals(_read.Get(snapshot.Player,"HandsController"),_toolHands))
                return "Local player, weapon, hands or session changed.";
            if(_item==null || _slot==null || !OriginalLayoutIntact() ||
                !ReferenceEquals(_read.Get(_slot,"ParentItem"),_toolSlotParent) ||
                !ReferenceEquals(_read.Get(_item,"Owner"),snapshot.Controller) ||
                !_read.Items(_read.Get(snapshot.Weapon,"AllSlots"),256).Any(slot=>ReferenceEquals(slot,_slot)))
                return "Advanced attachment identity, source or mounting point changed.";
            bool? required=ReadAccess.Bool(_read.Get(_slot,"Required"));
            if((required!=false && !(required==true && _toolRequiredReplacement!=null)) ||
                ReadAccess.Bool(_read.Get(_slot,"Locked"))!=false ||
                ReadAccess.Bool(_read.Get(_slot,"Deleted"))!=false ||
                !TryInt(_read.Get(_item,"PinLockState"),out int pin) || pin!=0 ||
                ReadAccess.Bool(_read.Get(_item,"RaidModdable"))==null ||
                !Equals(_toolExamined?.Invoke(snapshot.Controller,new[]{_item}),true) ||
                _filter.Accepts(_slot,_item)!=true)
                return "Advanced attachment restrictions changed.";
            var storage=new CarriedStorageReader(_read).Capture(snapshot.Controller);
            if(!storage.Complete || !storage.Grids.Any(g=>ReferenceEquals(g.NativeGrid,_sourceGrid) && g.Location==_storageLocation))
                return "Bound carried storage is no longer accessible.";
            if(_toolRequiredReplacement!=null)
            {
                string replacement=ValidateRequiredReplacement(storage);
                if(replacement.Length!=0) return replacement;
            }
            // Native OutProcess may be executing its own AddMod/RemoveMod operation
            // here. Do not reapply the pre-submit idle/active-event test to that phase.
            // The native hands CanExecute/OutProcess path remains authoritative.
            return new MultitoolReader(_read).Validate(snapshot.Controller,_toolExpected);
        }

        public void RevokeToolScope()
        {
            _requiredReplacement=null;
            RevokeActiveToolScope();
        }

        private void RevokeActiveToolScope()
        {
            _toolBinding?.Revoke(); _toolBinding=null;
            _toolSnapshot=null; _toolExpected=null; _toolHands=null; _toolSlotParent=null; _toolExamined=null;
            _toolRequiredReplacement=null;
        }
    }
}
