using System;
using System.Collections;
using System.Collections.Generic;
using Tylevo.FieldAttachments.Core;
using UnityEngine;

namespace Tylevo.FieldAttachments.Runtime
{
    // Read-only adapter verified against the local 4.1.5 assembly and held M4 prefab.
    // Never addresses an animator, changes a transform, or calls an inventory method.
    public sealed class WeaponAnchorReader
    {
        private readonly ReadAccess _read;
        private object? _player, _weapon, _hands;
        private Component? _prefab;
        private Camera? _camera;
        private Transform? _frame;
        private Transform? _muzzle, _boreStart, _topReference;
        private bool _topIsBelow;
        private Vector3 _lastBore, _lastRight, _lastUp, _lastCardRight, _lastCardUp;
        private Vector2 _lastHomeAnchor;
        private string _planeReason = "Not bound";
        private readonly Dictionary<object, Transform> _bones = new Dictionary<object, Transform>();
        private readonly HashSet<object> _ambiguous = new HashSet<object>();
        public string Status { get; private set; } = "No weapon bound";
        public string CameraName => _camera != null ? _camera.name : "unavailable";
        public string FrameName => _frame != null ? "Weapon_root/Weapon_root_anim/weapon" : "unavailable";
        public string PlaneStatus => _planeReason + "; bore=" + _lastBore.ToString("F4") + "; native-right=" + _lastRight.ToString("F4") +
            "; native-up=" + _lastUp.ToString("F4") + "; card-right=" + _lastCardRight.ToString("F4") +
            "; card-up=" + _lastCardUp.ToString("F4") + "; home-anchor=" + _lastHomeAnchor.ToString("F1") +
            (_muzzle != null && _muzzle.gameObject.activeInHierarchy ? " (receiver/muzzle midpoint)" : " (receiver)") + "; start=" + BoneName(_boreStart) + "; muzzle=" + BoneName(_muzzle) +
            "; top=" + BoneName(_topReference) + (_topIsBelow ? " (below receiver)" : " (above receiver)");
        private static string BoneName(Transform? bone) => bone != null ? bone.name : "unavailable";

        public WeaponAnchorReader(ReadAccess read) { _read = read; }

        internal static Camera? ResolveCamera(ReadAccess read)
        {
            // Backing singleton field only: Instance's getter can create a manager.
            object? manager = read.Get(read.FindType("EFT.CameraControl.CameraManager"), "instance");
            Camera? camera = read.Get(manager, "_camera") as Camera;
            return camera != null && camera.name == "FPS Camera" ? camera : null;
        }

        public void Bind(RaidSnapshot snapshot)
        {
            Clear();
            _player = snapshot.Player; _weapon = snapshot.Weapon;
            if (_player == null || _weapon == null) return;
            try
            {
                _hands = _read.Get(_player, "_handsController");
                _prefab = _read.Get(_hands, "_weaponPrefab") as Component;
                if (_prefab == null || !ReferenceEquals(_read.Get(_hands, "_item"), _weapon) ||
                    !ReferenceEquals(_read.Get(_prefab, "_weaponData"), _weapon))
                { Status = "Held weapon/prefab identity unavailable"; return; }
                // Read the backing singleton field: Instance's getter can create a manager.
                _camera = ResolveCamera(_read);
                if (_camera == null || _camera.name != "FPS Camera")
                { _camera = null; Status = "Verified FPS camera unavailable"; return; }
                // Local bridge verified this field/path on WeaponPrefab. Read only the animated
                // weapon bone, not the container or a globally searched lookalike.
                Transform? root = _read.Get(_prefab, "_localWeaponRoot") as Transform;
                _frame = root != null && root.IsChildOf(_prefab.transform) ? root.Find("Weapon_root_anim/weapon") : null;
                Type? poolType = _read.FindType("EFT.AssetsManager.AssetPoolObject");
                if (poolType == null) { Status = "AssetPoolObject type unavailable"; return; }
                Component[] owners = _prefab.GetComponentsInChildren(poolType, false);
                if (owners.Length > 128) { Status = "Weapon hierarchy exceeds anchor bound"; return; }
                foreach (Component owner in owners)
                {
                    object? collection = _read.Get(owner, "ContainerCollectionView");
                    if (!(_read.Get(collection, "ContainerBones") is IDictionary map) || map.Count > 128) continue;
                    foreach (SlotViewModel slot in snapshot.Slots)
                    {
                        object? key = slot.Slot.NativeSlot;
                        if (key == null) continue;
                        // Match the exact native slot object, never a name/template lookalike.
                        foreach (DictionaryEntry entry in map)
                        {
                            if (!ReferenceEquals(entry.Key, key)) continue;
                            Transform? bone = _read.Get(entry.Value, "Bone") as Transform;
                            if (bone == null || !bone.IsChildOf(_prefab.transform)) continue;
                            if (_bones.TryGetValue(key, out Transform previous) && previous != bone) _ambiguous.Add(key);
                            else _bones[key] = bone;
                        }
                    }
                }
                foreach (object key in _ambiguous) _bones.Remove(key);
                BindPlane(snapshot);
                Status = _bones.Count + " native slot anchors; " + _ambiguous.Count + " ambiguous omitted";
            }
            catch (Exception e) { Clear(); Status = "Anchor binding unavailable: " + e.GetBaseException().GetType().Name; }
        }

