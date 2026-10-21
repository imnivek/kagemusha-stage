using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace Kagemusha.Editor {
 public static partial class Project {
  sealed class GestureTest {
   public readonly GestureController G=new GestureController();public readonly Vector3[] P=new Vector3[17];public readonly float[] Q=Enumerable.Repeat(1f,17).ToArray();public float T;public bool Ready=true;public Vector3? Head;
   public void Feed(float poseTime,float seconds,bool animate=false){for(int i=0;i<Mathf.CeilToInt(seconds*60);i++){PoseSource.DemoPose(poseTime+(animate?i/60f:0),P);Step();}}
   public void Step(Vector3[] observed=null){G.Update(P,Q,Ready,T,1/60f,observed,Head);T+=1/60f;}
   public void Hold(float seconds,Vector3[] observed=null){for(int i=0;i<Mathf.CeilToInt(seconds*60);i++)Step(observed);}
  }
  static void Require(bool valid,string message){if(!valid)throw new Exception(message);}
  [MenuItem("Kagemusha/驗證六招經典姿勢")]
  public static string VerifyClassicGestures(){
   var f=new GestureTest();f.Feed(4,1.9f,true);Require(f.G.RasenganStage==2,"Moving circle did not form Rasengan");Require(f.G.Releases==0,"Formation released a projectile");f.Feed(6.6f,.45f);Require(f.G.RasenganStage==3,"Overhead preparation failed");f.Feed(7.3f,.45f,true);Require(f.G.Releases==1&&f.G.RasenganStage==0,"Prepared throw did not release once");
   f=new GestureTest();f.Feed(4.5f,2);Require(f.G.Power==0&&f.G.RasenganStage==1,"Stationary hands fabricated a rotating charge");f.Feed(6.6f,.5f);f.Feed(7.3f,.5f,true);Require(f.G.Releases==0,"Unformed throw released Rasengan");
   f=new GestureTest();f.Feed(4,1,true);f.Q[7]=0;f.Feed(5,.1f);Require(f.G.Power==0&&f.G.RasenganStage==0,"Missing helper hand retained formation");f.Q[7]=1;f.Feed(6.6f,.5f);f.Feed(7.3f,.5f,true);Require(f.G.Releases==0,"Reacquisition released stale Rasengan");
   f=new GestureTest();f.Feed(10.2f,.5f);Require(f.G.DrawReady&&!f.G.Sword,"Left-waist grip did not arm draw");f.Feed(11,.2f);Require(f.G.Sword,"Diagonal draw did not equip");f.Feed(11.3f,.9f,true);Require(f.G.Slashes>0,"Equipped sword cannot slash");f.Feed(18.4f,.25f);Require(f.G.SheathReady&&f.G.Sword,"Left-waist hold did not prepare sheath");f.Feed(18.4f,.5f);Require(!f.G.Sword,"Return to left waist did not sheath");
   f=new GestureTest();f.Feed(11,.4f);Require(!f.G.Sword,"Draw without grip equipped sword");PoseSource.DemoPose(0,f.P);f.P[7]=f.P[0]+new Vector3(.18f,.45f,-.2f);f.P[10]=f.P[0]+new Vector3(-.18f,.45f,-.2f);f.Hold(1);Require(!f.G.Sword,"Old crossed-hands pose still equips");
   f=new GestureTest();f.Feed(30,1.2f);Require(f.G.Power>=.3f&&f.G.Releases==0,"Water chest seal failed");f.Ready=false;f.Feed(30,.1f);Require(f.G.Power==0,"Tracking loss retained water charge");f.Ready=true;f.Feed(32.6f,.3f);Require(f.G.Releases==0,"Stale water charge released");f.Feed(30,1.2f);f.Feed(32.6f,.3f);Require(f.G.Releases==1,"Water seal and push failed");
   f=new GestureTest();f.Feed(15.6f,.45f);Require(f.G.Clones==0,"Clone triggered before hold threshold");f.Feed(15.6f,4);Require(f.G.Clones==1,"Held ninja run repeated clone");f.Feed(0,.25f);f.Feed(15.6f,.9f);Require(f.G.Clones==2,"Ninja run did not rearm after relaxing");
   foreach(int missing in new[]{3,6,7,9,10}){f=new GestureTest();f.Q[missing]=0;f.Feed(15.6f,1);Require(f.G.Clones==0,"Missing joint allowed clone: "+missing);}
   f=new GestureTest();PoseSource.DemoPose(0,f.P);f.P[7]=f.P[0]+new Vector3(-.30f,.8f,0);f.P[10]=f.P[0]+new Vector3(.30f,.8f,0);f.Hold(1);Require(f.G.Clones==0,"Old head-side pose still clones");
   f=new GestureTest();PoseSource.DemoPose(15.6f,f.P);for(int i=0;i<17;i++)f.P[i].x=-f.P[i].x;f.Hold(1);Require(f.G.Clones==1,"Mirrored ninja run failed");
   var observation=new Vector3[17];PoseSource.DemoPose(15.6f,observation);f=new GestureTest();PoseSource.DemoPose(0,f.P);f.Hold(1,observation);Require(f.G.Clones==1,"Observed gesture was replaced by retargeted rig");
   f=new GestureTest();f.Feed(21.8f,1);Require(f.G.SummonReady,"Dragon arming failed");f.Ready=false;f.Feed(21.8f,.1f);f.Ready=true;f.Feed(23.3f,.3f);Require(f.G.Summons==0,"Stale dragon summon survived tracking loss");f.Feed(21.8f,1);f.Feed(23.3f,.3f);Require(f.G.Summons==1,"Dragon summon failed");
   f=new GestureTest();f.Feed(35,1);Require(f.G.FlameActive,"Mouth seal did not start fire");f.Head=new Vector3(-.8f,.1f,1).normalized;f.Feed(35,.1f);Require(f.G.FlameDirection.x<-.25f,"Head direction does not steer fire left");f.Head=new Vector3(.8f,.1f,1).normalized;f.Feed(35,.1f);Require(f.G.FlameDirection.x>.25f,"Head direction does not steer fire right");f.Q[3]=0;f.Feed(35,.1f);Require(!f.G.FlameActive,"Invisible head kept fire active");
   VerifyNinjaObservation();
   string result="PASS: six jutsu; circular formation required; overhead-then-throw; missing-hand/stale-charge rejection; waist-grip draw and direct waist-hold sheath; old crossed/head-side gestures removed; water seal/push; ninja-run left/right, single latch and required joints; dragon cancel/rearm; mouth seal and head-steered fire; upper/full-body observation bridge and scale.";
   Directory.CreateDirectory("Evidence");File.WriteAllText("Evidence/gesture-verification.txt",result);return result;
  }
  static void VerifyNinjaObservation(){
   var go=new GameObject("Ninja observation test");var source=go.AddComponent<PoseSource>();source.Demo=false;var p=new Vector3[17];PoseSource.DemoPose(15.6f,p);
   try{foreach(bool full in new[]{false,true})foreach(float size in new[]{.75f,1f,1.3f}){
    source.StopCamera();source.Demo=false;var data=new float[128];data[119]=1;data[122]=1;
    for(int i=0;i<17;i++){int n=i*7;bool visible=full||(i>=2&&i<=10);data[n+3]=data[n+6]=visible?1:0;data[n+4]=.5f+p[i].x*.35f*size/(16/9f);data[n+5]=.5f+(p[i].y-1)*.35f*size;}
    source.AcceptObservation(data);Require(source.CanCast&&source.FullBodyVisible==full,"Observation framing failed");var g=new GestureController();for(int i=0;i<60;i++)g.Update(source.Joints,source.Confidence,source.CanCast,i/60f,1/60f,source.GestureJoints);Require(g.Clones==1,"Ninja run observation failed at "+full+"/"+size);
    data[124]=.6f;data[125]=.25f;data[126]=.9f;data[127]=1;source.AcceptObservation(data);Require(source.HeadAimVisible&&source.HeadDirection.x<0&&source.HeadDirection.y<0,"Head orientation decode/mirror failed");
   }}finally{UnityEngine.Object.DestroyImmediate(go);}
  }
 }
}
