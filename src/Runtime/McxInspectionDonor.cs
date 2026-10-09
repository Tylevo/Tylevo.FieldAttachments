using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Tylevo.FieldAttachments.Core;
using UnityEngine;

namespace Tylevo.FieldAttachments.Runtime
{
    // One private clip copy, never a donor controller or prefab. No game files/asset events edited.
    internal sealed class McxInspectionDonor
    {
        private const string Bundle="assets/content/weapons/mcx/client_assets.bundle";
        private const string Asset="Assets/Content/Weapons/mcx/weapon_sig_mcx_gen1_762x35_animations.fbx";
        private const string Sha256="48A462F8D3C0FEB2E37FFD77A98975C2FD26FC20CBAA21A528617D8F2B341D20";
        private AnimationClip? _clip;
        private bool _attempted;
        public string Status { get; private set; }="not loaded";
        public AnimationClip? Get()
        {
            if(_clip!=null) return _clip;
            if(_attempted) return null;
            _attempted=true;
            AssetBundle? owned=null;
            try
            {
                string path=Path.Combine(Application.streamingAssetsPath,"Windows",Bundle);
                using(var stream=File.OpenRead(path)) using(var hash=SHA256.Create())
                    if(BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","")!=Sha256)
                        throw new InvalidOperationException("installed MCX asset differs from the inspected 4.1.5 bundle");
                var loaded=AssetBundle.GetAllLoadedAssetBundles().Where(b=>b!=null && b.name==Bundle).ToArray();
                if(loaded.Length>1) throw new InvalidOperationException("MCX bundle ownership ambiguous");
                AssetBundle bundle=loaded.Length==1 ? loaded[0] : owned=AssetBundle.LoadFromFile(path);
                if(bundle==null) throw new InvalidOperationException("MCX asset bundle unavailable");
                var clips=bundle.LoadAssetWithSubAssets<AnimationClip>(Asset).Where(c=>c!=null && c.name=="mcx_look").ToArray();
                if(clips.Length!=1 || clips[0].legacy || clips[0].events.Length!=0 || Math.Abs(clips[0].length-3.300001144f)>.01f)
                    throw new InvalidOperationException("MCX inspection clip metadata changed/ambiguous");
                _clip=UnityEngine.Object.Instantiate(clips[0]);
                _clip.name=GlobalMcxInspection.DonorName;
                _clip.hideFlags=HideFlags.HideAndDontSave; // One cached clip for this plugin session.
                Status="private MCX clip from verified local bundle; no carried donor required";
                return _clip;
            }
            catch(Exception e) { Status="MCX donor unavailable: "+e.GetBaseException().Message; return null; }
            finally { if(owned!=null) owned.Unload(true); } // Only our bundle; never unload a game-owned bundle.
        }
    }
}