        private void BindPlane(RaidSnapshot snapshot)
        {
            if (_frame == null) { _planeReason = "Animated weapon frame unavailable"; return; }
            Transform? nearMuzzle = null;
            float farDistance = 0, nearDistance = float.PositiveInfinity;
            foreach (SlotViewModel slot in snapshot.Slots)
            {
                if (slot.Slot.Group != AttachmentGroup.Muzzle || slot.Slot.NativeSlot == null ||
                    !_bones.TryGetValue(slot.Slot.NativeSlot, out Transform bone) || bone == null) continue;
                float distance = (bone.position - _frame.position).sqrMagnitude;
                if (distance > farDistance) { farDistance = distance; _muzzle = bone; }
                if (distance < nearDistance) { nearDistance = distance; nearMuzzle = bone; }
            }
            // Two native muzzle sockets give the barrel's line even when the weapon origin
            // is offset from it. A single socket can still establish receiver-to-muzzle direction.
            _boreStart = _muzzle != null && nearMuzzle != null && (_muzzle.position - nearMuzzle.position).sqrMagnitude > .0016f ? nearMuzzle : _frame;
            _topReference = _frame.Find("mod_magazine");
            _topIsBelow = _topReference != null;
            if (_topReference != null && !UsableTop(_topReference)) _topReference = null;
            if (_topReference == null)
            {
                _topIsBelow = false;
                foreach (SlotViewModel slot in snapshot.Slots)
                    if (slot.Slot.Group == AttachmentGroup.Optic && slot.Slot.NativeSlot != null &&
                        _bones.TryGetValue(slot.Slot.NativeSlot, out Transform bone) && bone != null && UsableTop(bone))
                    { _topReference = bone; break; }
            }
            _planeReason = _muzzle == null || _topReference == null ? "Muzzle/top reference unavailable" : "Bound native barrel/weapon-top references";
        }
        private bool UsableTop(Transform reference)
        {
            if (_muzzle == null || _boreStart == null || _frame == null) return false;
            Vector3 bore = (_muzzle.position - _boreStart.position).normalized;
            Vector3 top = reference.position - _frame.position;
            return (top - bore * Vector3.Dot(top, bore)).sqrMagnitude > .000004f;
        }

        // Called once per visible frame. Only cached camera/transform reads and identity checks.
        public bool IsCurrent()
        {
            return _prefab != null && _camera != null && _camera.isActiveAndEnabled && _prefab.gameObject.activeInHierarchy &&
                ReferenceEquals(_read.Get(_player, "_handsController"), _hands) &&
                ReferenceEquals(_read.Get(_hands, "_item"), _weapon) &&
                ReferenceEquals(_read.Get(_prefab, "_weaponData"), _weapon);
        }

        public bool Project(SlotObservation? slot, float width, float height, out Vector2 point)
        {
            point = default;
            if (_camera == null || slot?.NativeSlot == null || !_bones.TryGetValue(slot.NativeSlot, out Transform bone) ||
                bone == null || !bone.gameObject.activeInHierarchy) return false;
            Vector3 screen = _camera.WorldToScreenPoint(bone.position);
            if (!OverlayLayout.Project(screen.x, screen.y, screen.z, Screen.width, Screen.height, width, height, out float x, out float y)) return false;
            point = new Vector2(x, y); return true;
        }

