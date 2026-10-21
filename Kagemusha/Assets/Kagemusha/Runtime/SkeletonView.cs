using UnityEngine;
namespace Kagemusha {
 public sealed class SkeletonView {
  public readonly Transform Root;
  readonly Transform[] joints=new Transform[17],bones=new Transform[16];
  readonly Material material;
  public SkeletonView(string name,Transform parent,Material mat,float radius=.035f){
   Root=new GameObject(name).transform;Root.SetParent(parent,false);material=mat;
   for(int i=0;i<17;i++){joints[i]=Primitive(PrimitiveType.Sphere,"Joint "+i,Root,mat);joints[i].localScale=Vector3.one*(i==3?.19f:radius*2.3f);}
   for(int i=1;i<17;i++)bones[i-1]=Primitive(PrimitiveType.Cylinder,"Bone "+PoseSource.Parents[i]+" → "+i,Root,mat);
  }
  public static Transform Primitive(PrimitiveType type,string name,Transform parent,Material mat){var o=GameObject.CreatePrimitive(type);o.name=name;o.transform.SetParent(parent,false);Object.Destroy(o.GetComponent<Collider>());o.GetComponent<Renderer>().sharedMaterial=mat;return o.transform;}
  public void Update(Vector3[] pose,Vector3 offset,float width=.035f){
   for(int i=0;i<17;i++)joints[i].localPosition=pose[i]+offset;
   for(int i=1;i<17;i++)Segment(bones[i-1],pose[PoseSource.Parents[i]]+offset,pose[i]+offset,width);
  }
  public static void Segment(Transform t,Vector3 a,Vector3 b,float width){Vector3 d=b-a;t.localPosition=(a+b)*.5f;t.localRotation=d.sqrMagnitude>.00001f?Quaternion.FromToRotation(Vector3.up,d.normalized):Quaternion.identity;t.localScale=new Vector3(width*2,d.magnitude*.5f,width*2);}
  public void SetQuality(float[] quality,Material missing){
   for(int i=0;i<17;i++)joints[i].GetComponent<Renderer>().sharedMaterial=quality[i]>=PoseSource.JointThreshold?material:missing;
   for(int i=1;i<17;i++)bones[i-1].GetComponent<Renderer>().sharedMaterial=quality[i]>=PoseSource.JointThreshold&&quality[PoseSource.Parents[i]]>=PoseSource.JointThreshold?material:missing;
  }
  public void SetVisible(bool value){Root.gameObject.SetActive(value);}
 }
}
