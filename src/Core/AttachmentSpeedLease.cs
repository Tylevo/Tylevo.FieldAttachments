using System;

namespace Tylevo.FieldAttachments.Core
{
    public sealed class AttachmentSpeedLease
    {
        public const float DefaultMultiplier=1.12f;
        private Func<float>? _read;
        private Action<float>? _write;
        private float _previous, _applied;
        public bool Active => _read!=null;
        public bool Acquire(Func<float> read,Action<float> write,float multiplier)
        {
            if(Active || !PoseMarker.Finite(multiplier) || multiplier<=1 || multiplier>1.15f) return false;
            float previous=read();
            if(!PoseMarker.Finite(previous) || previous<=0 || previous>4) return false;
            _previous=previous; _applied=previous*multiplier; _read=read; _write=write;
            // Retain restoration ownership if the setter throws after writing.
            write(_applied); return true;
        }
        public bool Release()
        {
            if(!Active) return true;
            try
            {
                // A different speed belongs to another system. Do not overwrite it.
                if(_read!()==_applied) _write!(_previous);
                Forget(); return true;
            }
            catch { return false; }
        }
        public void Forget() { _read=null; _write=null; }
    }
}
