using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Tylevo.FieldAttachments.Core;
using UnityEngine;

namespace Tylevo.FieldAttachments.Runtime
{
    public sealed class CustomStmClip
    {
        public const string BundleHash = "20D91BAE317056DE90EAB57E0A38326B8C6145C92D2F79665EABAEB31CBFE28D";
        public const string M4BundleHash = "DF15462805D726A4EBDF89F25125936E45CF2FDBD2899FF53B260A9325611D9B";
        public const string McxBundleHash = "8D6B90A453AF9405E9E3A934C8E30D2166717D252C63C222A41DFF67C0F8462D";
        public const string M700BundleHash = "9ACC7FCA62B718F4AA090A3DF431C246BD6917DA025FE87F6CB91DF9226E0A8A";
        public const string M870BundleHash = "23510E7D5E031B3DB2701860E5F74AB5FA260D7CEFF7078D1836E67096AE043E";
        public const string Glock17BundleHash = "A6D5339F9F2B6A018FD723EDB98C15B2832BBB9CDF80C06BC4F1BA23CC8F77E9";
        private readonly Dictionary<string, AnimationClip?> _clips = new Dictionary<string, AnimationClip?>();
        private readonly Dictionary<string, string> _statuses = new Dictionary<string, string>();
        public string Status { get; private set; } = "not loaded";
        public AnimationClip? Get(string directory, string template)
        {
            template = InstalledWeaponAliases.Canonical(template);
            string key = CustomStmInspection.BundleFor(template);
            if (key.Length == 0) { Status = "no authored animation for recipient"; return null; }
            if (_clips.TryGetValue(key, out var cached)) { Status = _statuses[key]; return cached; }
            AnimationClip? clip = null;
            AssetBundle? bundle = null;
            try
            {
                string expectedHash = AuditedWeaponPresentations.For(template)?.Sha256 ?? GeneratedPresentationFamilies.For(template)?.Sha256 ?? (template == CustomStmInspection.M4Template ? M4BundleHash :
                    template == McxInspectionSegment.Template ? McxBundleHash : template == CustomStmInspection.M700Template ? M700BundleHash :
                    template == CustomStmInspection.M870Template ? M870BundleHash : template == CustomStmInspection.Glock17Template ? Glock17BundleHash : BundleHash);
                string path = Path.Combine(directory, "Animation", key + ".bundle");
                using (var stream = File.OpenRead(path))
                using (var sha = SHA256.Create())
                    if (BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "") != expectedHash)
                        throw new InvalidOperationException("custom STM bundle hash differs from tested export");
                bundle = AssetBundle.LoadFromFile(path);
                if (bundle == null) throw new InvalidOperationException("custom STM bundle could not be loaded");
                var clips = bundle.LoadAllAssets<AnimationClip>();
                if (clips.Length != 1 || !CustomStmInspection.ValidClip(template, clips[0].name, clips[0].length, clips[0].legacy, clips[0].humanMotion, clips[0].events.Length))
                    throw new InvalidOperationException("custom STM clip metadata changed");
                clip = clips.Single(); // Our own clip-only asset; retain its compiled binding data.
                clip.hideFlags = HideFlags.DontUnloadUnusedAsset;
                Status = "verified " + clip.name;
            }
            catch (Exception e) { Status = e.GetBaseException().Message; }
            finally { if (bundle != null) bundle.Unload(false); }
            // Remember failures per asset too; never substitute another grip or silently retry.
            _clips[key] = clip; _statuses[key] = Status;
            return clip;
        }
    }
}
