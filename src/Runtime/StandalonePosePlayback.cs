using System;
using System.Collections.Generic;
using System.Globalization;
using Tylevo.FieldAttachments.Core;
using UnityEngine;

namespace Tylevo.FieldAttachments.Runtime
{
    // Samples our clip on a private skeleton and Animator. The live animator and its native
    // controller, operation, parameters and clock remain owned by the game.
    public sealed class StandalonePosePlayback
    {
        private GameObject? _shadow;
        private Animator? _shadowAnimator;
        private Animator? _animator;
        private Transform? _root;
        private AnimationClip? _clip;
        private Transform[] _live = Array.Empty<Transform>(), _parents = Array.Empty<Transform>(), _sampled = Array.Empty<Transform>();
        private Vector4[] _values = Array.Empty<Vector4>();
        private PoseFrameLease<Vector4>? _frame;
        private string _fault = "";
        private bool _prepared, _hasSample;
        private float _sampleTime;

        public bool Prepared => _prepared;
        public bool FrameActive => _frame?.Active == true;
        public string Fault => _fault.Length != 0 ? _fault : _frame?.Fault ?? "";
        // Number of transforms; each has independently owned position and rotation.
        public int BindingCount => _live.Length;

        public void Prepare(Animator animator, AnimationClip clip, string template)
        {
            if (_prepared || _shadow != null || FrameActive) throw new InvalidOperationException("Standalone presentation is already prepared or still restoring");
            if (Fault.Length != 0) throw new InvalidOperationException("Standalone presentation fault remains: " + Fault);
            if (animator == null || clip == null || animator.runtimeAnimatorController == null ||
                !CustomStmInspection.ControllerMatches(template, animator.runtimeAnimatorController.name) ||
                !CustomStmInspection.ValidClip(template, clip.name, clip.length, clip.legacy, clip.humanMotion, clip.events.Length))
                throw new InvalidOperationException("Standalone presentation recipient, controller or authored clip changed");

            var paths = new List<string>();
            var live = new List<Transform>();
            foreach (string path in GlobalMcxInspection.RequiredRigPaths)
            {
                if (path == "Camera_animated") continue;
                // The pinned TT Gold clip has no left-hand marker curves. Leave
                // that marker native even if another mod adds it to the rig.
                if (template == "5b3b713c5acfc4330140bd8d" && path == CustomStmInspection.LeftHandMarker) continue;
                Transform bone = animator.transform.Find(path);
                if (bone == null || bone.parent == null || !Finite(bone.localPosition) || !ValidRotation(bone.localRotation) || !Finite(bone.localScale))
                    throw new InvalidOperationException("Standalone presentation rig binding unavailable: " + path);
                paths.Add(path); live.Add(bone);
            }
            try
            {
                _animator = animator; _root = animator.transform; _clip = clip;
                _shadow = new GameObject("FieldAttachments private presentation sampler") { hideFlags = HideFlags.HideAndDontSave };
                var nodes = new Dictionary<string, Transform>(StringComparer.Ordinal) { [""] = _shadow.transform };
                _live = live.ToArray(); _parents = new Transform[_live.Length]; _sampled = new Transform[_live.Length];
                for (int i = 0; i < _live.Length; i++)
                {
                    string prefix = "";
                    Transform parent = _shadow.transform;
                    foreach (string part in paths[i].Split('/'))
                    {
                        prefix = prefix.Length == 0 ? part : prefix + "/" + part;
                        if (!nodes.TryGetValue(prefix, out Transform child))
                        {
                            var node = new GameObject(part) { hideFlags = HideFlags.HideAndDontSave };
                            child = node.transform; child.SetParent(parent, false); nodes.Add(prefix, child);
                        }
                        parent = child;
                    }
                    _parents[i] = _live[i].parent; _sampled[i] = parent;
                    parent.localPosition = _live[i].localPosition;
                    parent.localRotation = _live[i].localRotation;
                    parent.localScale = _live[i].localScale; // Private skeleton only; no live scale write.
                }
                // Compiled non-legacy clips require an Animator for SampleAnimation.
                // This component belongs only to our private skeleton; it has no
                // controller or avatar and never drives the live game hierarchy.
                _shadowAnimator = _shadow.AddComponent<Animator>();
                _shadowAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                _shadowAnimator.applyRootMotion = false;
                _values = new Vector4[_live.Length * 2];
                _frame = new PoseFrameLease<Vector4>(_values.Length,
                    i => i % 2 == 0 ? Position(_live[i / 2].localPosition) : Rotation(_live[i / 2].localRotation),
                    (i, value) => {
                        if (i % 2 == 0) _live[i / 2].localPosition = new Vector3(value.x, value.y, value.z);
                        else _live[i / 2].localRotation = new Quaternion(value.x, value.y, value.z, value.w);
                    }, i => BindingExists(i / 2),
                    (i, a, b) => i % 2 == 0 ? a.Equals(b) : StmPresentation.RestoredRotation(
                        new CardVector(a.x, a.y, a.z), a.w, new CardVector(b.x, b.y, b.z), b.w),
                    Describe);
                _hasSample = false; _prepared = true;
            }
            catch
            {
                // No live pose has been written during preparation. Preserve any
                // private cleanup failure and its handles instead of hiding it.
                Release();
                throw;
            }
        }

