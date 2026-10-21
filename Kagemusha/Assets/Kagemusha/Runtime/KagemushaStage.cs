using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kagemusha {
 public sealed partial class KagemushaStage:MonoBehaviour {
  public Shader GlowShader,SparkShader,NinjaShader,FlameShader;
  public Font StageFont;
  public PoseSource Source {get;private set;}
  public GestureController Gestures {get;private set;}
  public int Hits,Wave=1;
  public int EffectCount=>shots.Count+waves.Count+ghosts.Count;
  public string LastEvent="起場 · 動作喚醒舞台";
  public bool AutoShow=true,ShowControls=true;
  public float StageTime=>Time.time;
  public int Frame=>Time.frameCount;
  public readonly Dictionary<string,int> Events=new Dictionary<string,int>();
  public Color Cyan=new Color(.12f,1.5f,1.8f),Gold=new Color(2.3f,1.2f,.25f),Fire=new Color(3,.34f,.06f),Water=new Color(.08f,.9f,2.5f);
  Material bodyMat,untrackedMat,goldMat,mutedMat,floorMat,fireMat,waterMat,ghostMat,enemyMat;
  SkeletonView body;Transform weapon,chargeOrb;TrailRenderer leftTrail,rightTrail,swordTrail;
  LineRenderer ring,chargeRing;ParticleSystem particles;
  Camera stageCamera;Font font;GUIStyle title,small,label,button,big;
  Vector3[] visible=new Vector3[17];float[] displayQuality=new float[17];float cloneUntil,dragonUntil,startTime,lastTrail=-1,lastGhost,fps,nextShot,nextStatus;
  bool orbit;int lastAct=-1;float cameraShake;
  readonly List<Shot> shots=new List<Shot>();readonly List<WaveFx> waves=new List<WaveFx>();readonly List<Ghost> ghosts=new List<Ghost>();readonly List<Enemy> enemies=new List<Enemy>();
  Transform dragon;Transform[] dragonParts;LineRenderer dragonSpine;
  class Shot{public Transform root;public Vector3 velocity;public float life,power;public Color color;public RasenganVisual rasengan;public DragonRig waterDragon;}
  class WaveFx{public LineRenderer line;public float born,duration,radius;public Color color;public Vector3 at;}
  class Ghost{public SkeletonView view;public float born;public Material mat;public bool clone;public Vector3 offset;}
  class Enemy{public Transform root,core;public LineRenderer shield;public Vector3 spawn;public int hp=3;public float hit,dead=-1;}
  void Awake(){
   Application.runInBackground=true;Application.targetFrameRate=60;QualitySettings.vSyncCount=1;Source=gameObject.AddComponent<PoseSource>();Gestures=new GestureController();Gestures.Fired+=OnGestureSkill;
   stageCamera=Camera.main;if(!stageCamera){var c=new GameObject("Stage Camera",typeof(Camera),typeof(AudioListener));c.tag="MainCamera";stageCamera=c.GetComponent<Camera>();}
   stageCamera.transform.position=new Vector3(0,2.5f,-7.4f);stageCamera.transform.LookAt(new Vector3(0,1.3f,.7f));stageCamera.fieldOfView=46;stageCamera.nearClipPlane=.05f;stageCamera.farClipPlane=100;stageCamera.backgroundColor=new Color(.008f,.014f,.023f);stageCamera.clearFlags=CameraClearFlags.SolidColor;stageCamera.allowHDR=true;
   stageCamera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
   bodyMat=Mat(Cyan);untrackedMat=Mat(new Color(.12f,.17f,.20f,.60f));goldMat=Mat(Gold);mutedMat=Mat(new Color(.035f,.15f,.18f));fireMat=Mat(Fire);waterMat=Mat(Water);ghostMat=Mat(new Color(.1f,.6f,.8f,.28f));enemyMat=Mat(new Color(1.4f,.06f,.19f));
   BuildStage();body=new SkeletonView("Avatar Rig · 17 joints",transform,bodyMat);weapon=BuildKatana();weapon.gameObject.SetActive(false);
   leftTrail=Trail("Left hand energy",Cyan,.055f,1.1f);rightTrail=Trail("Right hand energy",Gold,.055f,1.1f);swordTrail=Trail("Katana trail",Gold,.10f,.36f);
   chargeOrb=SkeletonView.Primitive(PrimitiveType.Sphere,"Charge core",transform,fireMat);chargeOrb.gameObject.SetActive(false);chargeRing=Ring("Charge orbit",goldMat,64);chargeRing.gameObject.SetActive(false);
   BuildParticles();BuildJutsuArt();BuildDragon();SpawnEnemies();RingWave(Vector3.zero,Gold,3.8f,4);startTime=Time.time;
   font=StageFont?StageFont:Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
  }
  Material Mat(Color c){var m=new Material(GlowShader?GlowShader:Shader.Find("Kagemusha/Glow"));m.color=c;return m;}
  Transform Prop(PrimitiveType type,string name,Vector3 position,Vector3 scale,Material mat){var t=SkeletonView.Primitive(type,name,transform,mat);t.localPosition=position;t.localScale=scale;return t;}
  LineRenderer Line(string name,Material mat,float width=.012f){var line=new GameObject(name,typeof(LineRenderer)).GetComponent<LineRenderer>();line.transform.SetParent(transform);line.sharedMaterial=mat;line.useWorldSpace=true;line.widthMultiplier=width;line.numCapVertices=3;return line;}
  LineRenderer Ring(string name,Material mat,int count=128){var l=Line(name,mat,.018f);l.loop=true;l.positionCount=count;return l;}
  void Circle(LineRenderer l,Vector3 at,float radius,bool vertical=false,float phase=0){for(int i=0;i<l.positionCount;i++){float a=i*Mathf.PI*2/l.positionCount+phase;l.SetPosition(i,at+(vertical?new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0):new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius)));}}
  TrailRenderer Trail(string name,Color color,float width,float lifetime){var t=new GameObject(name,typeof(TrailRenderer)).GetComponent<TrailRenderer>();t.transform.SetParent(transform);t.sharedMaterial=Mat(color);t.time=lifetime;t.startWidth=width;t.endWidth=0;t.minVertexDistance=.025f;t.numCapVertices=3;t.emitting=false;return t;}
  void BuildStage(){
   floorMat=Mat(new Color(.012f,.025f,.035f));
   Prop(PrimitiveType.Cube,"Obsidian stage",new Vector3(0,-.12f,2),new Vector3(22,.2f,28),floorMat);
   for(int i=-12;i<=12;i++){var l=Line("Stage grid",mutedMat,.007f);l.positionCount=2;l.SetPositions(new[]{new Vector3(i*.6f,.001f,-6),new Vector3(i*.6f,.001f,15)});}
   for(int i=-10;i<=24;i++){var l=Line("Stage grid",mutedMat,.007f);l.positionCount=2;l.SetPositions(new[]{new Vector3(-7,.001f,i*.6f),new Vector3(7,.001f,i*.6f)});}
   ring=Ring("Summoning circle",goldMat,160);Circle(ring,new Vector3(0,.013f,0),1.65f);
   for(int i=0;i<3;i++){var l=Ring("Arcane orbit",i==0?mutedMat:goldMat,128);Circle(l,new Vector3(0,.012f,0),1.72f+i*.18f);}
   for(int i=0;i<32;i++){float a=i*Mathf.PI*2/32;var l=Line("Circle rune",goldMat,.018f);l.positionCount=2;l.SetPositions(new[]{new Vector3(Mathf.Cos(a)*1.75f,.02f,Mathf.Sin(a)*1.75f),new Vector3(Mathf.Cos(a)*1.86f,.02f,Mathf.Sin(a)*1.86f)});}
   var dark=Mat(new Color(.025f,.055f,.08f));
   for(int n=0;n<4;n++){float z=3+n*3.4f,w=3.6f+n*.3f;for(int sign=-1;sign<=1;sign+=2){Prop(PrimitiveType.Cube,"Torii pillar",new Vector3(sign*w,2.5f,z),new Vector3(.18f,5,.18f),dark);Prop(PrimitiveType.Cube,"Gold seam",new Vector3(sign*w-.02f,2.5f,z-.105f),new Vector3(.018f,5,.018f),goldMat);}Prop(PrimitiveType.Cube,"Torii lintel",new Vector3(0,4.8f,z),new Vector3(w*2+.6f,.14f,.24f),dark);Prop(PrimitiveType.Cube,"Lintel light",new Vector3(0,4.72f,z-.13f),new Vector3(w*2+.6f,.025f,.025f),goldMat);}
   for(int i=0;i<12;i++){float a=i*Mathf.PI*2/12;Vector3 at=new Vector3(Mathf.Cos(a)*4.5f,.15f,Mathf.Sin(a)*4.5f+2);Prop(PrimitiveType.Sphere,"Lantern",at,Vector3.one*.06f,goldMat);}
   var light=new GameObject("Cool stage light",typeof(Light)).GetComponent<Light>();light.type=LightType.Directional;light.color=new Color(.25f,.5f,.7f);light.intensity=1.2f;light.transform.rotation=Quaternion.Euler(40,-25,0);RenderSettings.ambientLight=new Color(.1f,.15f,.2f);
   var moon=Ring("Moon gate",mutedMat,180);Circle(moon,new Vector3(0,3.2f,10),2.5f,true);
  }
  void BuildParticles(){
   particles=new GameObject("Energy particles",typeof(ParticleSystem)).GetComponent<ParticleSystem>();particles.transform.SetParent(transform);var main=particles.main;main.playOnAwake=false;main.loop=false;main.simulationSpace=ParticleSystemSimulationSpace.World;main.startLifetime=2;main.startSize=.055f;main.startSpeed=0;main.maxParticles=6000;main.gravityModifier=.12f;var emission=particles.emission;emission.enabled=false;var shape=particles.shape;shape.enabled=false;var size=particles.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,0));var col=particles.colorOverLifetime;col.enabled=true;var g=new Gradient();g.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});col.color=g;particles.GetComponent<ParticleSystemRenderer>().sharedMaterial=new Material(SparkShader?SparkShader:Shader.Find("Kagemusha/Spark"));particles.Play();
  }
  void Emit(Vector3 at,Color color,int count,float speed=1.8f){for(int i=0;i<count;i++){var e=new ParticleSystem.EmitParams{position=at,velocity=UnityEngine.Random.onUnitSphere*UnityEngine.Random.Range(.1f,speed),startColor=color,startSize=UnityEngine.Random.Range(.025f,.075f),startLifetime=UnityEngine.Random.Range(.5f,2.2f)};particles.Emit(e,1);}}
  void BuildDragon(){summonedDragon=new DragonRig("Summoned chakra dragon",transform,ArtMat(new Color(.10f,.65f,.44f)),ArtMat(new Color(.90f,.86f,.60f)),ArtMat(new Color(.90f,.70f,.23f)),ArtMat(new Color(2.1f,.24f,.10f)),ArtMat(new Color(.025f,.045f,.055f)));summonedDragon.Root.gameObject.SetActive(false);}
  void SpawnEnemies(){foreach(var e in enemies)Destroy(e.root.gameObject);enemies.Clear();for(int i=0;i<3;i++){var root=new GameObject("Sentinel "+i).transform;root.SetParent(transform);Vector3 at=new Vector3(i==0?-2.3f:i==1?2.3f:0,1.05f,i==2?3.7f:1.8f);root.position=at;
    var core=SkeletonView.Primitive(PrimitiveType.Cube,"Sentinel core",root,enemyMat);core.localScale=Vector3.one*.24f;
    for(int side=-1;side<=1;side+=2){var horn=SkeletonView.Primitive(PrimitiveType.Cylinder,"Horn",root,goldMat);horn.localPosition=new Vector3(side*.14f,.24f,0);horn.localRotation=Quaternion.Euler(0,0,-side*30);horn.localScale=new Vector3(.025f,.16f,.025f);}
    var shield=Ring("Sentinel shield",enemyMat,6);enemies.Add(new Enemy{root=root,core=core,shield=shield,spawn=at});}}
  void Update(){
   float t=Time.time,dt=Mathf.Min(.05f,Time.deltaTime);fps=Mathf.Lerp(fps,1/Mathf.Max(.001f,Time.unscaledDeltaTime),.05f);
   // Source runs on the same object; LateUpdate consumes the completed pose.
   HandleKeys(t);UpdateEffects(t,dt);
   if(Time.realtimeSinceStartup>nextStatus){nextStatus=Time.realtimeSinceStartup+1;try{File.WriteAllText(Path.Combine(Application.persistentDataPath,"tracking-status.json"),Snapshot());}catch(IOException){}}
   if(Source.Demo&&AutoShow){int act=Source.DemoClock<10?0:Source.DemoClock<15?1:Source.DemoClock<21?2:Source.DemoClock<29?3:4;if(act!=lastAct){lastAct=act;RingWave(Vector3.zero,act==3?Water:Gold,2.8f,2);}}
   if(orbit){Vector3 at=new Vector3(Mathf.Sin(t*.3f)*1.8f,2.5f,-7.4f);stageCamera.transform.position=at;stageCamera.transform.LookAt(new Vector3(0,1.3f,.7f));}
   else{stageCamera.transform.position=Vector3.Lerp(stageCamera.transform.position,new Vector3(0,2.5f,-7.4f),dt*5);stageCamera.transform.LookAt(new Vector3(0,1.3f,.7f));}
   if(cameraShake>0){cameraShake=Mathf.Max(0,cameraShake-dt*.3f);stageCamera.transform.position+=UnityEngine.Random.insideUnitSphere*cameraShake;}
  }
  void LateUpdate(){
   float t=Time.time,dt=Mathf.Min(.05f,Time.deltaTime);bool ready=Source.CanCast;
   for(int i=0;i<17;i++)visible[i]=Source.Joints[i];
   Gestures.Update(visible,Source.Confidence,ready&&(!Tutorial.Active||Tutorial.Practicing),t,dt,Source.Demo?null:Source.GestureJoints,Source.HeadAimVisible?(Vector3?)Source.HeadDirection:null);UpdateTutorial(t);body.Update(visible,Vector3.zero);for(int i=0;i<17;i++)displayQuality[i]=!Source.Demo&&!Source.FullBodyVisible&&(i==0||i>=11)?0:Source.Confidence[i];body.SetQuality(displayQuality,untrackedMat);
   bodyMat.color=ready?Cyan:new Color(.05f,.20f,.25f,.4f);
   bool trails=ready;leftTrail.emitting=trails&&Source.Confidence[7]>=PoseSource.JointThreshold;rightTrail.emitting=trails&&Source.Confidence[10]>=PoseSource.JointThreshold;leftTrail.transform.position=visible[7];rightTrail.transform.position=visible[10];
   if(!trails){leftTrail.Clear();rightTrail.Clear();}
   Vector3 hand=visible[10],tip=hand+(hand-visible[9]).normalized*.85f;
   weapon.gameObject.SetActive(Gestures.Sword&&ready);if(weapon.gameObject.activeSelf){weapon.position=hand;weapon.rotation=Quaternion.FromToRotation(Vector3.up,(tip-hand).normalized);}swordTrail.emitting=weapon.gameObject.activeSelf;swordTrail.transform.position=tip;
   float power=Gestures.Power;Vector3 center=(visible[7]+visible[10])*.5f;UpdateJutsuArt(t,dt,ready);
   chargeOrb.gameObject.SetActive(power>0&&ready&&Gestures.Element=="water");chargeRing.gameObject.SetActive(power>0&&ready&&Gestures.Element=="water");
   if(chargeOrb.gameObject.activeSelf){chargeOrb.position=center;chargeOrb.localScale=Vector3.one*(.07f+power*.28f)*(1+Mathf.Sin(t*18)*.06f);chargeOrb.GetComponent<Renderer>().sharedMaterial=Gestures.Element=="water"?waterMat:fireMat;Circle(chargeRing,center,.15f+power*.23f,true,t*3);Emit(center+UnityEngine.Random.onUnitSphere*(.2f+power*.25f),Gestures.Element=="water"?Water:Fire,2,.2f);}
   if(ready&&t-lastTrail>.06f){lastTrail=t;Emit(visible[7],Cyan,1,.2f);Emit(visible[10],Gold,1,.2f);}
  }
  void HandleKeys(float t){
   TutorialKeys();
   if(Input.GetKeyDown(KeyCode.Tab))ShowControls=!ShowControls;
   if(Input.GetKeyDown(KeyCode.O))orbit=!orbit;
   if(Input.GetKeyDown(KeyCode.C)){Source.Calibrate();Gestures.Reset();if(Tutorial.Active&&Tutorial.Practicing)Tutorial.Practice(t);}
   if(Input.GetKeyDown(KeyCode.D)){if(Tutorial.Active)ExitTutorial();Source.UseDemo();Gestures.Reset();}
   if(Input.GetKeyDown(KeyCode.V)){Source.StartCamera();Gestures.Reset();if(Tutorial.Active&&Tutorial.Practicing)Tutorial.Practice(t);}
   if(Input.GetKeyDown(KeyCode.Space)){ClearEffects();if(Tutorial.Active&&Tutorial.Practicing)Tutorial.Practice(t);}
   if(Tutorial.Active)return;
   if(Input.GetKeyDown(KeyCode.Alpha1))OnSkill("rasengan",1);
   if(Input.GetKeyDown(KeyCode.Alpha2))OnSkill("water",1);
   if(Input.GetKeyDown(KeyCode.Alpha3))OnSkill("slash",1);
   if(Input.GetKeyDown(KeyCode.Alpha4))OnSkill("clone",1);
   if(Input.GetKeyDown(KeyCode.Alpha5))OnSkill("dragon",1);
   if(Input.GetKeyDown(KeyCode.Alpha6))OnSkill("fire",1);
  }
  public void Trigger(string id){OnSkill(id,1);}
  void OnSkill(string id,float power){
   Events[id]=Events.TryGetValue(id,out int n)?n+1:1;float t=Time.time;
   if(id=="select"){LastEvent=Gestures.Element=="water"?"水遁 · 結印集氣":"螺旋丸 · 繞圈集氣";RingWave(visible[0],Gold,.8f,.6f);return;}
   if(id=="equip"){LastEvent=power>0?"拔刀 · 揮臂斬擊":"收刀";Emit(visible[10],Gold,30);return;}
   if(id=="formed"){LastEvent="螺旋丸 · 旋流成形";Emit(visible[10],new Color(.5f,2,3),65);return;}
   if(id=="throw-ready"){LastEvent="螺旋丸 · 舉起待投";return;}
   if(id=="fire"){flamePreviewUntil=Gestures.FlameActive?0:t+2.5f;LastEvent="火遁 · 嘴前噴射";Emit(visible[3],new Color(3,.65f,.08f),45);return;}
   if(id=="rasengan"||id=="water"){
    Vector3 at=id=="rasengan"?visible[10]:(visible[7]+visible[10])*.5f;Color color=id=="rasengan"?new Color(.45f,2,3):Water;
    var root=new GameObject(id=="water"?"Water dragon projectile":"Rasengan projectile").transform;root.SetParent(transform);root.position=at;root.rotation=Quaternion.LookRotation(Gestures.AimDirection);
    var shot=new Shot{root=root,velocity=Gestures.AimDirection*4.2f,life=4,power=power,color=color};
    if(id=="rasengan")shot.rasengan=new RasenganVisual(root,rasenganCoreMat,whiteInk,waterMat);
    else shot.waterDragon=new DragonRig("Water dragon",root,waterSkin,waterFoam,waterFoam,whiteInk,darkInk);
    shots.Add(shot);Emit(at,color,90);RingWave(at,color,1.4f,.7f);LastEvent=(id=="rasengan"?"螺旋丸":"水遁・水龍彈")+" · 釋放";cameraShake=.025f;
   }
   if(id=="slash"){
    LastEvent="拔刀斬 · 金色刀弧";var l=Line("Slash arc",goldMat,.05f);l.positionCount=40;for(int i=0;i<40;i++){float a=-1.3f+i/39f*2.6f;l.SetPosition(i,visible[0]+new Vector3(Mathf.Sin(a)*1.1f,.5f+Mathf.Cos(a)*.35f,-.3f));}Destroy(l.gameObject,.28f);Emit(visible[10],Gold,50,2.4f);
    Vector3 segmentStart=visible[9],segmentEnd=visible[10]+(visible[10]-visible[9]).normalized*.85f;
    foreach(var e in enemies)if(e.hp>0&&DistanceToSegment(e.root.position,segmentStart,segmentEnd)<.65f)Damage(e,2,t);
   }
   if(id=="clone"){LastEvent="影分身 · 白煙現身";cloneUntil=t+5;CreateGhost(new Vector3(-.95f,0,.25f),t,true);CreateGhost(new Vector3(.95f,0,.25f),t,true);RingWave(Vector3.zero,Cyan,3,1.5f);}
   if(id=="dragon"){LastEvent="龍脈召喚 · 龍身盤旋現身";dragonUntil=t+8;RingWave(Vector3.zero,Water,5,2);Emit(new Vector3(0,.2f,0),Water,250,3);cameraShake=.035f;}
  }
  static float DistanceToSegment(Vector3 p,Vector3 a,Vector3 b){Vector3 ab=b-a;return Vector3.Distance(p,a+ab*Mathf.Clamp01(Vector3.Dot(p-a,ab)/Mathf.Max(.00001f,ab.sqrMagnitude)));}
  void Damage(Enemy e,int amount,float t){if(e.hp<=0||t-e.hit<.15f)return;e.hp=Mathf.Max(0,e.hp-amount);e.hit=t;Hits++;Emit(e.root.position,Gold,100,2.3f);RingWave(e.root.position,Gold,.7f,.55f);if(e.hp==0){e.dead=t;Destroy(e.shield.gameObject);}}
  void RingWave(Vector3 at,Color color,float radius,float duration){var mat=Mat(color);var l=Ring("Energy ripple",mat,96);waves.Add(new WaveFx{line=l,born=Time.time,duration=duration,radius=radius,color=color,at=at});}
  void CreateGhost(Vector3 offset,float t,bool clone=false){while(ghosts.Count>=6){var g=ghosts[0];Destroy(g.view.Root.gameObject);Destroy(g.mat);ghosts.RemoveAt(0);}var m=Mat(new Color(.15f,1.1f,1.4f,clone?.9f:.35f));var v=new SkeletonView("Shadow clone",transform,m,.028f);v.Update(visible,offset,.028f);ghosts.Add(new Ghost{view=v,born=t,mat=m,clone=clone,offset=offset});if(clone)Smoke(visible[0]+offset,t);}
  void UpdateEffects(float t,float dt){
   for(int i=shots.Count-1;i>=0;i--){var s=shots[i];s.life-=dt;Vector3 old=s.root.position;s.root.position+=s.velocity*dt;if(s.rasengan!=null)s.rasengan.Update(t,1);if(s.waterDragon!=null)s.waterDragon.Water(t);Emit(s.root.position,s.color,3,.15f);foreach(var e in enemies)if(e.hp>0&&DistanceToSegment(e.root.position,old,s.root.position)<.4f){Damage(e,1+Mathf.FloorToInt(s.power*2),t);s.life=0;break;}if(s.life<=0){Destroy(s.root.gameObject);shots.RemoveAt(i);}}
   for(int i=waves.Count-1;i>=0;i--){var w=waves[i];float age=(t-w.born)/w.duration;if(age>=1){Destroy(w.line.sharedMaterial);Destroy(w.line.gameObject);waves.RemoveAt(i);continue;}Circle(w.line,w.at+Vector3.up*.025f,Mathf.Lerp(.1f,w.radius,1-Mathf.Pow(1-age,2)),w.at.y>.3f);w.line.sharedMaterial.color=new Color(w.color.r,w.color.g,w.color.b,(1-age)*.8f);}
   for(int i=ghosts.Count-1;i>=0;i--){var g=ghosts[i];float age=(t-g.born)/(g.clone?5:3);if(g.clone&&Source.CanCast)g.view.Update(visible,g.offset,.028f);g.mat.color=new Color(.15f,1.1f,1.4f,(1-Mathf.Clamp01((age-.8f)*5))*(g.clone?.9f:.32f));if(age>=1){if(g.clone)Smoke(visible[0]+g.offset,t);Destroy(g.view.Root.gameObject);Destroy(g.mat);ghosts.RemoveAt(i);}}
   bool allDead=true;foreach(var e in enemies){if(e.hp>0){allDead=false;e.root.position=e.spawn+new Vector3(0,Mathf.Sin(t*1.4f+e.spawn.x)*.12f,0);e.core.Rotate(20*dt,45*dt,30*dt);Circle(e.shield,e.root.position,.34f,true,t*.25f);e.shield.widthMultiplier=t-e.hit<.25f?.045f:.012f;}else if(e.root){float a=Mathf.Clamp01((t-e.dead)/.6f);e.root.localScale=Vector3.one*(1-a);}}
   if(allDead&&t>nextShot){nextShot=t+4;SpawnEnemies();Wave++;}
   bool active=t<dragonUntil;summonedDragon.Root.gameObject.SetActive(active);
   if(active)summonedDragon.Summon(t,Mathf.Clamp01((8-(dragonUntil-t))/1.2f),Mathf.Clamp01((dragonUntil-t)/.8f));
  }

  public void ClearEffects(){foreach(var s in shots)Destroy(s.root.gameObject);shots.Clear();foreach(var w in waves){Destroy(w.line.sharedMaterial);Destroy(w.line.gameObject);}waves.Clear();foreach(var g in ghosts){Destroy(g.view.Root.gameObject);Destroy(g.mat);}ghosts.Clear();particles.Clear();leftTrail.Clear();rightTrail.Clear();swordTrail.Clear();dragonUntil=cloneUntil=flamePreviewUntil=0;ClearJutsuArt();Gestures.Reset();LastEvent="舞台已清理 · 重新待命";}
  public string Snapshot(){return JsonUtility.ToJson(new Metrics{frame=Time.frameCount,time=Time.time,mode=Source.Demo?"synthetic-demo":Source.FullBodyVisible?"camera-full":"camera-upper",ready=Source.CanCast,tracking=Source.Tracking,subjectSelected=Source.SubjectSelected,inferenceMs=Source.InferenceMs,nativeFrames=Source.Frames,people=Source.People,releases=Gestures.Releases,slashes=Gestures.Slashes,summons=Gestures.Summons,clones=Gestures.Clones,hits=Hits,last=LastEvent,message=Source.Message,quality=Source.Confidence,relativeSize=Source.RelativeSize,leftWrist=Source.Joints[7],rightWrist=Source.Joints[10],root=Source.Joints[0],rasenganStage=Gestures.RasenganStage,flame=Gestures.FlameActive,flameDirection=Gestures.FlameDirection,headAimVisible=Source.HeadAimVisible,tutorial=Tutorial.Active,practicing=Tutorial.Practicing,lesson=Tutorial.Lesson,tutorialStep=Tutorial.Step,tutorialSuccess=Tutorial.Success,learned=Tutorial.CompletedCount,tutorialFeedback=Tutorial.Feedback});}
  [Serializable]class Metrics{public int frame,nativeFrames,people,releases,slashes,summons,clones,hits,lesson,tutorialStep,learned,rasenganStage;public float time,inferenceMs,relativeSize;public bool ready,tracking,subjectSelected,tutorial,practicing,tutorialSuccess,flame,headAimVisible;public string mode,last,message,tutorialFeedback;public float[] quality;public Vector3 leftWrist,rightWrist,root,flameDirection;}
  void PreviewLine(Vector2 a,Vector2 b,Color color){var matrix=GUI.matrix;GUIUtility.RotateAroundPivot(Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg,a);GUI.color=color;GUI.DrawTexture(new Rect(a.x,a.y,Vector2.Distance(a,b),1.5f),Texture2D.whiteTexture);GUI.matrix=matrix;GUI.color=Color.white;}
  void DrawPreview(Rect r){
   GUI.DrawTextureWithTexCoords(r,Source.Preview,Source.Mirror?new Rect(1,0,-1,1):new Rect(0,0,1,1));
   var points=new Vector2[17];for(int i=0;i<17;i++){var p=Source.ImagePoints[i];points[i]=new Vector2(r.x+(Source.Mirror?1-p.x:p.x)*r.width,r.y+(1-p.y)*r.height);}
   for(int i=1;i<17;i++)if(Source.Confidence[i]>=PoseSource.JointThreshold&&Source.Confidence[PoseSource.Parents[i]]>=PoseSource.JointThreshold)PreviewLine(points[PoseSource.Parents[i]],points[i],new Color(.2f,1,.6f));
   for(int i=0;i<17;i++)if(Source.Confidence[i]>=PoseSource.JointThreshold){GUI.color=i==7||i==10?Color.yellow:Color.green;GUI.DrawTexture(new Rect(points[i].x-2.5f,points[i].y-2.5f,5,5),Texture2D.whiteTexture);}GUI.color=Color.white;
  }
  Texture2D Swatch(Color c){var texture=new Texture2D(1,1);texture.SetPixel(0,0,c);texture.Apply();return texture;}
  void InitStyles(){if(title!=null)return;title=new GUIStyle(GUI.skin.label){font=font,fontSize=25,fontStyle=FontStyle.Bold};title.normal.textColor=new Color(.88f,.94f,.94f);small=new GUIStyle(GUI.skin.label){font=font,fontSize=12};small.normal.textColor=new Color(.52f,.66f,.71f);label=new GUIStyle(GUI.skin.label){font=font,fontSize=15,wordWrap=true};label.normal.textColor=new Color(.8f,.9f,.91f);button=new GUIStyle(GUI.skin.button){font=font,fontSize=13,alignment=TextAnchor.MiddleCenter,padding=new RectOffset(6,6,5,5)};button.normal.background=Swatch(new Color(.08f,.16f,.19f));button.hover.background=Swatch(new Color(.13f,.27f,.30f));button.active.background=Swatch(new Color(.28f,.27f,.17f));button.normal.textColor=new Color(.85f,.94f,.94f);button.hover.textColor=Color.white;big=new GUIStyle(label){fontSize=20,fontStyle=FontStyle.Bold};}
  void OnGUI(){
   if(Source==null||Gestures==null)return;InitStyles();float scale=Mathf.Min(Screen.width/1440f,Screen.height/900f);GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,Vector3.one*scale);float width=Screen.width/scale,height=Screen.height/scale;
   GUI.color=new Color(.015f,.03f,.045f,.93f);GUI.DrawTexture(new Rect(0,0,width,104),Texture2D.whiteTexture);GUI.color=Color.white;
   GUI.Label(new Rect(36,18,900,34),"影武者  /  KAGEMUSHA",title);GUI.Label(new Rect(37,56,900,25),"武士 × 忍者　｜　3D 骨架互動舞台　／　PROTOTYPE 04.2",small);
   if(GUI.Button(new Rect(width-470,25,172,34),Tutorial.Active?"回到自由演出 T":"教學測試模式 T",button)){if(Tutorial.Active)ExitTutorial();else BeginTutorial();}
   GUI.Label(new Rect(width-275,20,240,25),Source.Demo?"● 自動演示 · 合成骨架":"● "+Source.Framing+" · "+(Source.Tracking?"跟隨中":"等待人物"),label);GUI.Label(new Rect(width-275,50,240,25),Mathf.RoundToInt(fps)+" FPS　·　第 "+Wave+" 波　·　命中 "+Hits,small);
   GUI.color=new Color(.015f,.03f,.045f,.9f);GUI.DrawTexture(new Rect(0,height-95,width,95),Texture2D.whiteTexture);GUI.color=Color.white;
   GUI.Label(new Rect(36,height-79,width-350,30),Tutorial.Active?(Tutorial.Success?Tutorial.Name+" · 練習成功":Tutorial.Name+" · "+Tutorial.StepTitle(Tutorial.GuideStep(Time.time))):LastEvent,big);GUI.Label(new Rect(36,height-43,width-350,26),Tutorial.Active?Tutorial.Feedback:Source.Message,small);
   GUI.Label(new Rect(width-285,height-78,250,27),Tutorial.Active?"已學會 "+Tutorial.CompletedCount+" / "+TutorialSession.Names.Length+" 招":Gestures.State,label);GUI.color=new Color(.12f,.25f,.28f);GUI.DrawTexture(new Rect(width-285,height-42,240,5),Texture2D.whiteTexture);GUI.color=new Color(1,.68f,.24f);GUI.DrawTexture(new Rect(width-285,height-42,240*(Tutorial.Active?Tutorial.CompletedCount/(float)TutorialSession.Names.Length:Mathf.Max(Gestures.Power,Gestures.Progress)),5),Texture2D.whiteTexture);GUI.color=Color.white;
   if(Tutorial.Active){DrawTutorial(width,height);return;}
   if(!ShowControls){if(GUI.Button(new Rect(width-150,120,120,32),"顯示控制 Tab",button))ShowControls=true;return;}
   float px=width-302;GUI.color=new Color(.015f,.03f,.045f,.94f);GUI.DrawTexture(new Rect(px,120,270,Mathf.Min(height-235,625)),Texture2D.whiteTexture);GUI.color=Color.white;
   GUI.Label(new Rect(px+18,135,230,26),"演出控制",big);
   if(GUI.Button(new Rect(px+18,174,112,34),"自動演示 D",button)){Source.UseDemo();ClearEffects();}
   if(GUI.Button(new Rect(px+140,174,112,34),Source.Starting?"開啟中…":"真人追蹤 V",button)){Source.StartCamera();ClearEffects();}
   if(GUI.Button(new Rect(px+18,218,234,34),"重新定位 C（上半身／全身）",button)){Source.Calibrate();Gestures.Reset();}
   if(GUI.Button(new Rect(px+18,260,112,30),"更換鏡頭",button)){Source.NextCamera();Gestures.Reset();}
   if(GUI.Button(new Rect(px+140,260,112,30),orbit?"固定視角":"環繞視角 O",button))orbit=!orbit;
   string[] names={"1  螺旋丸","2  水遁・水龍彈","3  拔劍斬","4  影分身","5  龍脈召喚","6  火遁"};string[] ids={"rasengan","water","slash","clone","dragon","fire"};for(int i=0;i<ids.Length;i++)if(GUI.Button(new Rect(px+18,305+i*35,234,29),names[i],button))OnSkill(ids[i],1);
   if(GUI.Button(new Rect(px+18,515,234,26),"清除特效 Space",button))ClearEffects();
   GUI.Label(new Rect(px+18,545,234,172),"施術動作\n托球繞圈 → 舉起 → 拋螺旋丸\n胸前結印 → 分開過肩寬 → 水遁\n左腰握柄 → 斜上抽出拔劍\n雙手回左腰 → 停半秒收劍\n前傾、雙臂後伸 → 分身\n頭頂合掌 → 張開召龍\n嘴前結印 → 轉頭指向火遁",small);
   if(!Source.Demo&&Source.Preview){Rect r=new Rect(35,125,280,157.5f);GUI.color=Color.white;DrawPreview(r);GUI.Label(new Rect(35,294,360,95),"綠點＝已辨識關節 · 黃點＝雙手\n"+Source.Framing+" · 相對影像大小 "+Source.RelativeSize.ToString("F2")+"×\n"+Source.InferenceMs.ToString("F0")+" ms／推論（非總延遲）\n靠近可測上半身，退後可測全身",small);}
   if(Source.Demo){GUI.Label(new Rect(36,128,500,25),"自動施術示範  /  "+Source.DemoClock.ToString("00.0")+" s",small);}
  }
  void OnDestroy(){if(Gestures!=null)Gestures.Fired-=OnGestureSkill;DisposeTutorial();}
 }
}
