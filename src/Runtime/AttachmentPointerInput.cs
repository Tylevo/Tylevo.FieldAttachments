using System;
using System.Collections.Generic;
using EFT.InputSystem;
using Tylevo.FieldAttachments.Core;
using UnityEngine;

namespace Tylevo.FieldAttachments.Runtime
{
    // Same local PlayerOwner input-child pattern used by TSC's UavPhonePointerInputNode.
    // Native traversal/order and EAxis values verified against the installed 4.1.5 assembly.
    public sealed class AttachmentPointerInput : InputNode
    {
        private InputNode? _owner;
        private List<InputNode>? _ownerChildren;
        private ReadAccess? _read;
        public Func<bool>? Eligible, LocalOwnership;
        public Func<bool, bool>? DesiredCapture;
        public Action<string>? BeforeGameplayCommand;
        public KeyCode Modifier = KeyCode.LeftAlt;
        public readonly PointerCapture Capture = new PointerCapture();
        public string Fault { get; private set; } = "";
        public bool Registered => isActiveAndEnabled && _owner != null && _ownerChildren != null &&
            ReferenceEquals(_read?.Get(_owner, "_children"), _ownerChildren) && _ownerChildren.Contains(this);
        public bool Attach(object? player, ReadAccess read)
        {
            if (_owner != null || _ownerChildren != null) Detach();
            _read = read;
            if (!(player is Component component) || typeof(InputNode).Module.ModuleVersionId.ToString() != NativeInstallProbe.InspectedGameMvid)
                return false;
            Type? type = read.FindType("EFT.GamePlayerOwner");
            _owner = type == null ? null : component.GetComponent(type) as InputNode;
            _ownerChildren = read.Get(_owner, "_children") as List<InputNode>;
            if (_ownerChildren == null) { _owner = null; return false; }
            _ownerChildren.Add(this); enabled = true; return true;
        }
        public bool Refresh()
        {
            if (Fault.Length != 0) return false;
            try
            {
            bool own = LocalOwnership?.Invoke() == true;
            bool automatic = DesiredCapture?.Invoke(own) == true;
            Capture.Sample(Registered && own && Eligible?.Invoke() == true, automatic || Modifier != KeyCode.None && Input.GetKey(Modifier),
                Input.GetMouseButton(0) || Input.GetMouseButton(1), Application.isFocused, Time.frameCount, automatic);
            return own;
            }
            catch (Exception e) { Fault=e.GetBaseException().GetType().Name; Capture.Cancel(); return false; }
        }
        public override ETranslateResult TranslateCommand(ECommand command)
        {
            bool own = Refresh();
            string name = command.ToString();
            if (own && PointerInputPolicy.Block(name, Capture.Active, Capture.SuppressMouse)) return ETranslateResult.Block;
            // Restore our visual frame before EFT sees a real gameplay command.
            // No replay, synthesis, or alteration of that command's native handling.
            if (own && StandalonePoseInputPolicy.ShouldInterrupt(name))
            {
                try { BeforeGameplayCommand?.Invoke(name); }
                catch (Exception e) { Fault = e.GetBaseException().GetType().Name; Capture.Cancel(); }
            }
            return ETranslateResult.Ignore;
        }
        public override void TranslateAxes(ref float[] axes)
        { Refresh(); PointerInputPolicy.FilterAxes(axes, Capture.Active); }
        public override ECursorResult ShouldLockCursor()
        { Refresh(); return Capture.Active ? ECursorResult.LockCursor : ECursorResult.Ignore; }
        public void Detach()
        { Capture.Cancel(); _ownerChildren?.Remove(this); _ownerChildren = null; _owner = null; }
        private void OnDisable() => Detach();
        private void OnDestroy() => Detach();
    }
}
