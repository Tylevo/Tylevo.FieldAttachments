using System;
using System.Collections;
using System.Collections.Generic;
using Tylevo.FieldAttachments.Core;
using UnityEngine;

namespace Tylevo.FieldAttachments.Runtime
{
    public sealed partial class InspectionPose
    {
        private readonly SupportHandPresentation _supportPresentation = new SupportHandPresentation();
        private Component? _supportPlayerBody, _supportSkeleton;
        private object? _supportSkin;
        private object[] _supportLods = Array.Empty<object>();
        private SkinnedMeshRenderer[] _supportRenderers = Array.Empty<SkinnedMeshRenderer>();
        private Transform? _supportLeft, _supportRight;
        private bool _authoredFiringHand;

        private object? HandsSkin(object? body)
        {
            if (!(_read.Get(body, "BodySkins") is IDictionary skins)) return null;
            foreach (DictionaryEntry entry in skins)
                if (ReadAccess.Text(entry.Key) == "Hands") return entry.Value;
            return null;
        }

        private void PrepareSupportPresentation(RaidSnapshot snapshot, Animator animator)
        {
            Component? player = snapshot.Player as Component;
            Component? body = _read.Get(snapshot.Player, "PlayerBody", "_playerBody") as Component;
            Component? skeleton = _read.Get(body, "SkeletonHands") as Component;
            object? bones = _read.Get(body, "PlayerBones");
            Transform[]? shoulders = _read.Get(bones, "Shoulders") as Transform[];
            object? skin = HandsSkin(body);
            Array? lods = _read.Get(skin, "_lods") as Array;
            Transform authored = animator.transform.Find("Base HumanLCollarbone/Base HumanLUpperarm");
            if (player == null || body == null || skeleton == null ||
                !body.transform.IsChildOf(player.transform) || !skeleton.transform.IsChildOf(body.transform) ||
                shoulders == null || shoulders.Length != 2 || shoulders[0] == null || shoulders[1] == null ||
                shoulders[0].name != "Base HumanLUpperarm" || shoulders[1].name != "Base HumanRUpperarm" ||
                !shoulders[0].IsChildOf(skeleton.transform) || !shoulders[1].IsChildOf(skeleton.transform) ||
                authored == null || skin == null || lods == null || lods.Length < 1 || lods.Length > 8)
                throw new InvalidOperationException("visible support arm/Hands skin binding unavailable");
            Transform palm = shoulders[0].Find("Base HumanLForearm1/Base HumanLForearm2/Base HumanLForearm3/Base HumanLPalm");
            if (palm == null || !ReferenceEquals(_read.Get(bones, "LeftPalm"), palm))
                throw new InvalidOperationException("visible support palm binding changed");
            var renderers = new List<SkinnedMeshRenderer>();
            var bindings = new List<object>();
            foreach (object lod in lods)
            {
                SkinnedMeshRenderer? renderer = _read.Get(lod, "_skinnedMeshRenderer") as SkinnedMeshRenderer;
                if (lod == null || !ReferenceEquals(_read.Get(lod, "_skeleton"), skeleton) ||
                    renderer == null || !renderer.transform.IsChildOf(body.transform) || renderers.Contains(renderer))
                    throw new InvalidOperationException("visible Hands skin LOD binding changed");
                bindings.Add(lod); renderers.Add(renderer);
            }
            _supportPlayerBody = body; _supportSkeleton = skeleton; _supportSkin = skin;
            _supportLods = bindings.ToArray(); _supportRenderers = renderers.ToArray();
            _supportLeft = shoulders[0]; _supportRight = shoulders[1];
            _authoredFiringHand = FiringHandPresentation.RequiresFiringHand(_template);
            if (_authoredFiringHand)
            {
                Transform authoredRight = animator.transform.Find("Base HumanRCollarbone/Base HumanRUpperarm");
                Transform rightPalm = shoulders[1].Find("Base HumanRForearm1/Base HumanRForearm2/Base HumanRForearm3/Base HumanRPalm");
                if (authoredRight == null || rightPalm == null || !ReferenceEquals(_read.Get(bones, "RightPalm"), rightPalm))
                    throw new InvalidOperationException("visible authored firing-hand binding unavailable");
                _supportPresentation.Prepare(_supportRenderers, _supportLeft, _supportRight, authored, authoredRight);
            }
            else _supportPresentation.Prepare(_supportRenderers, _supportLeft, _supportRight, authored);
        }

        private string SupportPresentationDenial()
        {
            if (!_supportPresentation.Prepared) return "";
            if (_supportPlayerBody == null || _supportSkeleton == null || _supportLeft == null || _supportRight == null ||
                !ReferenceEquals(_read.Get(_context?.Player, "PlayerBody", "_playerBody"), _supportPlayerBody) ||
                !ReferenceEquals(_read.Get(_supportPlayerBody, "SkeletonHands"), _supportSkeleton) ||
                !ReferenceEquals(HandsSkin(_supportPlayerBody), _supportSkin)) return "visible Hands skin context changed";
            object? bones = _read.Get(_supportPlayerBody, "PlayerBones");
            Transform[]? shoulders = _read.Get(bones, "Shoulders") as Transform[];
            Array? lods = _read.Get(_supportSkin, "_lods") as Array;
            if (shoulders == null || shoulders.Length != 2 || shoulders[0] != _supportLeft || shoulders[1] != _supportRight ||
                lods == null || lods.Length != _supportLods.Length) return "visible support arm binding changed";
            for (int i = 0; i < _supportLods.Length; i++)
                if (!ReferenceEquals(lods.GetValue(i), _supportLods[i]) ||
                    !ReferenceEquals(_read.Get(_supportLods[i], "_skeleton"), _supportSkeleton) ||
                    !ReferenceEquals(_read.Get(_supportLods[i], "_skinnedMeshRenderer"), _supportRenderers[i]))
                    return "visible Hands skin LOD context changed";
            return "";
        }

        private void ClearSupportPresentationBinding()
        {
            _supportPlayerBody = _supportSkeleton = null; _supportSkin = null;
            _supportLods = Array.Empty<object>(); _supportRenderers = Array.Empty<SkinnedMeshRenderer>();
            _supportLeft = _supportRight = null;
            _authoredFiringHand = false;
        }
    }
}
