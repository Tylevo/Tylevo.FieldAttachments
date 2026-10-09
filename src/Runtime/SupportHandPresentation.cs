using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tylevo.FieldAttachments.Runtime
{
    // The body skeleton remains native. Selected arm skinning references borrow
    // private visual bones for one rendered frame, in one coordinated renderer lease.
    public sealed class SupportHandPresentation
    {
        private sealed class Bone
        {
            public Transform Body = null!, Source = null!, BodyParent = null!, SourceParent = null!, Proxy = null!, ProxyParent = null!;
            public Transform BodyRoot = null!, SourceRoot = null!;
            public string Path = "";
        }
        private sealed class Binding
        {
            public SkinnedMeshRenderer Renderer = null!;
            public Mesh Mesh = null!;
            public Transform? Parent, RootBone;
            public Transform[] Original = Array.Empty<Transform>();
            public int[] Slots = Array.Empty<int>();
            public Bone[] Bones = Array.Empty<Bone>();
            public bool Touched;
        }

        private Binding[] _bindings = Array.Empty<Binding>();
        private Bone[] _bones = Array.Empty<Bone>();
        private Transform? _leftRoot, _rightRoot, _sourceRoot, _leftParent, _rightParent, _sourceParent, _ancestor;
        private Transform? _rightSourceRoot, _rightSourceParent;
        private GameObject? _proxyRoot, _rightProxyRoot;
        private bool _prepared, _frameActive, _everBound;
        private string _fault = "";
        // Kept as delegates so the isolated Unity fixture can inject setter/read-back failures.
        private Func<SkinnedMeshRenderer, Transform[]> _readBones = renderer => renderer.bones;
        private Action<SkinnedMeshRenderer, Transform[]> _writeBones = (renderer, bones) => renderer.bones = bones;

        public bool Prepared => _prepared;
        public bool FrameActive => _frameActive;
        public string Fault => _fault;
        public int RendererCount => _bindings.Length;
        public int BoneCount => _bones.Length;

        public void Prepare(SkinnedMeshRenderer[] renderers, Transform leftRoot, Transform rightRoot, Transform authoredLeftRoot)
            => Prepare(renderers, leftRoot, rightRoot, authoredLeftRoot, null);

        // The caller selects this route for audited weapons that need an authored firing grip.
        // Both arms share a single bones-array assignment and restoration boundary.
        public void Prepare(SkinnedMeshRenderer[] renderers, Transform leftRoot, Transform rightRoot, Transform authoredLeftRoot, Transform? authoredRightRoot)
        {
            if (_prepared || _proxyRoot != null || _rightProxyRoot != null || _frameActive) throw new InvalidOperationException("Support presentation is already prepared or still restoring");
            if (_fault.Length != 0) throw new InvalidOperationException("Support presentation fault remains: " + _fault);
            if (renderers == null || renderers.Length == 0 || leftRoot == null || rightRoot == null || authoredLeftRoot == null ||
                leftRoot.parent == null || rightRoot.parent == null || authoredLeftRoot.parent == null ||
                leftRoot == rightRoot || leftRoot.IsChildOf(rightRoot) || rightRoot.IsChildOf(leftRoot) ||
                authoredLeftRoot == leftRoot || authoredLeftRoot == rightRoot || authoredLeftRoot.IsChildOf(leftRoot) || authoredLeftRoot.IsChildOf(rightRoot) ||
                leftRoot.name != authoredLeftRoot.name)
                throw new InvalidOperationException("Support presentation roots are unavailable, overlapping or mismatched");
            if (authoredRightRoot != null && (authoredRightRoot.parent == null || authoredRightRoot.name != rightRoot.name ||
                authoredRightRoot == authoredLeftRoot || authoredRightRoot.IsChildOf(authoredLeftRoot) || authoredLeftRoot.IsChildOf(authoredRightRoot) ||
                authoredRightRoot == leftRoot || authoredRightRoot == rightRoot || authoredRightRoot.IsChildOf(leftRoot) || authoredRightRoot.IsChildOf(rightRoot) ||
                CommonAncestor(authoredLeftRoot, authoredRightRoot) == null))
                throw new InvalidOperationException("Authored firing and support arm roots are unavailable, overlapping or mismatched");
            Transform? ancestor = CommonAncestor(leftRoot, rightRoot);
            if (ancestor == null) throw new InvalidOperationException("Support and firing arm do not share a body ancestor");

            var bindings = new List<Binding>();
            var mapped = new Dictionary<Transform, Bone>();
            var seen = new HashSet<SkinnedMeshRenderer>();
            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                if (renderer == null || !seen.Add(renderer) || renderer.sharedMesh == null)
                    throw new InvalidOperationException("Support skin renderer is missing, duplicated or has no mesh");
                Transform[] original = _readBones(renderer);
                if (original == null || original.Length == 0) throw new InvalidOperationException("Support skin has no bone bindings");
                var slots = new List<int>();
                var selected = new List<Bone>();
                for (int i = 0; i < original.Length; i++)
                {
                    Transform body = original[i];
                    if (body == null) throw new InvalidOperationException("Support skin has a missing bone");
                    bool support = body == leftRoot || body.IsChildOf(leftRoot);
                    if (!support && (authoredRightRoot == null || (body != rightRoot && !body.IsChildOf(rightRoot)))) continue;
                    Transform bodyRoot = support ? leftRoot : rightRoot;
                    Transform sourceRoot = support ? authoredLeftRoot : authoredRightRoot!;
                    Transform cursor = body;
                    while (true)
                    {
                        if (!mapped.ContainsKey(cursor))
                        {
                            string path = RelativePath(bodyRoot, cursor);
                            Transform source = FindUnique(sourceRoot, path);
                            if (FindUnique(bodyRoot, path) != cursor || cursor.parent == null || source.parent == null ||
                                source == cursor || source == leftRoot || source.IsChildOf(leftRoot) || source == rightRoot || source.IsChildOf(rightRoot))
                                throw new InvalidOperationException("Support bone identity is ambiguous: " + path);
                            ValidatePose(cursor); ValidatePose(source);
                            mapped.Add(cursor, new Bone { Body = cursor, Source = source, BodyParent = cursor.parent, SourceParent = source.parent,
                                BodyRoot = bodyRoot, SourceRoot = sourceRoot, Path = path });
                        }
                        if (cursor == bodyRoot) break;
                        cursor = cursor.parent;
                    }
                    slots.Add(i); selected.Add(mapped[body]);
                }
                if (slots.Count != 0) bindings.Add(new Binding { Renderer = renderer, Mesh = renderer.sharedMesh,
                    Parent = renderer.transform.parent, RootBone = renderer.rootBone, Original = original,
                    Slots = slots.ToArray(), Bones = selected.ToArray() });
            }
            if (bindings.Count == 0) throw new InvalidOperationException("No skin bones belong to the verified support arm");
            if (!mapped.ContainsKey(leftRoot) || (authoredRightRoot != null && !mapped.ContainsKey(rightRoot)))
                throw new InvalidOperationException("A requested arm has no verified skin bindings");
            var ordered = new List<Bone>(mapped.Values);
            ordered.Sort((a, b) => { int depth = Depth(a.Path).CompareTo(Depth(b.Path)); return depth != 0 ? depth : string.CompareOrdinal(a.Path, b.Path); });

            try
            {
                _leftRoot = leftRoot; _rightRoot = rightRoot; _sourceRoot = authoredLeftRoot;
                _leftParent = leftRoot.parent; _rightParent = rightRoot.parent; _sourceParent = authoredLeftRoot.parent; _ancestor = ancestor;
                _rightSourceRoot = authoredRightRoot; _rightSourceParent = authoredRightRoot != null ? authoredRightRoot.parent : null;
                _bindings = bindings.ToArray(); _bones = ordered.ToArray();
                foreach (Bone bone in _bones)
                {
                    bool firing = bone.BodyRoot == rightRoot;
                    var node = new GameObject(bone.Path.Length == 0 ? (firing ? "FieldAttachments private firing arm" : "FieldAttachments private support arm") : bone.Body.name)
                        { hideFlags = HideFlags.HideAndDontSave };
                    if (bone.Path.Length == 0) { if (firing) _rightProxyRoot = node; else _proxyRoot = node; }
                    bone.Proxy = node.transform;
                    bone.Proxy.SetParent(bone.Path.Length == 0 ? bone.BodyParent : mapped[bone.BodyParent].Proxy, false);
                    bone.ProxyParent = bone.Proxy.parent;
                    bone.Proxy.localPosition = bone.Body.localPosition;
                    bone.Proxy.localRotation = bone.Body.localRotation;
                    bone.Proxy.localScale = bone.Body.localScale;
                }
                _prepared = true;
            }
            catch { Release(); throw; } // No renderer has been written during preparation.
        }

        public void Apply(float blend)
        {
            try
            {
                if (!_prepared || _frameActive || _fault.Length != 0 || !RootsMatch())
                    throw new InvalidOperationException("Support presentation is not ready or its roots changed");
                if (!Finite(blend) || blend < 0 || blend > 1) throw new InvalidOperationException("Support presentation blend is invalid");
                if (blend == 0) return;
                // Complete all identity/pose checks before the first live renderer write.
                foreach (Bone bone in _bones)
                {
                    if (bone.Body == null || bone.Source == null || bone.Proxy == null || bone.Body.parent != bone.BodyParent ||
                        bone.Source.parent != bone.SourceParent || bone.Proxy.parent != bone.ProxyParent ||
                        (bone.Body != bone.BodyRoot && !bone.Body.IsChildOf(bone.BodyRoot)) ||
                        (bone.Source != bone.SourceRoot && !bone.Source.IsChildOf(bone.SourceRoot)))
                        throw new InvalidOperationException("Support presentation bone changed: " + bone.Path);
                    ValidatePose(bone.Body); ValidatePose(bone.Source);
                }
                foreach (Binding binding in _bindings)
                {
                    if (!RendererMatches(binding) || !Same(_readBones(binding.Renderer), binding.Original))
                        throw new InvalidOperationException("Support skin mesh, hierarchy or native bone bindings changed");
                }
                foreach (Bone bone in _bones)
                {
                    bone.Proxy.localScale = bone.Body.localScale;
                    bone.Proxy.SetPositionAndRotation(Vector3.Lerp(bone.Body.position, bone.Source.position, blend),
                        Quaternion.Slerp(bone.Body.rotation, bone.Source.rotation, blend));
                    ValidatePose(bone.Proxy);
                }
                _frameActive = true;
                foreach (Binding binding in _bindings)
                {
                    Transform[] presented = (Transform[])binding.Original.Clone();
                    for (int i = 0; i < binding.Slots.Length; i++) presented[binding.Slots[i]] = binding.Bones[i].Proxy;
                    binding.Touched = true; // Setter failures can occur after native mutation.
                    _everBound = true;
                    _writeBones(binding.Renderer, presented);
                    if (!Same(_readBones(binding.Renderer), presented)) throw new InvalidOperationException("Support skin write read-back differs");
                }
            }
            catch (Exception e)
            {
                Latch("support presentation failed: " + e.GetBaseException().Message);
                Restore();
                throw;
            }
        }

        public bool Restore()
        {
            // Preparation never exposes proxies to a renderer. Failed preparation
            // must remain retryable when it acquired no native binding ownership.
            if (!_everBound) { _frameActive = false; return true; }
            bool pending = false;
            foreach (Binding binding in _bindings)
            {
                if (binding.Renderer == null)
                {
                    if (binding.Touched) Latch("support skin renderer was destroyed");
                    binding.Touched = false;
                    continue;
                }
                try
                {
                    Transform[] current = _readBones(binding.Renderer);
                    bool hasProxy = ContainsProxy(current);
                    if (!binding.Touched && !hasProxy) continue;
                    if (current.Length != binding.Original.Length || binding.Renderer.sharedMesh != binding.Mesh)
                    {
                        Latch("support skin mesh or bone array changed; external binding retained");
                        if (hasProxy) pending = true;
                        binding.Touched = hasProxy;
                        continue;
                    }
                    bool changed = false;
                    for (int i = 0; i < binding.Slots.Length; i++)
                    {
                        int slot = binding.Slots[i];
                        Transform proxy = binding.Bones[i].Proxy;
                        if (proxy != null && current[slot] == proxy)
                        {
                            if (binding.Original[slot] == null)
                            { Latch("support native bone destroyed; proxy retained"); pending = true; continue; }
                            current[slot] = binding.Original[slot]; changed = true;
                        }
                        else if (current[slot] != binding.Original[slot])
                            Latch("support skin slot " + slot + " changed externally; external binding retained");
                    }
                    if (changed) _writeBones(binding.Renderer, current);
                    Transform[] restored = _readBones(binding.Renderer);
                    binding.Touched = ContainsProxy(restored);
                    if (binding.Touched) { pending = true; Latch("support proxy still referenced; restoration pending"); }
                    if (!Same(restored, current)) Latch("support skin restore read-back differs; external binding retained");
                }
                catch (Exception e) { pending = true; Latch("support skin restoration pending: " + e.GetBaseException().Message); }
            }
            _frameActive = pending;
            return !pending;
        }

        public bool Release()
        {
            if (!Restore()) return false;
            try
            {
                // A private child may have been reparented by another component.
                // Keep its handle until it is no longer referenced, then dispose it too.
                foreach (Bone bone in _bones)
                    if (bone.Proxy != null && (_proxyRoot == null || !bone.Proxy.IsChildOf(_proxyRoot.transform)) &&
                        (_rightProxyRoot == null || !bone.Proxy.IsChildOf(_rightProxyRoot.transform)))
                    {
                        if (Application.isPlaying) UnityEngine.Object.Destroy(bone.Proxy.gameObject);
                        else UnityEngine.Object.DestroyImmediate(bone.Proxy.gameObject);
                    }
                if (_proxyRoot != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(_proxyRoot);
                    else UnityEngine.Object.DestroyImmediate(_proxyRoot);
                }
                if (_rightProxyRoot != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(_rightProxyRoot);
                    else UnityEngine.Object.DestroyImmediate(_rightProxyRoot);
                }
            }
            catch (Exception e) { Latch("private support arm cleanup failed: " + e.GetBaseException().Message); return false; }
            _prepared = _frameActive = _everBound = false; _proxyRoot = _rightProxyRoot = null;
            _bindings = Array.Empty<Binding>(); _bones = Array.Empty<Bone>();
            _leftRoot = _rightRoot = _sourceRoot = _leftParent = _rightParent = _sourceParent = _ancestor = null;
            _rightSourceRoot = _rightSourceParent = null;
            return true; // Ownership faults remain visible after successful cleanup.
        }

        private bool RootsMatch() => _proxyRoot != null && _leftRoot != null && _rightRoot != null && _sourceRoot != null &&
            _leftRoot.parent == _leftParent && _rightRoot.parent == _rightParent && _sourceRoot.parent == _sourceParent &&
            _ancestor != null && _leftRoot.IsChildOf(_ancestor) && _rightRoot.IsChildOf(_ancestor) &&
            _proxyRoot.transform.parent == _leftParent &&
            (_rightSourceRoot == null ? _rightProxyRoot == null : _rightProxyRoot != null &&
                _rightSourceRoot.parent == _rightSourceParent && _rightProxyRoot.transform.parent == _rightParent);
        private static bool RendererMatches(Binding binding) => binding.Renderer != null && binding.Renderer.sharedMesh == binding.Mesh &&
            binding.Renderer.transform.parent == binding.Parent && binding.Renderer.rootBone == binding.RootBone;
        private bool ContainsProxy(Transform[] values)
        {
            foreach (Transform value in values)
                foreach (Bone bone in _bones)
                    if (bone.Proxy != null && value == bone.Proxy) return true;
            return false;
        }
        private static bool Same(Transform[] a, Transform[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
        private static Transform? CommonAncestor(Transform a, Transform b)
        {
            for (Transform cursor = a.parent; cursor != null; cursor = cursor.parent)
                if (b.IsChildOf(cursor)) return cursor;
            return null;
        }
        private static string RelativePath(Transform root, Transform value)
        {
            var names = new List<string>();
            for (Transform cursor = value; cursor != root; cursor = cursor.parent)
            {
                if (cursor == null) throw new InvalidOperationException("Support bone left its root");
                names.Add(cursor.name);
            }
            names.Reverse(); return string.Join("/", names);
        }
        private static Transform FindUnique(Transform root, string path)
        {
            Transform current = root;
            if (path.Length == 0) return current;
            foreach (string part in path.Split('/'))
            {
                Transform? match = null;
                for (int i = 0; i < current.childCount; i++)
                {
                    Transform child = current.GetChild(i);
                    if (child.name != part) continue;
                    if (match != null) throw new InvalidOperationException("Support bone path is duplicated: " + path);
                    match = child;
                }
                if (match == null) throw new InvalidOperationException("Authored support bone is missing: " + path);
                current = match;
            }
            return current;
        }
        private static int Depth(string path) { int depth = path.Length == 0 ? 0 : 1; foreach (char c in path) if (c == '/') depth++; return depth; }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static void ValidatePose(Transform value)
        {
            Quaternion q = value.rotation;
            double norm = (double)q.x * q.x + (double)q.y * q.y + (double)q.z * q.z + (double)q.w * q.w;
            if (!Finite(value.position) || !Finite(value.localScale) || !Finite(q.x) || !Finite(q.y) || !Finite(q.z) || !Finite(q.w) ||
                Math.Abs(norm - 1) > .001 || value.localScale.x == 0 || value.localScale.y == 0 || value.localScale.z == 0)
                throw new InvalidOperationException("Support bone pose is not finite and invertible");
        }
        private void Latch(string reason) { if (_fault.Length == 0) _fault = reason; }
    }
}
