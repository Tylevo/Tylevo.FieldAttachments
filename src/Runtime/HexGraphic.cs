using UnityEngine;
using UnityEngine.UI;

namespace Tylevo.FieldAttachments.Runtime
{
    // Procedural UI geometry; no textures, materials or native game object writes.
    public sealed class HexGraphic : MaskableGraphic
    {
        private Color _edge=Color.white;
        private bool _slash;
        public void Style(Color fill,Color edge,bool slash=false)
        { color=fill; if (_edge==edge && _slash==slash) return; _edge=edge; _slash=slash; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); Rect r=rectTransform.rect; Vector2 center=r.center;
            for (int i=0;i<6;i++)
            {
                float a=i*Mathf.PI/3,b=(i+1)*Mathf.PI/3;
                Vector2 p=center+new Vector2(Mathf.Cos(a)*r.width/2,Mathf.Sin(a)*r.height/1.7320508f);
                Vector2 q=center+new Vector2(Mathf.Cos(b)*r.width/2,Mathf.Sin(b)*r.height/1.7320508f);
                int start=mesh.currentVertCount;
                mesh.AddVert(center,color,Vector2.zero); mesh.AddVert(p,color,Vector2.zero); mesh.AddVert(q,color,Vector2.zero);
                mesh.AddTriangle(start,start+1,start+2);
                Stroke(mesh,p,q,1.8f,_edge);
            }
            if (_slash)
            {
                const int segments=24; float radius=r.height*.23f;
                for(int i=0;i<segments;i++)
                {
                    float a=i*Mathf.PI*2/segments,b=(i+1)*Mathf.PI*2/segments;
                    Stroke(mesh,center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,
                        center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,2,_edge);
                }
                Stroke(mesh,center+new Vector2(-.7f,-.7f)*radius,center+new Vector2(.7f,.7f)*radius,2.4f,_edge);
            }
        }
        private static void Stroke(VertexHelper mesh,Vector2 a,Vector2 b,float width,Color tint)
        {
            Vector2 d=b-a,n=new Vector2(-d.y,d.x).normalized*width/2;
            int i=mesh.currentVertCount;
            mesh.AddVert(a-n,tint,Vector2.zero); mesh.AddVert(a+n,tint,Vector2.zero);
            mesh.AddVert(b+n,tint,Vector2.zero); mesh.AddVert(b-n,tint,Vector2.zero);
            mesh.AddTriangle(i,i+1,i+2); mesh.AddTriangle(i,i+2,i+3);
        }
    }
}
