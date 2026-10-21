using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace Kagemusha.Editor {
 public static partial class Project {
  static void GradualWaterPush(GestureTest f,float seconds){
   var from=new Vector3[17];var to=new Vector3[17];PoseSource.DemoPose(30,from);PoseSource.DemoPose(32.6f,to);
   int count=Mathf.CeilToInt(seconds*60);for(int n=0;n<count;n++){float u=n/(float)(count-1);for(int i=0;i<17;i++)f.P[i]=Vector3.Lerp(from[i],to[i],u);f.Step();}
  }
  [MenuItem("Kagemusha/驗證慢速水遁與左腰收劍")]
  public static string VerifyWaterSheath(){
   string water,sheath;
   try{
    foreach(float duration in new[]{.6f,1.2f,1.8f}){
     var f=new GestureTest();f.Feed(30,1.2f);GradualWaterPush(f,duration);
     Require(f.G.Releases==1&&f.G.Element=="water","Gradual water push was cancelled or stolen at "+duration+"s; stage="+f.G.RasenganStage+" state="+f.G.State);
     f.Feed(32.6f,2);Require(f.G.Releases==1,"Held water release repeated");
    }
    var missing=new GestureTest();missing.Feed(30,1.2f);missing.Q[7]=0;missing.Hold(.1f);missing.Q[7]=1;GradualWaterPush(missing,1.2f);Require(missing.G.Releases==0,"Missing hand restored stale water charge");
    water="PASS: continuous 0.6/1.2/1.8-second water pushes, one release, no Rasengan takeover, missing-hand cancellation.";
   }catch(Exception e){water="FAIL: "+e.Message;}
   try{
    var f=new GestureTest();f.Feed(10.2f,.5f);f.Feed(11,.2f);Require(f.G.Sword,"Draw precondition failed");f.Feed(11.3f,.9f,true);
    f.Feed(18.4f,.25f);Require(f.G.Sword,"Waist sheath completed before intentional hold");
    f.Feed(18.4f,.6f);Require(!f.G.Sword,"Repeating the left-waist grip did not sheath");
    f.Feed(18.4f,2);Require(!f.G.Sword&&!f.G.DrawReady,"Held sheath immediately rearmed draw");
    f.Feed(11,.3f);Require(!f.G.Sword,"Leaving waist after sheath unexpectedly drew again");
    f.Feed(10.2f,.5f);f.Feed(11,.2f);Require(f.G.Sword,"Fresh grip cannot draw again after sheath");
    sheath="PASS: draw/slash, direct left-waist hold to sheath, minimum hold, no immediate redraw, fresh grip rearm.";
   }catch(Exception e){sheath="FAIL: "+e.Message;}
   Directory.CreateDirectory("Evidence");string result=water+"\n"+sheath;File.WriteAllText("Evidence/water-sheath-verification.txt",result);
   if(water.StartsWith("FAIL")||sheath.StartsWith("FAIL"))throw new Exception(result);return result;
  }
 }
}
