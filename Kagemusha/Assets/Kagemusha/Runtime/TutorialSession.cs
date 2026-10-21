using UnityEngine;
namespace Kagemusha {
 public sealed class TutorialSession {
  public static readonly string[] Names={"螺旋丸","水遁・水龍彈","拔劍與收劍","影分身","龍脈召喚","火遁"};
  public static readonly string[] ShortNames={"螺旋丸","水遁","劍術","分身","召龍","火遁"};
  static readonly string[][] Titles={
   new[]{"右手托球，左手繞圈成形","右手把螺旋丸舉過頭","右臂向前下方快速拋出"},
   new[]{"胸前中央結印，累積水之力","雙手分開超過肩寬，引出水龍"},
   new[]{"左腰握柄，斜向右上拔劍","揮動右臂，斬出刀光","雙手回左腰，停約半秒收劍"},
   new[]{"忍者跑姿勢，開啟分身"},
   new[]{"頭頂合掌，喚醒龍脈","張開雙臂，釋放召喚"},
   new[]{"雙手在嘴前結印，發動火遁","轉頭並指向，調整噴火方向"}
  };
  public bool Active {get;private set;} public bool Practicing {get;private set;} public bool Success {get;private set;}
  public int Lesson {get;private set;} public int Step {get;private set;} public int Attempts {get;private set;}
  public float Progress {get;private set;} public string Feedback {get;private set;}="先看金色骨架，再開始練習。";
  public readonly bool[] Completed=new bool[Names.Length];float started;Vector3 firstFlameAim;
  public int StepCount=>Titles[Lesson].Length;
  public int CompletedCount {get{int n=0;foreach(bool b in Completed)if(b)n++;return n;}}
  public string Name=>Names[Lesson];public string StepTitle(int i)=>Titles[Lesson][Mathf.Clamp(i,0,StepCount-1)];
  public void Enter(float t){Active=true;Select(0,t);}public void Exit(){Active=Practicing=false;}
  public void Select(int lesson,float t){Lesson=Mathf.Clamp(lesson,0,Names.Length-1);Active=true;Practicing=Success=false;Step=0;Progress=0;started=t;Feedback="先看金色骨架示範，再按「開始練習」。";}
  public void Practice(float t){Practicing=true;Success=false;Step=0;Progress=0;started=t;Attempts++;Feedback="換你試試看，先完成第 1 步。";}
  public int GuideStep(float t)=>Practicing?Mathf.Min(Step,StepCount-1):Mathf.Min(StepCount-1,(int)(((t-started)%(StepCount*2.6f))/2.6f));
  void Pass(){Success=true;Progress=1;Completed[Lesson]=true;Feedback=Name+"成功！可以重練，或繼續下一招。";}
  public void Observe(string id,float power,GestureController g,bool live,float t){
   if(!Active||!Practicing||Success||!live)return;
   if(Lesson==0){if(id=="formed")Step=1;if(id=="throw-ready"&&Step==1)Step=2;if(id=="rasengan"&&Step==2)Pass();}
   if(Lesson==1&&id=="water"&&power>=.3f&&Step>=1)Pass();
   if(Lesson==2){if(id=="equip"&&power>0&&Step==0)Step=1;if(id=="slash"&&Step==1)Step=2;if(id=="equip"&&power<=0&&Step>=2)Pass();}
   if(Lesson==3&&id=="clone")Pass();if(Lesson==4&&id=="dragon"&&Step==1)Pass();
   if(Lesson==5&&id=="fire"){Step=1;firstFlameAim=g.FlameDirection;}
  }
  public void Tick(Vector3[] p,float[] q,GestureController g,bool live,bool ready,float t){
   if(!Active)return;if(!Practicing){Feedback="示範會循環播放；準備好後按「開始練習」。";return;}if(Success)return;Progress=0;
   if(!live){Feedback="請啟動真人鏡頭；自動演示不計入練習成績。";return;}
   if(!ready||q[5]<PoseSource.JointThreshold||q[8]<PoseSource.JointThreshold){if(Lesson==0||Lesson==1||Lesson==4||Lesson==5)Step=0;Feedback="請讓雙肩入鏡；忍者跑可稍微側身，不要完全背對鏡頭。";return;}
   bool right=q[10]>=PoseSource.JointThreshold,left=q[7]>=PoseSource.JointThreshold;
   if(!right||(!left&&!(Lesson==0&&g.RasenganStage>=2))){if(Lesson==0&&g.RasenganStage<2||Lesson==1||Lesson==4||Lesson==5)Step=0;Feedback=Lesson==3?"雙手不要藏在背後，略向外展並留在畫面內。":"請把手移回畫面，讓鏡頭看見手腕。";return;}
   if((Lesson==3||Lesson==5)&&q[3]<PoseSource.JointThreshold){Feedback="請讓頭部與雙手一起入鏡。";return;}
   if(Lesson==0){
    if(g.RasenganStage<2&&Step>0)Step=0;
    if(Step==0){Progress=g.RasenganStage==1?g.Power:0;Feedback=g.RasenganStage==1?"右手穩穩托球，左手繞著右手畫圈，直到螺旋丸成形。":"右手放在右側胸腹前托球，左手靠近並繞圈；不用按鍵選招。";}
    if(Step==1){Progress=g.Progress;Feedback="螺旋丸成形！左手可放下，把右手舉到頭頂上方。";}
    if(Step==2){Progress=g.Progress;Feedback="已準備好，右臂從頭頂向前下方快速揮出，拋出螺旋丸。";}
   }
   if(Lesson==1){
    if(g.Element=="water"&&g.Power>=.3f)Step=1;if(g.Power<=0)Step=0;
    Progress=Step==0?Mathf.Clamp01(g.Power/.3f):g.Power;
    Feedback=Step==0?"雙手在胸前中央靠近結印，停約一秒集氣；雙手都要入鏡。":"水之力已就緒！像推開兩扇門，把雙手分開到肩寬以上。";
   }
   if(Lesson==2){
    if(!g.Sword&&Step>0){Step=0;Feedback="請先完成左腰握柄，再斜向右上拔劍。";return;}
    if(Step==0){Progress=g.Progress;Feedback=g.DrawReady?"握柄已確認，左手扶腰不動，右手斜向右上抽出。":"左手扶左腰，右手跨過身體靠近左手握柄，稍停一下。";}
    if(Step==1){Progress=Mathf.Clamp01(g.RightHandSpeed/1.6f);Feedback="已拔劍！用右臂向外揮一次，斬出刀光。";}
    if(Step==2){Progress=g.SheathReady?g.Progress:0;Feedback="雙手回到拔劍前的左腰握柄位置，靠近並保持約半秒，就會收劍。";}
   }
   if(Lesson==3){Progress=g.Candidate=="clone"?g.Progress:0;Feedback=g.Candidate=="clone"?"忍者跑姿勢正確，保持約 0.6 秒；不用真的跑。":g.CloneHint??"上身前傾，雙臂後伸並略向外展。";}
   if(Lesson==4){if(g.SummonReady)Step=1;else if(Step==1)Step=0;Progress=Step==0?g.Progress:Mathf.Clamp01(Vector3.Distance(p[7],p[10])/1.1f);Feedback=Step==0?"雙手在頭頂合掌，保持約 0.6 秒喚醒龍脈。":"龍脈已喚醒，張開雙臂，讓龍從術式中現身。";}
   if(Lesson==5){
    if(Step==1&&!g.FlameActive)Step=0;
    if(Step==0){Progress=g.Candidate=="flame"?g.Progress:0;Feedback="雙手在嘴前靠近結印，保持約 0.6 秒；頭部與手腕都要可見。";}
    else {Progress=Mathf.Clamp01(Vector3.Angle(firstFlameAim,g.FlameDirection)/12f);Feedback="火遁噴射中！轉頭，雙手一起指向左、右或斜上方。";if(Progress>=1)Pass();}
   }
  }
  public void GuidePose(float t,Vector3[] p){
   int s=GuideStep(t);float clock=t-started,poseTime;
   if(Lesson==0)poseTime=s==0?4+clock%2.1f:s==1?6.6f:7.3f+clock%1.0f;
   else if(Lesson==1)poseTime=s==0?30:32.6f;
   else if(Lesson==2)poseTime=s==0?10+clock%1.2f:s==1?11.3f+clock%1.3f:17+clock%2.0f;
   else poseTime=Lesson==3?15.6f:Lesson==4?(s==0?21.8f:23.3f):35;
   PoseSource.DemoPose(poseTime,p);
   if((Lesson==1&&s==1)||(Lesson==4&&s==1)){
    float spread=Mathf.SmoothStep(0,1,Mathf.PingPong(clock*.7f,1)),x=Mathf.Lerp(.06f,.8f,spread),y=Lesson==4?.96f:.38f;
    p[7]=p[0]+new Vector3(-x,y,0);p[10]=p[0]+new Vector3(x,y,0);p[6]=Vector3.Lerp(p[5],p[7],.55f)+Vector3.down*.1f;p[9]=Vector3.Lerp(p[8],p[10],.55f)+Vector3.down*.1f;
   }
   if(Lesson==5&&s==1){float turn=Mathf.Sin(clock*1.5f)*.17f;p[3].x+=turn; p[4].x+=turn;p[7].x+=turn;p[10].x+=turn;}
   Vector3 offset=new Vector3(-p[0].x,0,-p[0].z);for(int i=0;i<17;i++)p[i]+=offset;
  }
 }
}
