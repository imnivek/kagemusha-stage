using System;
using UnityEngine;
namespace Kagemusha {
 public sealed class GestureController {
  public string Element="water",State="自由舞動",Candidate;
  public float Power,Progress;
  public bool Sword;
  public bool SummonReady=>summonArmed;
  public int RasenganStage {get;private set;}
  public bool DrawReady=>swordPhase==2;
  public bool SheathReady=>swordPhase==5;
  public bool WaterCharging=>charging;
  public bool FlameActive {get;private set;}
  public Vector3 AimDirection {get;private set;}=Vector3.forward;
  public Vector3 FlameDirection {get;private set;}=Vector3.forward;
  public float RightHandSpeed {get;private set;}
  public string CloneHint {get;private set;}
  public int Releases,Slashes,Summons,Clones;
  public event Action<string,float> Fired;
  string pending,latched;float since,cooldown,lastAttack,abilitySince,overheadSince=-1,circleAngle,circleSweep,swordSince,waterLastSeal;
  int swordPhase;bool summonArmed,charging,previous,circlePrevious,swordGripLatched;
  Vector2 previousHand,overheadHand;
  public void Reset(){Element="water";State="自由舞動";Power=Progress=RightHandSpeed=0;Sword=false;Candidate=pending=latched=null;CloneHint=null;summonArmed=charging=previous=swordGripLatched=false;cooldown=lastAttack=0;CancelRasengan();swordPhase=0;FlameActive=false;}
  void CancelRasengan(){RasenganStage=0;Power=0;circlePrevious=false;circleSweep=0;overheadSince=-1;}
  static Vector2 Centre(Vector3[] p)=>(p[5]+p[8])*.5f;
  static float Width(Vector3[] p)=>Mathf.Max(.12f,Vector2.Distance(p[5],p[8]));
  static bool WaterSeal(Vector3[] p){float w=Width(p);Vector2 c=Centre(p),m=(p[7]+p[10])*.5f;return Vector2.Distance(p[7],p[10])<w*.85f&&Mathf.Abs(m.x-c.x)<w*.45f&&m.y<c.y-w*.1f&&m.y>c.y-w*1.2f;}
  static bool MouthSeal(Vector3[] p){float w=Width(p);Vector2 m=(p[7]+p[10])*.5f;return Vector2.Distance(p[7],p[10])<w*.65f&&Vector2.Distance(m,p[3])<w*.80f&&m.y>Centre(p).y-w*.05f&&m.y< p[3].y+w*.18f;}
  static bool RasenganGrip(Vector3[] p){float w=Width(p),side=Mathf.Sign(p[8].x-p[5].x);Vector2 c=Centre(p),r=p[10];float gap=Vector2.Distance(p[7],r);return (r.x-c.x)*side>w*.30f&&r.y<c.y-w*.22f&&r.y>c.y-w*1.25f&&gap>w*.15f&&gap<w*1.15f;}
  // Recognize the visible silhouette, before fixed bone lengths/IK change arm angles.
  // A single camera cannot prove that hidden hands are behind the body.
  public static string NinjaRunHint(Vector3[] p,float[] quality=null){
   if(quality!=null){
    if(quality[3]<PoseSource.JointThreshold)return "請讓頭部留在畫面內，再做前傾動作。";
    if(quality[6]<PoseSource.JointThreshold||quality[9]<PoseSource.JointThreshold)return "雙臂略向外展，讓兩側手肘都能被鏡頭看見。";
    if(quality[7]<PoseSource.JointThreshold||quality[10]<PoseSource.JointThreshold)return "雙手不要藏在背後，略向外展並留在畫面內。";
   }
   Vector2 l=p[5],r=p[8],centre=(l+r)*.5f,head=(Vector2)p[3]-centre;
   float width=Mathf.Max(.12f,Vector2.Distance(l,r));
   bool sideLean=Mathf.Abs(head.x)>width*.27f&&Mathf.Abs(head.x)>Mathf.Max(0,head.y)*.65f;
   bool frontLean=head.y<width*.46f&&head.y>-width*.35f;
   if(!sideLean&&!frontLean)return "上身再向前傾一些；可稍微側身，讓鏡頭看清忍者跑姿勢。";
   Vector2 leftHand=p[7],rightHand=p[10];
   if(leftHand.y>l.y-width*.22f||rightHand.y>r.y-width*.22f)return "雙手放低到肩膀下方，雙臂向後斜伸。";
   float leftLength=Vector2.Distance(l,p[6])+Vector2.Distance(p[6],leftHand);
   float rightLength=Vector2.Distance(r,p[9])+Vector2.Distance(p[9],rightHand);
   if(Vector2.Distance(l,leftHand)<leftLength*.78f||Vector2.Distance(r,rightHand)<rightLength*.78f)return "手肘再伸直一些，雙臂向身後斜伸。";
   float direction=Mathf.Sign(head.x),leftSide=Mathf.Sign(l.x-r.x);
   bool trailing=sideLean&&(leftHand.x-l.x)*direction<-width*.25f&&(rightHand.x-r.x)*direction<-width*.25f;
   bool open=frontLean&&(leftHand.x-l.x)*leftSide>width*.16f&&(rightHand.x-r.x)*-leftSide>width*.16f;
   if(!trailing&&!open)return "雙臂往身後斜伸、略向外展；雙手保持可見，不要只向下垂。";
   return null;
  }
  public static string Pose(Vector3[] p,Vector3[] observed=null){
   var a=observed??p;float shoulder=Centre(a).y,w=Width(a),gap=Vector2.Distance(a[7],a[10]);
   if(a[7].y>shoulder+w*.8f&&a[10].y>shoulder+w*.8f&&gap<w*.6f)return "dragon";
   if(NinjaRunHint(a)==null)return "clone";
   if(MouthSeal(a))return "flame";
   if(WaterSeal(a))return "water";
   return null;
  }
  public void Update(Vector3[] p,float[] quality,bool ready,float t,float dt,Vector3[] observed=null,Vector3? headAim=null){
   var a=observed??p;
   if(!ready||quality[5]<PoseSource.JointThreshold||quality[8]<PoseSource.JointThreshold){Power=Progress=RightHandSpeed=0;Candidate=pending=latched=null;charging=summonArmed=previous=false;CancelRasengan();swordPhase=0;FlameActive=false;CloneHint="請讓雙肩、頭部與雙臂入鏡。";State="等待可靠追蹤";return;}
   bool left=quality[7]>=PoseSource.JointThreshold,right=quality[10]>=PoseSource.JointThreshold,both=left&&right,head=quality[3]>=PoseSource.JointThreshold;
   Vector2 hand=(Vector2)a[10]-Centre(a);RightHandSpeed=previous&&right?Vector2.Distance(hand,previousHand)/Mathf.Max(.01f,dt):0;previousHand=hand;previous=right;
   if(!both){charging=summonArmed=false;if(RasenganStage<2)CancelRasengan();swordPhase=0;}
   if(!right)CancelRasengan();
   CloneHint=NinjaRunHint(a,quality);string pose=both?Pose(p,a):null;
   if(pose=="clone"&&CloneHint!=null)pose=null;if(pose=="flame"&&!head)pose=null;
   Candidate=pose;
   if(pose!=pending){pending=pose;since=t;Progress=0;}if(pose==null){latched=null;Progress=0;}
   if(FlameActive){
    float w=Width(a);Vector2 mid=(a[7]+a[10])*.5f;
    if(!both||!head||Vector2.Distance(a[7],a[10])>w*.9f||Vector2.Distance(mid,a[3])>w*1.5f||t-abilitySince>3){FlameActive=false;cooldown=t+1.2f;latched="flame";State="火遁結束 · 放開雙手後可重做";}
    else {FlameDirection=FireAim(a,headAim);State="火遁噴射 · 轉頭並用雙手指向目標";}
    return;
   }
   // A prepared jutsu owns the movement between its seal and release. Natural
   // pushes pass through other pose silhouettes; they must not select a new skill.
   if(charging&&TickWater(a,both,pose,t,dt))return;
   if(summonArmed){State="龍脈已喚醒 · 張開雙臂釋放";if(Vector2.Distance(a[7],a[10])>Width(a)*2.5f){Manual("dragon",1,t);summonArmed=false;cooldown=t+5;}return;}
   if(RasenganStage>0){if(TickRasengan(a,both,right,pose,t,dt))return;}
   if(both&&swordPhase!=0&&TickSword(a,t))return;
   if(pose=="water"&&t>=cooldown){
    Candidate="water";Progress=Mathf.Clamp01((t-since)/.35f);
    if(t-since>=.35f){Element="water";charging=true;waterLastSeal=t;Power=dt/1.3f;Fired?.Invoke("select",0);State="水遁結印集氣 · 雙掌向兩側推開";}else State="雙手在胸前中央結印 · 準備水遁";
    return;
   }
   if(both&&TickSword(a,t))return;
   if(both&&t>=cooldown&&RasenganGrip(a)&&pose!="clone"&&pose!="flame"){
    charging=summonArmed=false;Element="rasengan";RasenganStage=1;abilitySince=t;circlePrevious=false;circleSweep=0;Power=0;Fired?.Invoke("select",0);TickRasengan(a,true,true,pose,t,dt);return;
   }
   if(pose!=null&&pose!="water"&&t>=cooldown){
    Progress=Mathf.Clamp01((t-since)/.6f);State="姿勢確認";
    if(Progress>=1&&latched!=pose){latched=pose;Progress=0;
     if(pose=="clone"){Manual("clone",1,t);cooldown=t+2;State="影分身已開啟 · 放鬆後可再次施展";}
     if(pose=="dragon"){summonArmed=true;State="龍脈已喚醒 · 張開雙臂釋放";}
     if(pose=="flame"){FlameActive=true;abilitySince=t;FlameDirection=FireAim(a,headAim);Manual("fire",1,t);State="火遁噴射 · 轉頭並用雙手指向目標";}
    }
   }else State=!both?"手臂跟隨中 · 雙手入鏡可結印":Sword?"持刀舞動":"自由舞動 · 指定姿勢施術";
   if(right&&Sword&&pose==null&&t-lastAttack>.48f&&RightHandSpeed>1.6f){lastAttack=t;Manual("slash",1,t);}
  }
  bool TickWater(Vector3[] a,bool both,string pose,float t,float dt){
   if(!both){Power=Progress=0;charging=false;State="手腕離開鏡頭 · 水遁取消";return true;}
   if(pose=="flame"||pose=="dragon"||pose=="clone"){Power=Progress=0;charging=false;return false;}
   Candidate="water";float w=Width(a),gap=Vector2.Distance(a[7],a[10]);Vector2 mid=(a[7]+a[10])*.5f;
   if(pose=="water"){waterLastSeal=t;Power=Mathf.Clamp01(Power+dt/1.3f);Progress=Mathf.Clamp01(Power/.3f);State=Power>=.3f?"水遁已就緒 · 雙掌向兩側推開超過肩寬":"水遁結印集氣";return true;}
   if(Power>=.3f&&gap>w*1.35f&&mid.y>Centre(a).y-w*1.25f){AimDirection=HandAim(a);Manual("water",Power,t);cooldown=t+.9f;Power=Progress=0;charging=false;State="水遁 · 水龍彈釋放";return true;}
   if(t-waterLastSeal>2.2f||mid.y<Centre(a).y-w*1.8f){Power=Progress=0;charging=false;State="推掌中斷 · 回胸前結印可重做水遁";return true;}
   Progress=Mathf.Clamp01((gap/w-.85f)/.5f);State="水遁已集氣 · 雙手分開到肩寬以上";return true;
  }
  bool TickRasengan(Vector3[] a,bool both,bool right,string pose,float t,float dt){
   if(!right||t-abilitySince>8||(RasenganStage<3&&(pose=="clone"||pose=="dragon"||pose=="flame"||pose=="water"))){CancelRasengan();return false;}
   Candidate="rasengan";float w=Width(a);Vector2 relative=(Vector2)a[10]-Centre(a);
   if(RasenganStage==1){
    if(!both||!RasenganGrip(a)){CancelRasengan();State="集氣中斷 · 右手托球，左手繞圈";return true;}
    Vector2 radial=a[7]-a[10];float angle=Mathf.Atan2(radial.y,radial.x)*Mathf.Rad2Deg;
    if(circlePrevious){float delta=Mathf.DeltaAngle(circleAngle,angle);if(Mathf.Abs(delta)<70)circleSweep+=delta;}
    circleAngle=angle;circlePrevious=true;Power=Mathf.Clamp01(Mathf.Abs(circleSweep)/300f);Progress=Power;State="螺旋丸 · 右手托球、左手持續繞圈";
    if(Power>=1){RasenganStage=2;abilitySince=t;Fired?.Invoke("formed",1);State="螺旋丸成形 · 把右手舉過頭";}return true;
   }
   Power=1;
   if(RasenganStage==2){
    State="螺旋丸已成形 · 右手舉過頭";Progress=Mathf.Clamp01((relative.y/w+.1f)/.9f);
    if(relative.y>w*.8f){if(overheadSince<0)overheadSince=t;if(t-overheadSince>=.18f){RasenganStage=3;overheadHand=relative;abilitySince=t;Fired?.Invoke("throw-ready",1);State="準備拋出 · 右臂向前下方快速揮出";}}else overheadSince=-1;
   }else{
    State="準備拋出 · 右臂向前下方快速揮出";Progress=Mathf.Clamp01((overheadHand.y-relative.y)/(w*.55f));
    if(overheadHand.y-relative.y>w*.4f&&RightHandSpeed>1.35f&&t-abilitySince>.08f){AimDirection=HandAim(a);Manual("rasengan",1,t);CancelRasengan();cooldown=t+1.2f;State="螺旋丸 · 拋出";}
   }return true;
  }
  bool TickSword(Vector3[] a,float t){
   float w=Width(a),side=Mathf.Sign(a[8].x-a[5].x);Vector2 c=Centre(a),l=a[7],r=a[10];
   bool waist=(l.x-c.x)*side<-w*.20f&&(l.x-c.x)*side>-w*1.35f&&l.y<c.y-w*.62f&&l.y>c.y-w*1.6f;
   bool grip=waist&&Vector2.Distance(l,r)<w*.70f;
   if(swordGripLatched){if(grip){State="已收劍 · 放開握柄後可再次拔劍";return true;}swordGripLatched=false;}
   if(swordPhase!=0&&(!waist||t-swordSince>3))swordPhase=0;
   if(swordPhase==0){if(grip){swordPhase=Sword?5:1;swordSince=t;}else return false;}
   Candidate=Sword?"sheath":"draw";
   if(swordPhase==1){if(!grip){swordPhase=0;return false;}Progress=Mathf.Clamp01((t-swordSince)/.3f);State="左手扶左腰 · 右手握柄";if(Progress>=1){swordPhase=2;swordSince=t;}return true;}
   if(swordPhase==2){Progress=1;State="握柄已確認 · 右手斜向右上抽出";if((r.x-l.x)*side>w*.9f&&r.y>l.y+w*.45f){Sword=true;swordPhase=0;lastAttack=t;Fired?.Invoke("equip",1);State="已拔劍 · 揮臂斬擊";}return true;}
   if(!grip){swordPhase=0;Progress=0;return false;}
   Progress=Mathf.Clamp01((t-swordSince)/.55f);State="雙手回到左腰 · 稍停完成收劍";
   if(Progress>=1){Sword=false;swordPhase=0;swordGripLatched=true;Progress=0;Fired?.Invoke("equip",0);State="已收劍 · 放開握柄可再拔劍";}
   return true;
  }
  static Vector3 HandAim(Vector3[] p){Vector2 offset=((Vector2)p[7]+(Vector2)p[10])*.5f-Centre(p);float w=Width(p);return new Vector3(Mathf.Clamp(offset.x/w*1.5f,-1.3f,1.3f),Mathf.Clamp(offset.y/w*.7f,-.35f,.8f),1).normalized;}
  static Vector3 FireAim(Vector3[] p,Vector3? head){float w=Width(p);Vector2 offset=((Vector2)p[7]+(Vector2)p[10])*.5f-(Vector2)p[3];var hands=new Vector3(Mathf.Clamp(offset.x/w*2,-1.5f,1.5f),Mathf.Clamp(offset.y/w*1.4f+.25f,-.8f,.8f),1).normalized;return head.HasValue?(head.Value.normalized*.65f+hands*.35f).normalized:hands;}
  public void Manual(string id,float power,float t){if(id=="rasengan"||id=="water"||id=="fire")Releases++;if(id=="slash")Slashes++;if(id=="dragon")Summons++;if(id=="clone")Clones++;Fired?.Invoke(id,power);}
 }
}
