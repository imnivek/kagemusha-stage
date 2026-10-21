using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace Kagemusha.Editor {
 public static partial class Project {
  [MenuItem("Kagemusha/驗證六招教學")]
  public static string VerifyTutorial(){
   var f=new GestureTest();var tutorial=new TutorialSession();bool live=true;
   f.G.Fired+=(id,power)=>tutorial.Observe(id,power,f.G,live&&f.Ready,f.T);
   Action<float,float,bool> feed=(time,seconds,animate)=>{for(int i=0;i<Mathf.CeilToInt(seconds*60);i++){PoseSource.DemoPose(time+(animate?i/60f:0),f.P);f.Step();tutorial.Tick(f.P,f.Q,f.G,live,f.Ready,f.T);}};
   tutorial.Enter(f.T);feed(4,1.9f,true);Require(!tutorial.Practicing&&!tutorial.Success,"Watch counted as practice");
   for(int lesson=0;lesson<TutorialSession.Names.Length;lesson++){
    tutorial.Select(lesson,f.T);tutorial.Practice(f.T);f.G.Reset();f.Head=null;
    if(lesson==0){feed(4,1.9f,true);Require(tutorial.Step==1,"Formation did not advance");feed(6.6f,.45f,false);Require(tutorial.Step==2,"Overhead did not advance");feed(7.3f,.45f,true);}
    if(lesson==1){feed(30,1.2f,false);Require(tutorial.Step==1,"Water seal did not advance");var from=(Vector3[])f.P.Clone();var to=new Vector3[17];PoseSource.DemoPose(32.6f,to);for(int n=0;n<90;n++){for(int i=0;i<17;i++)f.P[i]=Vector3.Lerp(from[i],to[i],n/89f);f.Step();tutorial.Tick(f.P,f.Q,f.G,live,f.Ready,f.T);}}
    if(lesson==2){feed(10.2f,.5f,false);feed(11,.2f,false);Require(tutorial.Step==1,"Draw did not advance");feed(11.3f,.9f,true);Require(tutorial.Step==2,"Slash did not advance");feed(18.4f,.25f,false);Require(f.G.SheathReady&&!tutorial.Success,"Sheath needs an intentional hold");feed(18.4f,.5f,false);}
    if(lesson==3)feed(15.6f,1,false);
    if(lesson==4){feed(21.8f,1,false);Require(tutorial.Step==1,"Dragon arming did not advance");feed(23.3f,.3f,false);}
    if(lesson==5){feed(35,1,false);Require(tutorial.Step==1&&!tutorial.Success,"Fire aim lesson passed without steering");f.Head=new Vector3(.9f,0,1).normalized;feed(35,.2f,false);}
    Require(tutorial.Success&&tutorial.Completed[lesson],"Lesson failed: "+lesson);
   }
   Require(tutorial.CompletedCount==6,"Six lessons count");tutorial.Select(0,f.T);tutorial.Practice(f.T);f.G.Reset();live=false;feed(4,1.9f,true);feed(6.6f,.45f,false);feed(7.3f,.45f,true);Require(!tutorial.Success&&tutorial.Step==0,"Synthetic demo counted as live practice");
   live=true;tutorial.Select(4,f.T);tutorial.Practice(f.T);f.G.Reset();feed(21.8f,1,false);f.Ready=false;feed(21.8f,.1f,false);Require(tutorial.Step==0&&!f.G.SummonReady,"Tracking loss retained tutorial summon");f.Ready=true;feed(23.3f,.3f,false);Require(!tutorial.Success,"Unarmed spread passed lesson");
   tutorial.Select(3,f.T);var guide=new Vector3[17];tutorial.GuidePose(f.T,guide);Require(GestureController.NinjaRunHint(guide)==null,"Ninja tutorial guide is not a valid pose");tutorial.Exit();Require(!tutorial.Active,"Tutorial exit failed");
   string result="PASS: all six tutorials through actual gesture events, staged Rasengan, water seal/push, draw/slash/sheath, ninja run, dragon and directional fire; watch/demo rejection; head steering required; tracking-loss cancel; guide pose; lesson count and exit.";Directory.CreateDirectory("Evidence");File.WriteAllText("Evidence/tutorial-verification.txt",result);return result;
  }
 }
}
