using System.Collections.Generic;
using UnityEngine;
namespace Kagemusha {
 public sealed partial class KagemushaStage {
  Material whiteInk,darkInk,rasenganCoreMat,flameMat,waterSkin,waterFoam;RasenganVisual handRasengan;DragonRig summonedDragon;
  Transform flamePivot;Mesh flameMesh;Vector3[] flameVertices;float flamePreviewUntil;
  readonly List<SmokeCloud> smokeClouds=new List<SmokeCloud>();
  class SmokeCloud {public Transform root;public Material mat;public float born;}
  Material ArtMat(Color c){var m=new Material(NinjaShader?NinjaShader:GlowShader);m.color=c;return m;}
  static Transform Shape(PrimitiveType type,string name,Transform parent,Material mat,Vector3 at,Vector3 scale){var v=SkeletonView.Primitive(type,name,parent,mat);v.localPosition=at;v.localScale=scale;return v;}
  static LineRenderer InkLine(string name,Transform parent,Material mat,float width,int count){var l=new GameObject(name,typeof(LineRenderer)).GetComponent<LineRenderer>();l.transform.SetParent(parent,false);l.sharedMaterial=mat;l.useWorldSpace=false;l.widthMultiplier=width;l.positionCount=count;l.numCapVertices=2;return l;}
  Transform BuildKatana(){
   var root=new GameObject("Katana").transform;root.SetParent(transform,false);
   var silver=ArtMat(new Color(1.15f,1.3f,1.4f));var ink=ArtMat(new Color(.035f,.055f,.07f));
   Shape(PrimitiveType.Cube,"Silver blade",root,silver,new Vector3(0,.43f,0),new Vector3(.047f,.86f,.016f));
   var edge=Shape(PrimitiveType.Cube,"Blade edge",root,goldMat,new Vector3(.023f,.43f,0),new Vector3(.006f,.86f,.018f));
   Shape(PrimitiveType.Cube,"Tsuba",root,goldMat,Vector3.zero,new Vector3(.16f,.024f,.09f));
   Shape(PrimitiveType.Cylinder,"Wrapped grip",root,ink,new Vector3(0,-.13f,0),new Vector3(.045f,.12f,.045f));
   for(int i=0;i<5;i++)Shape(PrimitiveType.Cube,"Grip wrapping",root,goldMat,new Vector3(0,-.04f-i*.043f,-.024f),new Vector3(.04f,.012f,.009f));
   return root;
  }
  void BuildJutsuArt(){
   whiteInk=ArtMat(new Color(1.6f,1.8f,2));darkInk=ArtMat(new Color(.015f,.035f,.06f));rasenganCoreMat=Mat(new Color(.12f,1.5f,2.6f));
   waterSkin=ArtMat(new Color(.06f,.65f,1.15f,.88f));waterFoam=ArtMat(new Color(.72f,1.25f,1.6f,.93f));
   handRasengan=new RasenganVisual(transform,rasenganCoreMat,whiteInk,waterMat);handRasengan.Root.gameObject.SetActive(false);
   flameMat=new Material(FlameShader?FlameShader:GlowShader);flameMat.color=new Color(1,.64f,.25f,.8f);flamePivot=new GameObject("Mouth fire breath").transform;flamePivot.SetParent(transform,false);
   flameMesh=new Mesh{name="Animated flame volume"};flameMesh.MarkDynamic();flameVertices=new Vector3[15*12];
   var triangles=new int[14*12*6];int n=0;for(int i=0;i<14;i++)for(int j=0;j<12;j++){int a=i*12+j,b=i*12+(j+1)%12,c=a+12,d=b+12;triangles[n++]=a;triangles[n++]=b;triangles[n++]=c;triangles[n++]=b;triangles[n++]=d;triangles[n++]=c;}
   var uv=new Vector2[15*12];for(int i=0;i<15;i++)for(int j=0;j<12;j++)uv[i*12+j]=new Vector2(j/12f,i/14f);
   flameMesh.vertices=flameVertices;flameMesh.triangles=triangles;flameMesh.uv=uv;
   var outer=new GameObject("Orange flame",typeof(MeshFilter),typeof(MeshRenderer));outer.transform.SetParent(flamePivot,false);outer.GetComponent<MeshFilter>().sharedMesh=flameMesh;outer.GetComponent<MeshRenderer>().sharedMaterial=flameMat;
   var inner=new GameObject("Yellow flame core",typeof(MeshFilter),typeof(MeshRenderer));inner.transform.SetParent(flamePivot,false);inner.transform.localScale=new Vector3(.48f,.48f,.86f);inner.GetComponent<MeshFilter>().sharedMesh=flameMesh;var innerMat=new Material(flameMat);innerMat.color=new Color(1,1.5f,2,.9f);inner.GetComponent<MeshRenderer>().sharedMaterial=innerMat;
   flamePivot.gameObject.SetActive(false);
  }
  void UpdateJutsuArt(float t,float dt,bool ready){
   bool forming=ready&&Gestures.RasenganStage>0;handRasengan.Root.gameObject.SetActive(forming);
   if(forming){handRasengan.Root.position=visible[10]+new Vector3(0,.08f,-.06f);handRasengan.Root.rotation=Quaternion.identity;handRasengan.Update(t,Gestures.Power);}
   bool flaming=(ready&&Gestures.FlameActive)||t<flamePreviewUntil;flamePivot.gameObject.SetActive(flaming);
   if(flaming){
    Vector3 direction=Gestures.FlameActive?Gestures.FlameDirection:new Vector3(.65f,.04f,1).normalized;
    flamePivot.position=visible[3]+direction*.12f;flamePivot.rotation=Quaternion.LookRotation(direction);
    for(int i=0;i<15;i++){float u=i/14f,r=(.035f+u*.67f)*(1+Mathf.Sin(t*22-u*27)*.14f);for(int j=0;j<12;j++){float a=j*Mathf.PI*2/12,tip=1+Mathf.Pow(u,4)*Mathf.Sin(a*5+t*16)*.13f,z=u*3.8f*tip,rr=r*(1+Mathf.Sin(a*3-u*20+t*18)*u*.17f);flameVertices[i*12+j]=new Vector3(Mathf.Cos(a)*rr+Mathf.Sin(t*13+u*16)*u*.09f,Mathf.Sin(a)*rr,z);}}
    flameMesh.vertices=flameVertices;flameMesh.RecalculateNormals();flameMesh.RecalculateBounds();
    for(int i=0;i<7;i++){var e=new ParticleSystem.EmitParams{position=flamePivot.position,velocity=(direction+UnityEngine.Random.insideUnitSphere*.14f)*UnityEngine.Random.Range(4f,7f),startColor=i%3==0?new Color(3,1.7f,.12f):new Color(3,.4f,.02f),startSize=UnityEngine.Random.Range(.09f,.20f),startLifetime=.55f};particles.Emit(e,1);}
    foreach(var enemy in enemies)if(enemy.hp>0){Vector3 d=enemy.root.position-flamePivot.position;float along=Vector3.Dot(d,direction);if(along>0&&along<4&&(d-direction*along).magnitude<.18f+along*.18f)Damage(enemy,1,t);}
   }
   for(int i=smokeClouds.Count-1;i>=0;i--){var c=smokeClouds[i];float age=(t-c.born)/1.1f;c.root.localScale=Vector3.one*(.5f+age*.8f);c.mat.color=new Color(.9f,.93f,.96f,Mathf.Clamp01((1-age)*1.4f));if(age>=1){Destroy(c.root.gameObject);Destroy(c.mat);smokeClouds.RemoveAt(i);}}
  }
  void Smoke(Vector3 at,float t){
   var root=new GameObject("Shadow clone smoke").transform;root.SetParent(transform,false);root.position=at;var m=ArtMat(new Color(.9f,.93f,.96f,.95f));
   for(int i=0;i<16;i++){Vector3 offset=UnityEngine.Random.insideUnitSphere;offset.y*=1.2f;Shape(PrimitiveType.Sphere,"White smoke puff",root,m,offset,Vector3.one*UnityEngine.Random.Range(.45f,.85f));}
   smokeClouds.Add(new SmokeCloud{root=root,mat=m,born=t});
  }
  void ClearJutsuArt(){foreach(var c in smokeClouds){Destroy(c.root.gameObject);Destroy(c.mat);}smokeClouds.Clear();if(flamePivot)flamePivot.gameObject.SetActive(false);if(handRasengan!=null)handRasengan.Root.gameObject.SetActive(false);}
  sealed class RasenganVisual {
   public readonly Transform Root;readonly Transform wings,core;readonly LineRenderer[] swirl=new LineRenderer[3];
   public RasenganVisual(Transform parent,Material blue,Material white,Material water){
    Root=new GameObject("Blue white rotating chakra").transform;Root.SetParent(parent,false);
    core=Shape(PrimitiveType.Sphere,"Chakra core",Root,blue,Vector3.zero,Vector3.one*.32f);
    wings=new GameObject("Formed wind blades").transform;wings.SetParent(Root,false);
    var mesh=new Mesh{name="Four curved wind blades"};var verts=new Vector3[16];var tris=new int[24];
    for(int i=0;i<4;i++){float a=i*Mathf.PI*.5f;Vector3[] points={new Vector3(.13f,-.06f,0),new Vector3(.85f,.02f,0),new Vector3(.52f,.18f,0),new Vector3(.16f,.10f,0)};for(int j=0;j<4;j++)verts[i*4+j]=Quaternion.AngleAxis(a*Mathf.Rad2Deg,Vector3.forward)*points[j];int o=i*6,v=i*4;tris[o]=v;tris[o+1]=v+1;tris[o+2]=v+2;tris[o+3]=v;tris[o+4]=v+2;tris[o+5]=v+3;}
    mesh.vertices=verts;mesh.triangles=tris;mesh.RecalculateNormals();var wing=new GameObject("White wind",typeof(MeshFilter),typeof(MeshRenderer));wing.transform.SetParent(wings,false);wing.GetComponent<MeshFilter>().sharedMesh=mesh;wing.GetComponent<MeshRenderer>().sharedMaterial=white;
    // Back face stays visible as the sphere is carried and thrown.
    var back=new GameObject("Back wind",typeof(MeshFilter),typeof(MeshRenderer));back.transform.SetParent(wings,false);back.transform.localRotation=Quaternion.Euler(180,0,0);back.GetComponent<MeshFilter>().sharedMesh=mesh;back.GetComponent<MeshRenderer>().sharedMaterial=white;
    for(int i=0;i<3;i++)swirl[i]=InkLine("Spiral chakra",Root,i==0?white:water,.018f,80);
   }
   public void Update(float t,float power){
    float r=Mathf.Lerp(.045f,.23f,Mathf.Clamp01(power));core.localScale=Vector3.one*r*2*(1+Mathf.Sin(t*23)*.04f);wings.gameObject.SetActive(power>.8f);wings.localScale=Vector3.one*Mathf.Clamp01((power-.8f)*5)*.75f;wings.localRotation=Quaternion.Euler(0,0,t*1250);
    for(int n=0;n<3;n++){for(int i=0;i<80;i++){float u=i/79f,a=u*Mathf.PI*8+t*(n==1?-14:18),y=(u-.5f)*r*1.8f,ring=Mathf.Sqrt(Mathf.Max(0,r*r-y*y));Vector3 p=new Vector3(Mathf.Cos(a)*ring,y,Mathf.Sin(a)*ring);swirl[n].SetPosition(i,Quaternion.Euler(n*60,n*55,n*40)*p);}swirl[n].widthMultiplier=.008f+power*.009f;}
   }
  }
  sealed class DragonRig {
   public readonly Transform Root;readonly Transform head;readonly Transform[] feet=new Transform[4],scales=new Transform[18];readonly Mesh skin;readonly Vector3[] vertices=new Vector3[48*10],path=new Vector3[48];readonly LineRenderer[] whiskers=new LineRenderer[2];
   public DragonRig(string name,Transform parent,Material body,Material belly,Material horn,Material eye,Material ink){
    Root=new GameObject(name).transform;Root.SetParent(parent,false);skin=new Mesh{name=name+" continuous body"};skin.MarkDynamic();int[] tris=new int[47*10*6];int k=0;
    for(int i=0;i<47;i++)for(int j=0;j<10;j++){int a=i*10+j,b=i*10+(j+1)%10;tris[k++]=a;tris[k++]=b;tris[k++]=a+10;tris[k++]=b;tris[k++]=b+10;tris[k++]=a+10;}
    skin.vertices=vertices;skin.triangles=tris;var tube=new GameObject("Serpentine dragon skin",typeof(MeshFilter),typeof(MeshRenderer));tube.transform.SetParent(Root,false);tube.GetComponent<MeshFilter>().sharedMesh=skin;tube.GetComponent<MeshRenderer>().sharedMaterial=body;
    head=new GameObject("Horned dragon head").transform;head.SetParent(Root,false);
    Shape(PrimitiveType.Sphere,"Skull",head,body,new Vector3(0,.04f,0),new Vector3(.55f,.38f,.52f));
    Shape(PrimitiveType.Sphere,"Long snout",head,body,new Vector3(0,-.015f,.30f),new Vector3(.38f,.22f,.48f));
    Shape(PrimitiveType.Sphere,"Open mouth",head,ink,new Vector3(0,-.11f,.36f),new Vector3(.33f,.13f,.42f));
    Shape(PrimitiveType.Sphere,"Lower jaw",head,belly,new Vector3(0,-.18f,.32f),new Vector3(.37f,.10f,.44f));
    for(int side=-1;side<=1;side+=2){
     Shape(PrimitiveType.Sphere,"Eye",head,eye,new Vector3(side*.23f,.11f,.17f),new Vector3(.13f,.10f,.13f));
     Shape(PrimitiveType.Cube,"Pupil",head,ink,new Vector3(side*.285f,.11f,.185f),new Vector3(.015f,.065f,.025f));
     var h=Shape(PrimitiveType.Capsule,"Backward horn",head,horn,new Vector3(side*.23f,.32f,-.17f),new Vector3(.07f,.27f,.07f));h.localRotation=Quaternion.Euler(-35,0,side*-25);
     Shape(PrimitiveType.Sphere,"Nostril",head,ink,new Vector3(side*.09f,.05f,.51f),Vector3.one*.055f);
     for(int t=0;t<4;t++)Shape(PrimitiveType.Capsule,"Fang",head,belly,new Vector3(side*.13f,-.09f,.23f+t*.075f),new Vector3(.025f,.045f,.025f));
     int n=side<0?0:1;whiskers[n]=InkLine("Flowing dragon whisker",head,belly,.018f,16);
    }
    for(int i=0;i<4;i++){feet[i]=new GameObject("Clawed leg").transform;feet[i].SetParent(Root,false);var arm=Shape(PrimitiveType.Capsule,"Foreleg",feet[i],body,new Vector3(0,0,0),new Vector3(.10f,.23f,.10f));arm.localRotation=Quaternion.Euler(0,0,55);for(int j=0;j<3;j++){var claw=Shape(PrimitiveType.Capsule,"Claw",feet[i],horn,new Vector3(.13f+j*.07f,-.21f,.06f),new Vector3(.035f,.13f,.035f));claw.localRotation=Quaternion.Euler(65,0,-20);}}
    for(int i=0;i<scales.Length;i++){scales[i]=Shape(PrimitiveType.Cube,"Dorsal scale",Root,horn,Vector3.zero,new Vector3(.10f,.16f,.12f));scales[i].localRotation=Quaternion.Euler(0,0,45);}
   }
   public void Water(float t){Root.localScale=Vector3.one*.78f;for(int i=0;i<48;i++){float u=i/47f;path[i]=new Vector3(Mathf.Sin(t*7-u*9)*u*.24f,Mathf.Cos(t*6-u*8)*u*.18f,-u*2.8f);}Draw(t,.19f);}
   public void Summon(float t,float growth,float fade){Root.localScale=Vector3.one*fade;for(int i=0;i<48;i++){float u=i/47f,a=(1-u)*Mathf.PI*3+t*.5f;path[i]=new Vector3(Mathf.Sin(a)*(1.05f+u*.25f),.24f+(1-u)*4.2f*growth,1.4f+Mathf.Cos(a)*.8f);}Draw(t,.25f);}
   void Draw(float t,float radius){
    for(int i=0;i<48;i++){Vector3 tangent=(path[Mathf.Min(47,i+1)]-path[Mathf.Max(0,i-1)]).normalized,normal=Vector3.Cross(tangent,Vector3.right);if(normal.sqrMagnitude<.01f)normal=Vector3.Cross(tangent,Vector3.up);normal.Normalize();Vector3 binormal=Vector3.Cross(tangent,normal).normalized;float r=radius*Mathf.Lerp(1,.08f,i/47f);for(int j=0;j<10;j++){float a=j*Mathf.PI*2/10;vertices[i*10+j]=path[i]+(normal*Mathf.Cos(a)+binormal*Mathf.Sin(a))*r;}}
    skin.vertices=vertices;skin.RecalculateNormals();skin.RecalculateBounds();head.localPosition=path[0];Vector3 forward=(path[0]-path[1]).normalized;head.localRotation=Quaternion.LookRotation(forward);head.localScale=Vector3.one*(radius/.19f);
    for(int i=0;i<4;i++){int idx=i<2?11:24;float side=i%2==0?-1:1;feet[i].localPosition=path[idx]+new Vector3(side*radius,-radius*.3f,0);feet[i].localScale=new Vector3(side,1,1);}
    for(int i=0;i<scales.Length;i++)scales[i].localPosition=path[2+i*2]+Vector3.up*radius*.9f;
    for(int n=0;n<2;n++)for(int j=0;j<16;j++){float u=j/15f,side=n==0?-1:1;whiskers[n].SetPosition(j,new Vector3(side*(.13f+u*.70f),-.03f+Mathf.Sin(u*4+t*3)*u*.09f,.43f-u*.35f));}
   }
  }
 }
}
