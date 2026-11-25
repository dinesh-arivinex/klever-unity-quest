using UnityEngine;
namespace Klever.VR
{
    // Bounds-based display placement shared with the DPVR lesson behaviour.
    // Quest retains its authored Oculus grab/hand interactions.
    public static class LessonModelPlacement
    {
        private static bool BoundsOf(GameObject root,out Bounds bounds)
        {
            bounds=default; bool found=false;
            foreach(var r in root.GetComponentsInChildren<Renderer>(true)) {
                if(!(r is MeshRenderer || r is SkinnedMeshRenderer) || r.CompareTag("notAChild") || r.name=="Quad" || r.GetComponent<TMPro.TMP_Text>()!=null || r.GetComponentInParent<Canvas>()!=null)continue;
                if(!found){bounds=r.bounds;found=true;}else bounds.Encapsulate(r.bounds);
            }
            return found;
        }
        public static void PlaceOnTable(GameObject root,Vector3 anchor)
        {
            if(!BoundsOf(root,out var bounds))return;
            float largest=Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));
            if(largest<=0.00001f)return;
            root.transform.localScale*=0.65f/largest;
            BoundsOf(root,out bounds);
            root.transform.position+=new Vector3(anchor.x-bounds.center.x,anchor.y+0.02f-bounds.min.y,anchor.z-bounds.center.z);
            var box=root.GetComponent<BoxCollider>();
            if(box==null)return;
            Bounds local=default; bool found=false;
            foreach(var r in root.GetComponentsInChildren<Renderer>(true)) {
                if(!(r is MeshRenderer || r is SkinnedMeshRenderer) || r.CompareTag("notAChild") || r.name=="Quad" || r.GetComponent<TMPro.TMP_Text>()!=null || r.GetComponentInParent<Canvas>()!=null)continue;
                var b=r.bounds;
                for(int i=0;i<8;i++){
                    var p=root.transform.InverseTransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                    if(!found){local=new Bounds(p,Vector3.zero);found=true;}else local.Encapsulate(p);
                }
            }
            if(found){box.center=local.center;box.size=Vector3.Max(local.size,Vector3.one*0.01f);}
        }
    }
}