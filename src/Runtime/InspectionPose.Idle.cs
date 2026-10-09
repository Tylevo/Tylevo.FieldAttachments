using Tylevo.FieldAttachments.Core;
using UnityEngine;

namespace Tylevo.FieldAttachments.Runtime
{
    public sealed partial class InspectionPose
    {
        // Used by both Start and its one-shot post-transaction scheduler. Native Idling
        // alone can precede the end of the visible attachment-change animation.
        private int StableIdleHash(object? hands, Animator animator)
        {
            string name = ReadAccess.Text(_read.Get(_read.Get(hands, "FirearmsAnimator"), "FullIdleStateName"));
            int hash = name.Length == 0 ? 0 : Animator.StringToHash(name);
            return hash != 0 && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null &&
                animator.layerCount > 1 && animator.GetLayerName(1) == "Hands" &&
                !animator.IsInTransition(1) && animator.HasState(1, hash) &&
                animator.GetCurrentAnimatorStateInfo(1).fullPathHash == hash &&
                PoseMarker.Finite(animator.speed) && animator.speed > 0 ? hash : 0;
        }
        public bool VisibleIdle(RaidSnapshot snapshot)
        {
            object? hands = _read.Get(snapshot.Player, "HandsController");
            Animator? animator = ResolveAnimator(hands);
            return animator != null && ReferenceEquals(_read.Get(hands, "Item"), snapshot.Weapon) &&
                StableIdleHash(hands, animator) != 0;
        }
    }
}
