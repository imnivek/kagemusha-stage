using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Kagemusha {
 public sealed partial class KagemushaStage {
  public TutorialSession Tutorial {get;}=new TutorialSession();
  SkeletonView guide;Transform guideRoot;Camera guideCamera;RenderTexture guideTexture;Material guideMaterial;
  readonly Vector3[] guidePose=new Vector3[17];
  GUIStyle coachText,coachSmall,primaryButton;
  void Start(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"--tutorial")>=0)BeginTutorial();}
  public void BeginTutorial(){
   ClearEffects();ShowControls=true;orbit=false;Tutorial.Enter(Time.time);
   if(Source.Demo&&Array.IndexOf(Environment.GetCommandLineArgs(),"--demo")<0)Source.StartCamera();
   BuildGuide();
  }
  public void ExitTutorial(){Tutorial.Exit();ClearEffects();if(guideRoot)guideRoot.gameObject.SetActive(false);if(guideCamera)guideCamera.enabled=false;}
  public void ChooseTutorialLesson(int lesson){ClearEffects();Tutorial.Select(lesson,Time.time);}
  public void PracticeTutorial(){ClearEffects();Tutorial.Practice(Time.time);}
  void NextTutorialLesson(){for(int n=1;n<=TutorialSession.Names.Length;n++){int next=(Tutorial.Lesson+n)%TutorialSession.Names.Length;if(!Tutorial.Completed[next]){ChooseTutorialLesson(next);return;}}ExitTutorial();}
  void OnGestureSkill(string id,float power){
   Tutorial.Observe(id,power,Gestures,!Source.Demo&&Source.CanCast,Time.time);
   OnSkill(id,power);
  }
  void BuildGuide(){
   if(guideRoot)return;
   guideRoot=new GameObject("Tutorial demonstration only").transform;guideRoot.SetParent(transform);guideRoot.localPosition=new Vector3(100,0,0);
   guideMaterial=Mat(new Color(2,.95f,.20f));guide=new SkeletonView("Gold demonstration rig",guideRoot,guideMaterial,.032f);
   foreach(var item in guideRoot.GetComponentsInChildren<Transform>())item.gameObject.layer=30;
   stageCamera.cullingMask&=~(1<<30);
   guideTexture=new RenderTexture(420,420,16,RenderTextureFormat.ARGB32);guideTexture.name="Tutorial guide preview";guideTexture.Create();
   guideCamera=new GameObject("Tutorial preview camera",typeof(Camera)).GetComponent<Camera>();guideCamera.transform.SetParent(transform);guideCamera.transform.position=guideRoot.position+new Vector3(0,1.15f,-3.8f);guideCamera.transform.LookAt(guideRoot.position+new Vector3(0,1.15f,0));guideCamera.fieldOfView=35;guideCamera.nearClipPlane=.05f;guideCamera.farClipPlane=10;guideCamera.cullingMask=1<<30;guideCamera.targetTexture=guideTexture;guideCamera.clearFlags=CameraClearFlags.SolidColor;guideCamera.backgroundColor=new Color(.025f,.045f,.055f);guideCamera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
  }
  void UpdateTutorial(float t){
   if(guideRoot){guideRoot.gameObject.SetActive(Tutorial.Active);guideCamera.enabled=Tutorial.Active;}
   if(!Tutorial.Active)return;
   Tutorial.Tick(visible,Source.Confidence,Gestures,!Source.Demo,Source.CanCast,t);
   BuildGuide();Tutorial.GuidePose(t,guidePose);guide.Update(guidePose,Vector3.zero,.032f);
  }
  void TutorialKeys(){
   if(Input.GetKeyDown(KeyCode.T)){if(Tutorial.Active)ExitTutorial();else BeginTutorial();}
   if(!Tutorial.Active)return;
   if(Input.GetKeyDown(KeyCode.Escape)){ExitTutorial();return;}
   if(Input.GetKeyDown(KeyCode.LeftArrow))ChooseTutorialLesson((Tutorial.Lesson+TutorialSession.Names.Length-1)%TutorialSession.Names.Length);
   if(Input.GetKeyDown(KeyCode.RightArrow))ChooseTutorialLesson((Tutorial.Lesson+1)%TutorialSession.Names.Length);
   if(Input.GetKeyDown(KeyCode.R))PracticeTutorial();
   if(Input.GetKeyDown(KeyCode.Return)){if(Tutorial.Success)NextTutorialLesson();else if(!Tutorial.Practicing)PracticeTutorial();}
  }
  void InitTutorialStyles(){
   if(coachText!=null)return;
   coachText=new GUIStyle(label){fontSize=16,wordWrap=true};coachSmall=new GUIStyle(small){wordWrap=true};
   primaryButton=new GUIStyle(button){fontSize=16,fontStyle=FontStyle.Bold};primaryButton.normal.background=Swatch(new Color(.95f,.66f,.25f));primaryButton.hover.background=Swatch(new Color(1,.78f,.42f));primaryButton.active.background=Swatch(new Color(.8f,.49f,.15f));primaryButton.normal.textColor=primaryButton.hover.textColor=primaryButton.active.textColor=new Color(.045f,.065f,.08f);
  }
  void Panel(Rect r,Color color){GUI.color=color;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=Color.white;}
  void GuideHandLabel(Rect r,int joint,string text,float shift){
   Vector3 point=guideCamera.WorldToViewportPoint(guideRoot.TransformPoint(guidePose[joint]));
   GUI.Label(new Rect(r.x+point.x*r.width+shift,r.y+(1-point.y)*r.height-10,60,24),text,small);
  }
  void DrawTutorial(float width,float height){
   InitTutorialStyles();float px=width-354;int step=Tutorial.GuideStep(Time.time);
   Panel(new Rect(px,120,322,height-235),new Color(.015f,.03f,.045f,.97f));
   GUI.Label(new Rect(px+18,136,205,28),"教學測試",big);
   if(GUI.Button(new Rect(px+230,133,74,30),"離開 Esc",button)){ExitTutorial();return;}
   for(int i=0;i<TutorialSession.Names.Length;i++){
    GUI.color=Tutorial.Lesson==i?new Color(1,.74f,.35f):Color.white;
    if(GUI.Button(new Rect(px+18+i*47,180,45,34),TutorialSession.ShortNames[i],new GUIStyle(button){fontSize=12}))ChooseTutorialLesson(i);
   }GUI.color=Color.white;
   GUI.Label(new Rect(px+18,231,286,32),(Tutorial.Lesson+1)+" / "+TutorialSession.Names.Length+"　"+Tutorial.Name,big);
   GUI.Label(new Rect(px+18,270,286,26),Tutorial.Success?"成功出招！這一課已通過":Tutorial.Practicing?"換你練習 · 用真人動作完成":"先看示範 · 金色骨架會循環演示",coachSmall);
   for(int i=0;i<Tutorial.StepCount;i++){
    bool done=Tutorial.Success||(Tutorial.Practicing&&i<Tutorial.Step),current=i==step&&!Tutorial.Success;
    if(current)Panel(new Rect(px+13,305+i*(Tutorial.StepCount>3?35:43),296,Tutorial.StepCount>3?32:39),new Color(.09f,.18f,.20f));
    GUI.color=done?new Color(.35f,1,.7f):current?new Color(1,.76f,.35f):new Color(.6f,.7f,.73f);
    GUI.Label(new Rect(px+23,313+i*(Tutorial.StepCount>3?35:43),277,29),(done?"完成  ":(i+1)+"  ")+Tutorial.StepTitle(i),label);
   }GUI.color=Color.white;
   Panel(new Rect(px+18,453,286,117),new Color(.045f,.095f,.12f));
   GUI.Label(new Rect(px+30,464,262,70),Tutorial.Feedback,coachText);
   Panel(new Rect(px+30,548,262,7),new Color(.10f,.21f,.24f));
   Panel(new Rect(px+30,548,262*Tutorial.Progress,7),Tutorial.Success?new Color(.25f,.9f,.6f):new Color(1,.68f,.24f));
   string shoulders=Source.CanCast&&!Source.Demo?"雙肩 OK":"雙肩未辨識";
   string left=Source.CanCast&&Source.Confidence[7]>=PoseSource.JointThreshold&&!Source.Demo?"左手 OK":"左手未辨識",right=Source.CanCast&&Source.Confidence[10]>=PoseSource.JointThreshold&&!Source.Demo?"右手 OK":"右手未辨識";
   GUI.Label(new Rect(px+18,585,286,25),shoulders+"  /  "+left+"  /  "+right,coachSmall);
   GUI.Label(new Rect(px+18,614,286,23),"上半身與全身都可練 · 已學會 "+Tutorial.CompletedCount+" / "+TutorialSession.Names.Length+" 招",coachSmall);
   if(Tutorial.Success){
    if(GUI.Button(new Rect(px+18,height-254,286,50),Tutorial.CompletedCount==TutorialSession.Names.Length?"完成教學，回到演出":"下一招  →",primaryButton))NextTutorialLesson();
   }else if(!Tutorial.Practicing){
    if(GUI.Button(new Rect(px+18,height-254,286,50),"開始練習  ↵",primaryButton))PracticeTutorial();
   }else if(GUI.Button(new Rect(px+18,height-254,286,50),"從第 1 步重練  R",primaryButton))PracticeTutorial();
   if(GUI.Button(new Rect(px+18,height-194,138,34),Tutorial.Success?"再練一次 R":"重新看示範",button)){if(Tutorial.Success)PracticeTutorial();else ChooseTutorialLesson(Tutorial.Lesson);}
   if(GUI.Button(new Rect(px+166,height-194,138,34),"下一課 →",button))ChooseTutorialLesson((Tutorial.Lesson+1)%TutorialSession.Names.Length);
   if(GUI.Button(new Rect(px+18,height-151,138,30),"重新定位 C",button)){Source.Calibrate();if(Tutorial.Practicing)PracticeTutorial();else Gestures.Reset();}
   if(GUI.Button(new Rect(px+166,height-151,138,30),Source.Starting?"鏡頭啟動中":"重開真人鏡頭",button)){Source.StartCamera();if(Tutorial.Practicing)PracticeTutorial();}
   if(!Source.Demo&&Source.Preview){DrawPreview(new Rect(35,125,280,157.5f));GUI.Label(new Rect(35,292,285,54),"你的鏡頭 · 黃點是手腕\n"+Source.Framing+" · 雙手請留在畫面內",coachSmall);}
   else {Panel(new Rect(35,125,280,157.5f),new Color(.025f,.045f,.055f));GUI.Label(new Rect(50,148,250,115),Source.Demo?"請開啟真人鏡頭練習":Source.Message,coachText);}
   GUI.Label(new Rect(35,356,280,26),"動作示範  /  "+(step+1)+" · "+Tutorial.StepCount,big);
   Rect guideRect=new Rect(35,389,280,280);if(guideTexture){GUI.DrawTexture(guideRect,guideTexture,ScaleMode.ScaleToFit);GuideHandLabel(guideRect,7,"左手",-51);GuideHandLabel(guideRect,10,"右手",12);}
   GUI.Label(new Rect(35,682,280,70),"金色＝示範動作　青色＝你的骨架\n"+Tutorial.StepTitle(step)+"\n左右手方向與鏡像預覽一致",coachSmall);
  }
  void DisposeTutorial(){if(guideTexture){guideTexture.Release();Destroy(guideTexture);}if(guideMaterial)Destroy(guideMaterial);}
 }
}