        public void Apply(float seconds, float blend)
        {
            try
            {
                if (!_prepared || _shadow == null || _shadowAnimator == null || _clip == null || _frame == null || Fault.Length != 0)
                    throw new InvalidOperationException("Standalone presentation is not prepared or is faulted");
                if (FrameActive) throw new InvalidOperationException("Previous standalone presentation frame has not been restored");
                if (!PoseMarker.Finite(seconds) || seconds < 0 || seconds > _clip.length ||
                    !PoseMarker.Finite(blend) || blend < 0 || blend > 1)
                    throw new InvalidOperationException("Standalone presentation time or blend is invalid");
                if (blend == 0) return;
                if (!_hasSample || seconds != _sampleTime)
                {
                    _clip.SampleAnimation(_shadow, seconds);
                    _sampleTime = seconds; _hasSample = true;
                }
                // Validate the entire sample and current native pose before any
                // live write. Blend from this frame, never yesterday's overlay.
                for (int i = 0; i < _live.Length; i++)
                {
                    if (!BindingExists(i) || _sampled[i] == null)
                        throw new InvalidOperationException("Standalone presentation binding changed at " + i);
                    Vector3 nativePosition = _live[i].localPosition, samplePosition = _sampled[i].localPosition;
                    Quaternion nativeRotation = _live[i].localRotation, sampleRotation = _sampled[i].localRotation;
                    if (!Finite(nativePosition) || !Finite(samplePosition) || !ValidRotation(nativeRotation) || !ValidRotation(sampleRotation))
                        throw new InvalidOperationException("Standalone presentation pose is invalid at " + i);
                    Vector3 position = Vector3.Lerp(nativePosition, samplePosition, blend);
                    Quaternion rotation = Quaternion.Slerp(nativeRotation, sampleRotation, blend);
                    if (!Finite(position) || !ValidRotation(rotation)) throw new InvalidOperationException("Standalone blended pose is invalid at " + i);
                    _values[i * 2] = Position(position); _values[i * 2 + 1] = Rotation(rotation);
                }
                _frame.Apply(_values);
            }
            catch (Exception e)
            {
                Latch("standalone frame failed: " + e.GetBaseException().Message);
                Restore();
                throw;
            }
        }

        public bool Restore()
        {
            if (_frame == null) return true;
            bool restored = _frame.Restore();
            if (_frame.Fault.Length != 0) Latch(_frame.Fault);
            return restored;
        }

        public bool Release()
        {
            if (!Restore()) return false;
            try
            {
                // Only the private hierarchy and its Animator belong to this component.
                // The clip is cached elsewhere and every live reference is borrowed.
                if (_shadow != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(_shadow);
                    else UnityEngine.Object.DestroyImmediate(_shadow);
                }
            }
            catch (Exception e) { Latch("private sampler cleanup failed: " + e.GetBaseException().Message); return false; }
            _prepared = _hasSample = false; _shadow = null; _shadowAnimator = null; _animator = null; _root = null; _clip = null; _frame = null;
            _live = _parents = _sampled = Array.Empty<Transform>(); _values = Array.Empty<Vector4>();
            return true; // Fault remains visible even after all ownership is released.
        }

        private bool BindingExists(int i) => _animator != null && _root != null && _animator.transform == _root &&
            _live[i] != null && _live[i].parent == _parents[i] && _live[i].IsChildOf(_root);
        private void Latch(string reason) { if (_fault.Length == 0) _fault = reason; }
        private static Vector4 Position(Vector3 value) => new Vector4(value.x, value.y, value.z, 0);
        private static Vector4 Rotation(Quaternion value) => new Vector4(value.x, value.y, value.z, value.w);
        private static bool Finite(Vector3 value) => PoseMarker.Finite(value.x) && PoseMarker.Finite(value.y) && PoseMarker.Finite(value.z);
        private static bool ValidRotation(Quaternion value) => Finite(new Vector3(value.x, value.y, value.z)) && PoseMarker.Finite(value.w) &&
            Math.Abs((double)value.x * value.x + (double)value.y * value.y + (double)value.z * value.z + (double)value.w * value.w - 1) <= .001;
        private static string Describe(Vector4 value) => "(" + value.x.ToString("R", CultureInfo.InvariantCulture) + "," +
            value.y.ToString("R", CultureInfo.InvariantCulture) + "," + value.z.ToString("R", CultureInfo.InvariantCulture) + "," +
            value.w.ToString("R", CultureInfo.InvariantCulture) + ")";
    }
}
