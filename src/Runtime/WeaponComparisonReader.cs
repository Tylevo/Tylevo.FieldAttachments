using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Tylevo.FieldAttachments.Core;

namespace Tylevo.FieldAttachments.Runtime
{
    // Observations only: never clones, reparents, removes, adds, or updates an item.
    public sealed class WeaponComparisonReader
    {
        private const int Limit=512;
        private readonly ReadAccess _read;
        private readonly Func<bool> _knownBuild;
        public WeaponComparisonReader(ReadAccess read) : this(read,()=>read.FindType("EFT.InventoryLogic.Weapon")?.Module.ModuleVersionId.ToString()==NativeInstallProbe.InspectedGameMvid) { }
        internal WeaponComparisonReader(ReadAccess read,Func<bool> knownBuild) { _read=read; _knownBuild=knownBuild; }

        public AttachmentComparison Read(RaidSnapshot snapshot,SlotObservation slot,ItemObservation? candidate)
        {
            var current=new WeaponComparisonValues();
            string label=candidate==null ? "Remove attachment" : candidate.Name;
            try
            {
                Require(_knownBuild(),"Stat calculation is unavailable for this game build.");
                object weapon=snapshot.Weapon ?? throw Invalid("Held weapon is unavailable.");
                object nativeSlot=slot.NativeSlot ?? throw Invalid("Mounting point is unavailable.");
                Require(ReadAccess.IsKind(weapon,"EFT.InventoryLogic.Weapon") && snapshot.WeaponId==Text(weapon,"Id") &&
                    ReferenceEquals(_read.Get(_read.Get(snapshot.Player,"HandsController"),"Item"),weapon),"Held weapon changed; refresh attachments.");
                Require(snapshot.Slots.Any(v=>ReferenceEquals(v.Slot,slot)),"Mounting point changed; refresh attachments.");
                var tree=Capture(weapon);
                var nativeSlots=Slots(weapon,tree);
                Require(nativeSlots.Any(s=>ReferenceEquals(s,nativeSlot)) && Text(nativeSlot,"ID")==slot.Id &&
                    slot.Path==snapshot.WeaponId+"/"+Text(Required(nativeSlot,"ParentItem"),"Id")+"/"+slot.Id,"Mounting point changed; refresh attachments.");
                object? outgoing=_read.Get(nativeSlot,"ContainedItem");
                Require(ReferenceEquals(outgoing,slot.Installed?.NativeItem),"Installed attachment changed; refresh attachments.");
                if(slot.Installed!=null) ValidateObservation(slot.Installed);
                bool folded=Flag(weapon,"Folded");
                object template=Required(weapon,"Template");
                float baseErgo=Number(template,"Ergonomics"), baseUp=Number(template,"RecoilForceUp"), baseBack=Number(template,"RecoilForceBack");
                var present=Modifiers(weapon,tree,folded);
                float nativeErgo=Number(weapon,"ErgonomicsTotal"), nativeDelta=Number(weapon,"RecoilDelta");
                Require(WeaponComparisonMath.Matches(nativeErgo,WeaponComparisonMath.Ergonomics(baseErgo,present.Ergo)) &&
                    WeaponComparisonMath.Matches(nativeDelta,present.Recoil/100) &&
                    WeaponComparisonMath.Matches(Number(weapon,"RecoilTotal"),WeaponComparisonMath.Recoil(baseUp+baseBack,present.Recoil)),
                    "Native weapon stats differ; comparison unavailable.");
                current=WeaponComparisonMath.Values(baseErgo,baseUp,baseBack,present.Ergo,present.Recoil,tree.Weight);

                var removed=outgoing==null ? new Totals() : Modifiers(outgoing,tree,folded);
                float oldWeight=outgoing==null ? 0 : tree.Nodes[outgoing].Weight;
                Totals added=new Totals(); float newWeight=0;
                Tree? incomingTree=null;
                if(outgoing!=null) Require(!HasFoldable(outgoing,tree),"This attachment changes folding; comparison unavailable.");
                if(candidate!=null)
                {
                    object incoming=candidate.NativeItem ?? throw Invalid("Candidate attachment is unavailable.");
                    Require(snapshot.Carried.Any(i=>ReferenceEquals(i,candidate)) && candidate.Examined==true && !tree.Nodes.ContainsKey(incoming),
                        "Candidate changed; refresh attachments.");
                    Require(candidate.SourceAddress!=null && candidate.SourceGrid!=null &&
                        ReferenceEquals(_read.Get(incoming,"Parent"),candidate.SourceAddress) &&
                        ReferenceEquals(_read.Get(candidate.SourceAddress,"Container"),candidate.SourceGrid),"Candidate moved; refresh attachments.");
                    Require(Sequence(Required(candidate.SourceGrid!,"Items")).Any(i=>ReferenceEquals(i,incoming)),"Candidate left carried storage; refresh attachments.");
                    Require(ReadAccess.IsKind(incoming,"EFT.InventoryLogic.Mod"),"Candidate stat type is unavailable.");
                    incomingTree=Capture(incoming);
                    ValidateObservation(candidate);
                    Require(!incomingTree.Ids.Overlaps(tree.Ids),"Duplicate item identities cannot be compared.");
                    Require(incomingTree.Nodes[incoming].Stack<=1,"Stacked candidate attachments cannot be compared.");
                    Require(!HasFoldable(incoming,incomingTree),"This attachment changes folding; comparison unavailable.");
                    added=Modifiers(incoming,incomingTree,folded); newWeight=incomingTree.Weight;
                }
                // A stack multiplier on the destination's ancestry would also multiply
                // the removed/added subtree weight. Guns and installed mods are singletons.
                object? ancestor=_read.Get(nativeSlot,"ParentItem"); int depth=0;
                while(ancestor!=null)
                {
                    Require(++depth<=16,"Weapon ancestry is unavailable.");
                    Require(tree.Nodes.TryGetValue(ancestor,out var node),"Weapon ancestry is unavailable.");
                    Require(node!.Stack<=1,"Stacked weapon attachments cannot be compared.");
                    if(ReferenceEquals(ancestor,weapon)) break;
                    ancestor=_read.Get(_read.Get(_read.Get(ancestor,"Parent"),"Container"),"ParentItem");
                }
                Require(ReferenceEquals(ancestor,weapon),"Weapon ancestry changed; refresh attachments.");
                var preview=WeaponComparisonMath.Values(baseErgo,baseUp,baseBack,present.Ergo-removed.Ergo+added.Ergo,
                    present.Recoil-removed.Recoil+added.Recoil,tree.Weight-oldWeight+newWeight);
                Require(preview.Ergonomics.HasValue && preview.Weight.HasValue,"Projected stats are unavailable.");
                ValidateStable(tree); if(incomingTree!=null) ValidateStable(incomingTree);
                Require(Flag(weapon,"Folded")==folded && ReferenceEquals(_read.Get(_read.Get(snapshot.Player,"HandsController"),"Item"),weapon) &&
                    WeaponComparisonMath.Matches(Number(weapon,"ErgonomicsTotal"),nativeErgo) &&
                    WeaponComparisonMath.Matches(Number(weapon,"RecoilDelta"),nativeDelta),"Weapon changed during comparison; refresh attachments.");
                return new AttachmentComparison(current,preview,label);
            }
            catch(Exception e)
            {
                string reason=e.GetBaseException().Message;
                _read.Note("Attachment stat comparison withheld: "+reason);
                return new AttachmentComparison(current,new WeaponComparisonValues(),label,reason);
            }
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance=new ReferenceComparer();
            public new bool Equals(object? a,object? b)=>ReferenceEquals(a,b);
            public int GetHashCode(object value)=>RuntimeHelpers.GetHashCode(value);
        }
        private sealed class Node
        {
            public object Item=null!,Template=null!; public object? Address;
            public string Id="",TemplateId=""; public int Stack; public float OwnWeight,Weight;
            public float? Ergo,Recoil;
            public bool IsContainer;
            public readonly List<object> Containers=new List<object>();
            public readonly List<Node> Children=new List<Node>();
        }
        private sealed class Tree
        {
            public readonly Dictionary<object,Node> Nodes=new Dictionary<object,Node>(ReferenceComparer.Instance);
            public readonly Dictionary<object,object[]> Containers=new Dictionary<object,object[]>(ReferenceComparer.Instance);
            public readonly HashSet<string> Ids=new HashSet<string>(StringComparer.Ordinal);
            public float Weight;
        }
        private readonly struct Totals
        {
            public readonly float Ergo,Recoil;
            public Totals(float ergonomics,float recoil) { Ergo=ergonomics; Recoil=recoil; }
        }
        private Tree Capture(object root)
        {
            var tree=new Tree(); tree.Weight=Walk(root,tree,0).Weight;
            Require(WeaponComparisonMath.Matches(Number(root,"TotalWeight"),tree.Weight),"Native item weight differs; comparison unavailable.");
            return tree;
        }
        private Node Walk(object item,Tree tree,int depth)
        {
            Require(depth<=16 && tree.Nodes.Count<Limit && !tree.Nodes.ContainsKey(item),"Item tree is incomplete or cyclic.");
            var node=new Node {Item=item,Template=Required(item,"Template"),Address=_read.Get(item,"Parent"),Id=Text(item,"Id"),TemplateId=Text(item,"TemplateId"),
                Stack=Integer(item,"StackObjectsCount"),OwnWeight=Number(item,"Weight")};
            Require(node.Id.Length>0 && node.TemplateId.Length>0 && tree.Ids.Add(node.Id) && node.OwnWeight>=0,"Item identity or weight is unavailable.");
            tree.Nodes.Add(item,node);
            float weight=node.OwnWeight;
            node.IsContainer=ReadAccess.IsKind(item,"EFT.InventoryLogic.ContainerCollection") || ReadAccess.IsKind(item,"EFT.InventoryLogic.CompoundItem");
            if(node.IsContainer)
            {
                foreach(object container in Sequence(Required(item,"Containers")))
                {
                    Require(tree.Containers.Count<Limit && !tree.Containers.ContainsKey(container) && ReferenceEquals(_read.Get(container,"ParentItem"),item),"Container ownership is unavailable.");
                    object[] children=Sequence(Required(container,"Items"));
                    tree.Containers.Add(container,children); node.Containers.Add(container);
                    foreach(object child in children)
                    {
                        Require(ReferenceEquals(_read.Get(_read.Get(child,"Parent"),"Container"),container),"Attachment address changed; refresh attachments.");
                        Node childNode=Walk(child,tree,depth+1); node.Children.Add(childNode); weight+=childNode.Weight;
                    }
                }
            }
            node.Weight=weight*Math.Max(1,node.Stack);
            Require(WeaponComparisonMath.Finite(node.Weight),"Item weight is unavailable.");
            return node;
        }
        private object[] Slots(object root,Tree tree)
        {
            object[] slots=Sequence(Required(root,"AllSlots"));
            var seen=new HashSet<object>(ReferenceComparer.Instance);
            foreach(object slot in slots)
            {
                Require(seen.Add(slot) && tree.Containers.ContainsKey(slot),"Attachment slot tree is unavailable.");
                object? child=_read.Get(slot,"ContainedItem"); object[] children=tree.Containers[slot];
                Require(child==null ? children.Length==0 : children.Length==1 && ReferenceEquals(children[0],child),"Attachment slot changed during comparison.");
            }
            return slots;
        }
        private Totals Modifiers(object root,Tree tree,bool folded)
        {
            var mods=new List<object>();
            if(ReadAccess.IsKind(root,"EFT.InventoryLogic.Mod")) mods.Add(root);
            foreach(object slot in Slots(root,tree))
            {
                object? child=_read.Get(slot,"ContainedItem");
                if(ReadAccess.IsKind(child,"EFT.InventoryLogic.Mod")) mods.Add(child!);
            }
            double ergo=0,recoil=0;
            foreach(object mod in mods)
            {
                Node node=tree.Nodes[mod];
                node.Ergo=Number(node.Template,"Ergonomics"); node.Recoil=Number(node.Template,"Recoil");
                ergo+=node.Ergo.Value;
                if(!folded || !ReadAccess.IsKind(mod,"EFT.InventoryLogic.Stock")) recoil+=node.Recoil.Value;
            }
            Require(WeaponComparisonMath.Finite((float)ergo) && WeaponComparisonMath.Finite((float)recoil),"Attachment stats are unavailable.");
            return new Totals((float)ergo,(float)recoil);
        }
        private bool HasFoldable(object root,Tree tree)
        {
            var pending=new Stack<Node>(); pending.Push(tree.Nodes[root]);
            while(pending.Count>0)
            {
                Node node=pending.Pop();
                foreach(object component in Sequence(Required(node.Item,"Components")))
                    if(ReadAccess.IsKind(component,"EFT.InventoryLogic.FoldableComponent")) return true;
                foreach(Node child in node.Children) pending.Push(child);
            }
            return false;
        }
        private void ValidateObservation(ItemObservation observation)
        {
            object item=observation.NativeItem ?? throw Invalid("Attachment is unavailable.");
            Require(Text(item,"Id")==observation.Id && Text(item,"TemplateId")==observation.TemplateId,"Attachment identity changed; refresh attachments.");
            bool children=Sequence(Required(item,"Slots")).Any(s=>_read.Get(s,"ContainedItem")!=null);
            Require(children==observation.HasChildren,"Attachment assembly changed; refresh attachments.");
            if(!observation.HasChildren) return;
            Require(observation.Assembly?.Complete==true,"Attachment assembly is incomplete.");
            object[] slots=Sequence(Required(item,"AllSlots"));
            Require(slots.Length==observation.Assembly!.Parts.Count && slots.All(s=>observation.Assembly.Parts.Any(p=>ReferenceEquals(p.Slot,s))),
                "Attachment assembly changed; refresh attachments.");
            foreach(var part in observation.Assembly!.Parts)
                Require(ReferenceEquals(_read.Get(part.Slot,"ParentItem"),part.Parent) && ReferenceEquals(_read.Get(part.Slot,"ContainedItem"),part.Item) &&
                    (part.Item==null || Text(part.Item,"Id")==part.Id && Text(part.Item,"TemplateId")==part.Template && ReferenceEquals(_read.Get(part.Item,"Parent"),part.Address)),
                    "Attachment assembly changed; refresh attachments.");
        }
        private void ValidateStable(Tree tree)
        {
            foreach(Node node in tree.Nodes.Values)
            {
                Require(Text(node.Item,"Id")==node.Id && Text(node.Item,"TemplateId")==node.TemplateId && ReferenceEquals(_read.Get(node.Item,"Template"),node.Template) &&
                    ReferenceEquals(_read.Get(node.Item,"Parent"),node.Address) && Integer(node.Item,"StackObjectsCount")==node.Stack &&
                    Number(node.Item,"Weight")==node.OwnWeight,"Item changed during comparison; refresh attachments.");
                if(node.Ergo.HasValue) Require(Number(node.Template,"Ergonomics")==node.Ergo.Value && Number(node.Template,"Recoil")==node.Recoil!.Value,"Attachment stats changed during comparison.");
                if(node.IsContainer) Require(Same(Sequence(Required(node.Item,"Containers")),node.Containers.ToArray()),"Containers changed during comparison.");
            }
            foreach(var pair in tree.Containers)
                Require(Same(Sequence(Required(pair.Key,"Items")),pair.Value),"Contents changed during comparison; refresh attachments.");
        }
        private static bool Same(object[] a,object[] b)=>a.Length==b.Length && a.Where((v,i)=>!ReferenceEquals(v,b[i])).Count()==0;
        private object Required(object target,string name)=>_read.Get(target,name) ?? throw Invalid("Stat observation unavailable: "+name+".");
        private string Text(object target,string name)=>ReadAccess.Text(Required(target,name));
        private float Number(object target,string name)
        {
            object value=Required(target,name);
            Require(value is float || value is double || value is int,"Stat value is unavailable: "+name+".");
            float number=Convert.ToSingle(value);
            Require(WeaponComparisonMath.Finite(number),"Stat value is unavailable: "+name+"."); return number;
        }
        private int Integer(object target,string name)=>Required(target,name) is int number ? number : throw Invalid("Item count is unavailable.");
        private bool Flag(object target,string name)=>Required(target,name) is bool flag ? flag : throw Invalid("Weapon state is unavailable.");
        private static object[] Sequence(object value)
        {
            Require(value is IEnumerable && !(value is string),"Item collection is unavailable.");
            var result=new List<object>();
            foreach(object? item in (IEnumerable)value)
            {
                Require(item!=null && result.Count<Limit,"Item collection is incomplete."); result.Add(item!);
            }
            return result.ToArray();
        }
        private static InvalidOperationException Invalid(string message)=>new InvalidOperationException(message);
        private static void Require(bool condition,string message) { if(!condition) throw Invalid(message); }
    }
}
