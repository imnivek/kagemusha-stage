using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Kagemusha {
 public sealed class PoseSource : MonoBehaviour {
  public const float JointThreshold=.25f;
  public static readonly int[] Parents={-1,0,1,2,3,2,5,6,2,8,9,0,11,12,0,14,15};
  public readonly Vector3[] Joints=new Vector3[17];
  public readonly Vector3[] GestureJoints=new Vector3[17];
  public readonly float[] Confidence=new float[17];
  public readonly Vector2[] ImagePoints=new Vector2[17];
  public bool Demo=true,Calibrated,Tracking,Starting,Mirror=true;
  public bool FullBodyVisible {get;private set;}
  public bool HeadAimVisible {get;private set;}
  public Vector3 HeadDirection {get;private set;}=Vector3.forward;
  public bool HandsVisible=>Confidence[7]>=JointThreshold&&Confidence[10]>=JointThreshold;
  public bool CanCast=>Demo||(Tracking&&Time.realtimeSinceStartup-lastResult<.25f);
  public string Framing=>FullBodyVisible?"全身追蹤":"上半身追蹤";
  public string Message="自動演示 · 合成動作，非真人追蹤";
  public float InferenceMs,DemoClock,DemoOffset,RelativeSize=1;
  public int People,Frames,CameraIndex;
  public bool SubjectSelected {get;private set;}
  public Texture Preview=>preview?preview:webcam;
  public string CameraName=>webcam?webcam.deviceName:"未啟動";
  public WebCamDevice[] Devices=>WebCamTexture.devices;
  readonly float[] buffer=new float[128];
  readonly float[] lengths={0,.22f,.33f,.18f,.14f,.22f,.30f,.28f,.22f,.30f,.28f,.14f,.46f,.46f,.14f,.46f,.46f};
  readonly Vector3[] raw=new Vector3[17],target=new Vector3[17],directions=new Vector3[17];
  Vector2 referenceCenter,lastCenter;float referenceSpan,referenceFootY,referenceFullSpan,lastResult=-100,nextSubmit,captureStarted=-100;
  ulong sequence;bool native,anchored,fullAnchored,havePose;WebCamTexture webcam;Texture2D preview;Color32[] pixels;byte[] rgba;
  Coroutine opening;
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
  [DllImport("KagemushaPose")]static extern int KP_Submit(byte[] rgba,int width,int height);
  [DllImport("KagemushaPose")]static extern ulong KP_Copy([Out]float[] dest,int capacity);
  [DllImport("KagemushaPose")]static extern void KP_Reset();
  [DllImport("KagemushaPose")]static extern void KP_ResetFocus();
#endif
  void Awake(){
   var neutral=new Vector3[17];DemoPose(0,neutral);Array.Copy(neutral,Joints,17);Array.Copy(neutral,GestureJoints,17);
   for(int i=1;i<17;i++)directions[i]=(neutral[i]-neutral[Parents[i]]).normalized;
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
   try{KP_Reset();native=true;}catch(Exception e){Debug.LogWarning("Capture unavailable: "+e.Message);}
#endif
  }
  void Start(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"--demo")>=0)UseDemo();else StartCamera();}
  public void UseDemo(){StopCamera();Demo=true;Tracking=true;Message="自動演示 · 合成動作，非真人追蹤";}
  public void StartCamera(){if(Starting)return;StopCamera();opening=StartCoroutine(Open());}
  IEnumerator Open(){
   Demo=false;Starting=true;
   if(!native){Starting=false;Message="此鏡頭版本需要 macOS 14 以上";yield break;}
   if(!Application.HasUserAuthorization(UserAuthorization.WebCam)){
    Message="正在請求鏡頭權限…";var request=Application.RequestUserAuthorization(UserAuthorization.WebCam);
    float permissionEnd=Time.realtimeSinceStartup+20;while(!request.isDone&&Time.realtimeSinceStartup<permissionEnd)yield return null;
    if(!Application.HasUserAuthorization(UserAuthorization.WebCam)){Starting=false;opening=null;Message="鏡頭尚未授權 · 請在系統設定允許影武者相機後重試";yield break;}
   }
   var devices=Devices;if(devices.Length==0){Starting=false;opening=null;Message="找不到鏡頭，請確認裝置已連接";yield break;}
   CameraIndex=Mathf.Clamp(CameraIndex,0,devices.Length-1);webcam=new WebCamTexture(devices[CameraIndex].name,960,540,30);webcam.Play();
   Message="相機啟動中…";float end=Time.realtimeSinceStartup+12;while(webcam&&webcam.width<32&&Time.realtimeSinceStartup<end)yield return null;
   Starting=false;opening=null;
   if(!webcam||webcam.width<32){StopCamera();Message="鏡頭未提供影像，請重試或更換鏡頭";yield break;}
   Message="請正面入鏡 · 上半身與全身都可追蹤，首次辨識需暖機";captureStarted=Time.realtimeSinceStartup;
  }
  public void NextCamera(){CameraIndex=(CameraIndex+1)%Mathf.Max(1,Devices.Length);if(!Demo)StartCamera();}
  public void Calibrate(){
   if(Demo)return;
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
   if(native)KP_ResetFocus();
#endif
   if(!Tracking||Time.realtimeSinceStartup-lastResult>.25f){anchored=false;Message="主角已重選 · 請讓雙肩留在鏡頭中央";return;}
   Anchor();Calibrated=true;Message=Framing+" · 定位已重設；雙手入鏡即可施術";
  }
  void Anchor(){referenceCenter=(ImagePoints[5]+ImagePoints[8])*.5f;lastCenter=referenceCenter;referenceSpan=ShoulderSpan();anchored=true;fullAnchored=false;RelativeSize=1;}
  float ShoulderSpan(){var d=ImagePoints[5]-ImagePoints[8];return Mathf.Max(.035f,new Vector2(d.x*16/9f,d.y).magnitude);}
  public void StopCamera(){if(opening!=null){StopCoroutine(opening);opening=null;}if(webcam){webcam.Stop();Destroy(webcam);webcam=null;}if(preview){Destroy(preview);preview=null;}Starting=Tracking=Calibrated=FullBodyVisible=HeadAimVisible=anchored=fullAnchored=havePose=false;sequence=0;Frames=0;lastResult=-100;
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
   if(native)KP_Reset();
#endif
  }
  public void AcceptObservation(float[] data){
   People=Mathf.RoundToInt(data[119]);InferenceMs=data[120];lastResult=Time.realtimeSinceStartup;Frames++;
   HeadAimVisible=data.Length>=128&&data[127]>.5f&&data[126]>=.5f;
   if(HeadAimVisible){float yaw=data[124],pitch=data[125];HeadDirection=new Vector3((Mirror?-1:1)*Mathf.Sin(yaw)*Mathf.Cos(pitch),-Mathf.Sin(pitch),Mathf.Cos(yaw)*Mathf.Cos(pitch)).normalized;}
   for(int i=0;i<17;i++){int n=i*7;Confidence[i]=data[n+6]>.5f?data[n+3]:0;ImagePoints[i]=new Vector2(data[n+4],data[n+5]);raw[i]=new Vector3((Mirror?-1:1)*data[n],data[n+1],-data[n+2]);}
   SubjectSelected=data.Length>122?data[122]>.5f:People==1;
   Tracking=SubjectSelected&&Confidence[5]>=JointThreshold&&Confidence[8]>=JointThreshold;
   FullBodyVisible=Tracking;foreach(int i in new[]{0,11,13,14,16})FullBodyVisible&=Confidence[i]>=JointThreshold;
   if(People>1&&!SubjectSelected){Tracking=false;Message="主角不明確 · 請靠近鏡頭中央，按 C 重選";return;}
   if(!Tracking){Message=People==0?"尚未找到人物 · 請正面入鏡":"主角暫時不清楚 · 請讓雙肩入鏡，或按 C 重選";return;}
   Vector2 center=(ImagePoints[5]+ImagePoints[8])*.5f;
   if(anchored&&Vector2.Distance(center,lastCenter)>.28f){Tracking=false;anchored=Calibrated=fullAnchored=false;Message="人物位置突然改變 · 暫停施術，重新定位中";return;}
   if(!anchored)Anchor();lastCenter=center;MapPose();
   if(!havePose){Array.Copy(target,Joints,17);havePose=true;}
   Message=Framing+(People>1?" · 主角已鎖定":"")+(HandsVisible?" · 雙手已辨識，可施術":" · 請把雙手留在鏡頭內")+(FullBodyVisible?"":" · 灰色腿部為站姿參考");
  }
  void Update(){
   if(Demo){DemoClock=(Time.time+DemoOffset)%38;DemoPose(DemoClock,Joints);for(int i=0;i<17;i++)Confidence[i]=1;Tracking=true;return;}
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
   if(native&&webcam&&webcam.width>32){
    ulong latest=KP_Copy(buffer,buffer.Length);if(latest!=0&&latest!=sequence){sequence=latest;AcceptObservation(buffer);}
    if(Time.realtimeSinceStartup-lastResult>.5f){Tracking=false;Message="等待骨架資料 · 請正面入鏡，首次辨識需數秒";}
    if(Time.realtimeSinceStartup>=nextSubmit&&webcam.didUpdateThisFrame){nextSubmit=Time.realtimeSinceStartup+.05f;Submit();}
   }
#endif
   if(Tracking){float blend=1-Mathf.Exp(-22*Time.unscaledDeltaTime);for(int i=0;i<17;i++)Joints[i]=Vector3.Lerp(Joints[i],target[i],blend);}
  }
  void MapPose(){
   var center=(ImagePoints[5]+ImagePoints[8])*.5f;float span=ShoulderSpan(),scale=.44f/span,sign=Mirror?-1:1;
   RelativeSize=Mathf.Clamp(span/Mathf.Max(.035f,referenceSpan),.65f,1.45f);
   float z=Mathf.Clamp(7.4f*(1/RelativeSize-1),-1.4f,2.4f);
   float x=Mathf.Clamp(((FullBodyVisible?ImagePoints[0].x:center.x)-referenceCenter.x)*16/9f*scale*sign,-1.8f,1.8f);
   // Keep bone lengths stable; size in the image changes perspective depth, not limb length.
   if(!FullBodyVisible)fullAnchored=false;
   if(FullBodyVisible&&!fullAnchored){referenceFootY=Mathf.Min(ImagePoints[13].y,ImagePoints[16].y);referenceFullSpan=span;fullAnchored=true;}
   Vector3 root=new Vector3(x,1,z);target[0]=root;
   var local=new Vector3[17];local[0]=Vector3.zero;
   for(int i=1;i<17;i++)local[i]=new Vector3((ImagePoints[i].x-center.x)*16/9f*scale*sign,(ImagePoints[i].y-center.y)*scale+.49f,FullBodyVisible?Mathf.Clamp(People==1?raw[i].z:0,-.5f,.5f):0);
   local[1]=new Vector3(0,.22f,0);local[2]=new Vector3(0,.55f,0);
   if(Confidence[3]<JointThreshold)local[3]=new Vector3(0,.73f,0);local[4]=local[3]+Vector3.up*.14f;
   if(FullBodyVisible){Vector3 hip=new Vector3((ImagePoints[0].x-center.x)*16/9f*scale*sign,(ImagePoints[0].y-center.y)*scale+.49f,0);for(int i=3;i<17;i++)local[i]-=hip;}
   local[2]=(local[5]+local[8])*.5f+Vector3.up*.06f;local[1]=local[2]*.4f;
   // Preserve the image-plane pose for silhouette gestures. Rendering still uses
   // the fixed-length rig below; inferred elbows must not count as observed ones.
   for(int i=0;i<17;i++)GestureJoints[i]=root+new Vector3(local[i].x,local[i].y,0);
   for(int i=1;i<17;i++){
    bool observed=i<5||(Confidence[i]>=JointThreshold&&(Parents[i]<5||Confidence[Parents[i]]>=JointThreshold));
    if(i>=11&&!FullBodyVisible)observed=false;
    if(observed){Vector3 delta=local[i]-local[Parents[i]];if(delta.sqrMagnitude>.0001f)directions[i]=delta.normalized;}
    else if(i>=11){directions[i]=i==11?new Vector3(-1,-.2f,0).normalized:i==14?new Vector3(1,-.2f,0).normalized:Vector3.down;}
    target[i]=target[Parents[i]]+directions[i]*lengths[i];
   }
   SolveLimb(5,6,7,root,local,new Vector3(-.3f,-.2f,0));SolveLimb(8,9,10,root,local,new Vector3(.3f,-.2f,0));
   if(FullBodyVisible){
    SolveLimb(11,12,13,root,local,Vector3.back);SolveLimb(14,15,16,root,local,Vector3.back);
    float expectedFoot=.5f+(referenceFootY-.5f)*span/Mathf.Max(.035f,referenceFullSpan);
    float lift=Mathf.Clamp((Mathf.Min(ImagePoints[13].y,ImagePoints[16].y)-expectedFoot)*scale,0,.7f);
    float correction=.05f+lift-Mathf.Min(target[13].y,target[16].y);for(int i=0;i<17;i++)target[i].y+=correction;
   }
  }
  void SolveLimb(int start,int middle,int end,Vector3 root,Vector3[] local,Vector3 fallback){
   if(Confidence[end]<JointThreshold)return;
   Vector3 a=target[start],delta=root+local[end]-a;if(delta.sqrMagnitude<.0001f)return;
   float l1=lengths[middle],l2=lengths[end],distance=Mathf.Clamp(delta.magnitude,Mathf.Abs(l1-l2)+.01f,l1+l2-.002f);Vector3 along=delta.normalized;
   Vector3 hint=Confidence[middle]>=JointThreshold?root+local[middle]-a:fallback;
   Vector3 bend=hint-along*Vector3.Dot(hint,along);if(bend.sqrMagnitude<.0001f)bend=Vector3.Cross(along,Vector3.forward);if(bend.sqrMagnitude<.0001f)bend=Vector3.right;
   float near=(l1*l1-l2*l2+distance*distance)/(2*distance),height=Mathf.Sqrt(Mathf.Max(0,l1*l1-near*near));
   target[middle]=a+along*near+bend.normalized*height;target[end]=a+along*distance;
  }
  void Submit(){
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
   const int w=640,h=360;if(rgba==null)rgba=new byte[w*h*4];pixels=webcam.GetPixels32(pixels);
   int sw=webcam.width,sh=webcam.height,rotation=webcam.videoRotationAngle;bool rotated=rotation==90||rotation==270;float aspect=rotated?(float)sh/sw:(float)sw/sh;
   for(int y=0;y<h;y++)for(int x=0;x<w;x++){float u=(x+.5f)/w,v=(y+.5f)/h;if(aspect>(float)w/h)v=(v-.5f)*aspect/((float)w/h)+.5f;else u=(u-.5f)*((float)w/h)/aspect+.5f;int n=(y*w+x)*4;
    if(u<0||u>1||v<0||v>1){rgba[n]=rgba[n+1]=rgba[n+2]=0;rgba[n+3]=255;continue;}
    float su=u,sv=v;if(rotation==90){su=v;sv=1-u;}else if(rotation==180){su=1-u;sv=1-v;}else if(rotation==270){su=1-v;sv=u;}if(webcam.videoVerticallyMirrored)sv=1-sv;
    Color32 c=pixels[Mathf.Clamp((int)(sv*sh),0,sh-1)*sw+Mathf.Clamp((int)(su*sw),0,sw-1)];rgba[n]=c.r;rgba[n+1]=c.g;rgba[n+2]=c.b;rgba[n+3]=255;
   }
   if(!preview)preview=new Texture2D(w,h,TextureFormat.RGBA32,false);preview.LoadRawTextureData(rgba);preview.Apply(false);KP_Submit(rgba,w,h);
#endif
  }
  public static void DemoPose(float t,Vector3[] p){
   float bob=Mathf.Sin(t*3)*.035f,x=Mathf.Sin(t*.75f)*.35f;Vector3 root=new Vector3(x,1+bob,0);
   p[0]=root;p[1]=root+new Vector3(0,.22f,0);p[2]=root+new Vector3(0,.55f,0);p[3]=root+new Vector3(0,.73f,0);p[4]=root+new Vector3(0,.87f,0);
   p[5]=root+new Vector3(-.22f,.49f,0);p[8]=root+new Vector3(.22f,.49f,0);
   p[6]=root+new Vector3(-.47f,.28f,Mathf.Sin(t*2)*.16f);p[9]=root+new Vector3(.47f,.30f,-Mathf.Sin(t*2)*.16f);
   p[7]=root+new Vector3(-.65f,.4f+Mathf.Sin(t*2)*.25f,-.18f);p[10]=root+new Vector3(.65f,.4f-Mathf.Sin(t*2)*.25f,.18f);
   p[11]=root+new Vector3(-.13f,-.04f,0);p[14]=root+new Vector3(.13f,-.04f,0);
   p[12]=root+new Vector3(-.18f,-.48f,Mathf.Sin(t*2)*.12f);p[15]=root+new Vector3(.18f,-.48f,-Mathf.Sin(t*2)*.12f);
   p[13]=new Vector3(x-.25f,.06f,Mathf.Sin(t*2)*.12f);p[16]=new Vector3(x+.25f,.06f,-Mathf.Sin(t*2)*.12f);
   if(t>=4&&t<6.2f){float a=(t-4)*Mathf.PI*2/1.8f;p[10]=root+new Vector3(.30f,.23f,-.18f);p[7]=p[10]+new Vector3(Mathf.Cos(a)*.16f,Mathf.Sin(a)*.16f,-.02f);p[9]=root+new Vector3(.37f,.30f,-.12f);p[6]=root+new Vector3(.02f,.34f,-.15f);}
   if(t>=6.2f&&t<7.3f){p[10]=root+new Vector3(.20f,1.08f,-.15f);p[9]=root+new Vector3(.36f,.78f,-.12f);p[7]=root+new Vector3(-.24f,.08f,0);}
   if(t>=7.3f&&t<8.2f){float a=Mathf.Clamp01((t-7.3f)/.22f);p[10]=root+Vector3.Lerp(new Vector3(.20f,1.08f,-.15f),new Vector3(.50f,.38f,-.5f),a);p[9]=root+new Vector3(.38f,.5f,-.25f);p[7]=root+new Vector3(-.24f,.08f,0);}
   if(t>=10&&t<11.2f){p[7]=root+new Vector3(-.24f,.06f,-.1f);float draw=Mathf.Clamp01((t-10.6f)/.4f);p[10]=Vector3.Lerp(p[7]+new Vector3(.04f,.02f,-.03f),root+new Vector3(.50f,.65f,-.15f),draw);p[6]=root+new Vector3(-.30f,.25f,-.08f);p[9]=root+new Vector3(.10f,.34f,-.13f);}
   if(t>=11.2f&&t<14.2f){float swing=Mathf.Sin((t-11.2f)*8);p[10]=root+new Vector3(swing*.85f,.6f+Mathf.Cos(t*8)*.2f,-.4f);p[9]=root+new Vector3(.3f,.45f,-.2f);}
   if(t>=15&&t<16.4f){
    p[1]=root+new Vector3(.12f,.18f,-.08f);p[2]=root+new Vector3(.30f,.43f,-.14f);
    p[3]=root+new Vector3(.40f,.56f,-.23f);p[4]=root+new Vector3(.46f,.68f,-.28f);
    p[5]=root+new Vector3(.07f,.44f,-.15f);p[8]=root+new Vector3(.47f,.34f,-.11f);
    p[6]=p[5]+new Vector3(-.25f,-.10f,.15f);p[7]=p[5]+new Vector3(-.47f,-.22f,.27f);
    p[9]=p[8]+new Vector3(-.25f,-.10f,.15f);p[10]=p[8]+new Vector3(-.47f,-.22f,.27f);
   }
   if(t>=17&&t<19){p[7]=root+new Vector3(-.24f,.06f,-.1f);float sheath=Mathf.Clamp01((t-17)/.6f);p[10]=Vector3.Lerp(root+new Vector3(.4f,.5f,-.2f),p[7]+new Vector3(.04f,.02f,-.03f),sheath);p[6]=root+new Vector3(-.3f,.25f,-.08f);p[9]=root+new Vector3(.05f,.36f,-.15f);}
   if(t>=21&&t<22.8f){p[7]=root+new Vector3(-.055f,1.05f,0);p[10]=root+new Vector3(.055f,1.05f,0);p[6]=root+new Vector3(-.35f,.8f,0);p[9]=root+new Vector3(.35f,.8f,0);}
   if(t>=22.8f&&t<24){p[7]=root+new Vector3(-.85f,.9f,0);p[10]=root+new Vector3(.85f,.9f,0);}
   if(t>=29&&t<32.3f){p[7]=root+new Vector3(-.05f,.35f,-.22f);p[10]=root+new Vector3(.05f,.35f,-.22f);}
   if(t>=32.3f&&t<33.2f){p[7]=root+new Vector3(-.6f,.45f,-.5f);p[10]=root+new Vector3(.6f,.45f,-.5f);}
   if(t>=34&&t<37.6f){p[7]=root+new Vector3(-.04f,.66f,-.15f);p[10]=root+new Vector3(.04f,.66f,-.15f);p[6]=root+new Vector3(-.30f,.40f,-.1f);p[9]=root+new Vector3(.30f,.40f,-.1f);}
  }
  void OnDestroy(){StopCamera();}
 }
}