        public bool ProjectCardPlane(float width, float height, float perspective, float clockwiseDegrees,
            out PanelProjection? panel, out Vector2 weaponPoint, bool faceCamera = false)
        {
            panel = null; weaponPoint = default;
            if (_camera == null || _frame == null || !_frame.gameObject.activeInHierarchy || _camera.orthographic) return false;
            bool muzzleAvailable = _muzzle != null && _muzzle.gameObject.activeInHierarchy;
            bool basisAvailable = muzzleAvailable && _boreStart != null && _topReference != null &&
                _boreStart.gameObject.activeInHierarchy && _topReference.gameObject.activeInHierarchy;
            if (!faceCamera && !basisAvailable) { _planeReason = "Muzzle/top reference unavailable"; return false; }
            Vector3 screen = _camera.WorldToScreenPoint(muzzleAvailable ? (_frame.position + _muzzle!.position) * .5f : _frame.position);
            if (!OverlayLayout.Project(screen.x, screen.y, screen.z, Screen.width, Screen.height, width, height, out float x, out float y)) return false;
            weaponPoint = _lastHomeAnchor = new Vector2(x, y);
            Vector3 bore = basisAvailable ? _camera.transform.InverseTransformDirection(_muzzle!.position - _boreStart!.position) : default;
            Vector3 top = basisAvailable ? _camera.transform.InverseTransformDirection((_topReference!.position - _frame.position) * (_topIsBelow ? -1 : 1)) : default;
            bool nativePlane = WeaponCardBasis.TryCreate(Numeric(bore), Numeric(top), out var localRight, out var localUp);
            if (!faceCamera && !nativePlane)
            { _planeReason = "Barrel/top plane end-on or degenerate"; return false; }
            _planeReason = faceCamera ? "Camera-facing plane at live weapon depth" + (muzzleAvailable ? "" : "; receiver anchor") : "Native barrel-aligned plane";
            _lastBore = bore; _lastRight = Unity(localRight); _lastUp = Unity(localUp);
            // Measure a full viewport height, then divide. Subtracting two world positions
            // only one pixel apart loses precision far from the world origin and makes cards breathe.
            float unit = (_camera.ViewportToWorldPoint(new Vector3(.5f, 1, screen.z)) -
                _camera.ViewportToWorldPoint(new Vector3(.5f, 0, screen.z))).magnitude / height;
            const float scale = .9f;
            // Project one surface on the camera's central ray at the weapon's observed depth.
            // Reusing it for all category homes avoids a different off-axis skew for each slot.
            Vector3 center = _camera.ViewportToWorldPoint(new Vector3(.5f, .5f, screen.z));
            if (unit <= 0) return false;
            if (!WeaponCardBasis.TryTune(localRight, localUp, perspective, clockwiseDegrees, out var cardRight, out var cardUp, faceCamera))
            { _planeReason = "Card tuning unavailable"; return false; }
            Vector3 right = _camera.transform.TransformDirection(Unity(cardRight));
            Vector3 down = -_camera.transform.TransformDirection(Unity(cardUp));
            _lastCardRight = Unity(cardRight); _lastCardUp = Unity(cardUp);
            Vector3 a = center - (right * OverlayLayout.PanelWidth + down * OverlayLayout.PanelHeight) * (.5f * scale * unit);
            return PanelProjection.TryCreate(Corner(a, width, height), Corner(a + right * (OverlayLayout.PanelWidth * scale * unit), width, height),
                Corner(a + down * (OverlayLayout.PanelHeight * scale * unit), width, height), out panel);
        }
        private PanelPoint Corner(Vector3 world, float width, float height)
        {
            Vector3 p = _camera!.WorldToScreenPoint(world);
            return new PanelPoint(p.x / Screen.width * width, (1 - p.y / Screen.height) * height, p.z);
        }
        public bool OwnsPanel(WeaponPanelAttachment panel) => _frame != null && ReferenceEquals(panel.Owner,_frame);
        public WeaponPanelAttachment? AttachPanel(PanelProjection plane, float width, float height)
        {
            if (!IsCurrent() || _frame == null || !_frame.gameObject.activeInHierarchy || width <= 0 || height <= 0) return null;
            float depth=_camera!.WorldToScreenPoint(_frame.position).z;
            float centerDepth=plane.Map(.5f,.5f).Depth;
            if (!PoseMarker.Finite(depth) || depth<=.02f || !PoseMarker.Finite(centerDepth) || centerDepth<=.02f) return null;
            // Preserve the exported perspective exactly at capture, at the real gun depth.
            return WeaponPanelAttachment.Capture(_frame,plane,p=>Numeric(_frame.InverseTransformPoint(
                _camera.ViewportToWorldPoint(new Vector3(p.X/width,1-p.Y/height,p.Depth*depth/centerDepth)))));
        }
        public bool ProjectPanel(WeaponPanelAttachment panel, float width, float height, out PanelProjection? plane)
        {
            plane=null;
            return IsCurrent() && _frame != null && _frame.gameObject.activeInHierarchy &&
                panel.Project(_frame,p=>Corner(_frame.TransformPoint(Unity(p)),width,height),out plane);
        }
        private static CardVector Numeric(Vector3 v) => new CardVector(v.x, v.y, v.z);
        private static Vector3 Unity(CardVector v) => new Vector3(v.X, v.Y, v.Z);

        public void Clear()
        {
            _player = _weapon = _hands = null; _prefab = null; _camera = null; _frame = _muzzle = _boreStart = _topReference = null;
            _planeReason = "Not bound"; _lastBore = _lastRight = _lastUp = _lastCardRight = _lastCardUp = default; _topIsBelow = false;
            _lastHomeAnchor = default;
            _bones.Clear(); _ambiguous.Clear(); Status = "No weapon bound";
        }
    }
}
