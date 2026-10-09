using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Tylevo.FieldAttachments.Core;

namespace Tylevo.FieldAttachments.Runtime
{
    public sealed class AttachmentAssemblyReader
    {
        private readonly ReadAccess _read;
        public AttachmentAssemblyReader(ReadAccess read) { _read=read; }
        public AttachmentAssembly Capture(object root, object? controller)
        {
            var parts=new List<AssemblyPart>(); var seen=new HashSet<object>(); var ids=new HashSet<string>();
            try { Walk(root,controller,"",0,parts,seen,ids); return new AttachmentAssembly(true,parts); }
            catch(Exception e) { _read.Note("Optic assembly withheld: "+e.GetBaseException().Message); return new AttachmentAssembly(false,parts); }
        }
        private void Walk(object parent,object? controller,string path,int depth,List<AssemblyPart> parts,HashSet<object> seen,HashSet<string> ids)
        {
            if(depth>8 || !seen.Add(parent)) throw new InvalidOperationException("child cycle/depth");
            if(!(Read(parent,"Slots") is IEnumerable slots)) throw new InvalidOperationException("child slots unavailable");
            var local=new HashSet<string>();
            foreach(object slot in slots)
            {
                if(parts.Count>=64 || slot==null) throw new InvalidOperationException("child slot cap/null");
                string slotId=ReadAccess.Text(Read(slot,"ID"));
                if(slotId.Length==0 || !local.Add(slotId) || !ReferenceEquals(Read(slot,"ParentItem"),parent)) throw new InvalidOperationException("child slot identity");
                object? child=Read(slot,"ContainedItem"), address=child==null ? null : Read(child,"Parent");
                string id=child==null ? "" : ReadAccess.Text(Read(child,"Id"));
                string template=child==null ? "" : ReadAccess.Text(Read(child,"TemplateId"));
                if(child!=null && (id.Length==0 || template.Length==0 || !ids.Add(id) || address==null ||
                    !ReferenceEquals(Read(address,"Container"),slot))) throw new InvalidOperationException("child item/address identity");
                bool examined=child==null || Examined(controller,child);
                int pin=child==null ? 0 : Convert.ToInt32(Read(child,"PinLockState") ?? throw new InvalidOperationException("child pin unknown"));
                string route=path+"/"+slotId;
                parts.Add(new AssemblyPart(slot,parent,child,address,route,id,template,child?.GetType().FullName ?? "",
                    Flag(slot,"Required"),Flag(slot,"Locked"),Flag(slot,"Deleted"),examined,pin));
                if(child!=null) Walk(child,controller,route,depth+1,parts,seen,ids);
            }
        }
        private static bool Examined(object? controller,object item)
        {
            if(controller==null) return false;
            var methods=ReadAccess.Methods(controller.GetType(),"Examined",false).Where(m=>!m.ContainsGenericParameters &&
                m.ReturnType==typeof(bool) && m.GetParameters().Length==1 && m.GetParameters()[0].ParameterType.IsInstanceOfType(item)).ToArray();
            return methods.Length==1 && Equals(methods[0].Invoke(controller,new[]{item}),true);
        }
        private static bool Flag(object obj,string name) => Read(obj,name) is bool b ? b : throw new InvalidOperationException(name+" unknown");
        private static object? Read(object obj,string name)
        {
            for(Type? t=obj.GetType();t!=null;t=t.BaseType)
            {
                const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly;
                var p=t.GetProperty(name,flags); if(p!=null && p.GetIndexParameters().Length==0) return p.GetValue(obj,null);
                var f=t.GetField(name,flags); if(f!=null) return f.GetValue(obj);
            }
            throw new InvalidOperationException(name+" unavailable");
        }
    }
}
