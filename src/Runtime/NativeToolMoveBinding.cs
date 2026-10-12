using System;
using System.Threading;

namespace Tylevo.FieldAttachments.Runtime
{
    // Operation identity and lifetime only. This object never writes inventory data.
    internal sealed class NativeToolMoveBinding
    {
        internal readonly object Item, Controller, Slot, From, To;
        internal readonly bool RequiredReplacement;
        private Func<string>? _validate;
        private readonly Func<DateTime> _clock;
        private readonly DateTime _expires;
        private readonly int _thread;
        private bool _executed;
        internal bool Revoked { get; private set; }
        internal string Denial { get; private set; } = "";

        internal NativeToolMoveBinding(object item, object controller, object slot, object from, object to,
            Func<string> validate, Func<DateTime>? clock = null, bool requiredReplacement = false)
        {
            Item=item; Controller=controller; Slot=slot; From=from; To=to;
            RequiredReplacement=requiredReplacement;
            _validate=validate; _clock=clock ?? (()=>DateTime.UtcNow);
            _expires=_clock().AddSeconds(20); _thread=Thread.CurrentThread.ManagedThreadId;
        }

        internal bool Matches(object item, object controller, object from, object to) =>
            ReferenceEquals(Item,item) && ReferenceEquals(Controller,controller) &&
            ReferenceEquals(From,from) && ReferenceEquals(To,to);

        internal bool Validate(bool execution)
        {
            if(Revoked || _executed) return false;
            if(Thread.CurrentThread.ManagedThreadId!=_thread) return Reject("Native request changed execution thread.");
            if(_clock()>=_expires) return Reject("Multitool request expired.");
            try
            {
                string reason=_validate?.Invoke() ?? "Multitool request was revoked.";
                if(reason.Length!=0) return Reject(reason);
                if(execution) _executed=true;
                return true;
            }
            catch(Exception e) { return Reject("Multitool revalidation failed: "+e.GetBaseException().Message); }
        }

        private bool Reject(string reason) { Denial=reason; Revoke(); return false; }
        internal void Revoke() { Revoked=true; _validate=null; }
    }
}
