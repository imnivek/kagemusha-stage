using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kagemusha.Editor {
 public static partial class Project {
  [MenuItem("Kagemusha/建立骨架互動舞台")]
  public static string Setup(){
   if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop play mode first.");
   Directory.CreateDirectory("Assets/Kagemusha/Scenes");Directory.CreateDirectory("Assets/Kagemusha/Settings");
   var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
   var camera=new GameObject("Stage Camera",typeof(Camera),typeof(AudioListener));camera.tag="MainCamera";
   var root=new GameObject("影武者 · 3D Skeleton Stage");var stage=root.AddComponent<KagemushaStage>();stage.GlowShader=Shader.Find("Kagemusha/Glow");stage.SparkShader=Shader.Find("Kagemusha/Spark");stage.NinjaShader=Shader.Find("Kagemusha/NinjaToon");stage.StageFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/Kagemusha/Fonts/StageUnicode.otf");
   stage.FlameShader=Shader.Find("Kagemusha/NinjaFlame");if(!stage.GlowShader||!stage.SparkShader||!stage.NinjaShader||!stage.FlameShader)throw new Exception("Stage shaders missing.");
   var volume=new GameObject("Stage Bloom",typeof(Volume)).GetComponent<Volume>();volume.isGlobal=true;volume.priority=20;
   const string path="Assets/Kagemusha/Settings/Bloom.asset";var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
   if(!profile){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,path);}if(!profile.TryGet<Bloom>(out var bloom)){bloom=profile.Add<Bloom>(true);AssetDatabase.AddObjectToAsset(bloom,profile);}bloom.threshold.Override(1);bloom.intensity.Override(.65f);bloom.scatter.Override(.65f);volume.sharedProfile=profile;EditorUtility.SetDirty(profile);
   PlayerSettings.companyName="AZ Interactive";PlayerSettings.productName="Kagemusha Stage";PlayerSettings.bundleVersion="0.4.2";PlayerSettings.macOS.targetOSVersion="14.0";PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone,"studio.az.kagemusha");
   PlayerSettings.defaultScreenWidth=1440;PlayerSettings.defaultScreenHeight=900;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.runInBackground=true;
   PlayerSettings.macOS.cameraUsageDescription="影武者使用鏡頭在本機辨識人體骨架，讓你的動作啟動招式。影像不會上傳。";
   EditorSceneManager.SaveScene(scene,"Assets/Kagemusha/Scenes/Kagemusha.unity");EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Kagemusha/Scenes/Kagemusha.unity",true)};AssetDatabase.SaveAssets();Selection.activeGameObject=root;
   return "Kagemusha scene saved: 3D rig, six jutsu, sentinels, camera source.";
  }
  [MenuItem("Kagemusha/驗證施術流程")]
  public static string VerifyGestures(){
   return VerifyClassicGestures();
  }
  [MenuItem("Kagemusha/驗證自適應真人追蹤")]
  public static string VerifyAdaptiveTracking(){
   var go=new GameObject("Adaptive tracking verification");var source=go.AddComponent<PoseSource>();source.Demo=false;
   var positions=new Vector3[17];PoseSource.DemoPose(0,positions);
   Func<bool,float,float[]> frame=(full,size)=>{var data=new float[122];data[119]=1;for(int i=0;i<17;i++){int n=i*7;bool visible=full||(i>=2&&i<=10);data[n+3]=visible?1:0;data[n+4]=.5f+positions[i].x*.35f*size/(16/9f);data[n+5]=.5f+(positions[i].y-1)*.35f*size;data[n+6]=visible?1:0;}return data;};
   try{
    source.AcceptObservation(frame(false,1));if(!source.Tracking||!source.CanCast||source.FullBodyVisible)throw new Exception("Upper body incorrectly requires legs or manual calibration.");
    if(source.Joints[7].y<.5f)throw new Exception("Upper body did not map into the 3D rig.");
    source.AcceptObservation(frame(false,1.3f));if(source.RelativeSize<1.2f)throw new Exception("Near-camera size not reflected.");
    source.AcceptObservation(frame(false,.75f));if(source.RelativeSize>.8f)throw new Exception("Far-camera size not reflected.");
    source.AcceptObservation(frame(true,1));if(!source.FullBodyVisible||!source.CanCast)throw new Exception("Full body did not adapt automatically.");
    source.StopCamera();source.Demo=false;source.AcceptObservation(frame(true,1));if(Mathf.Abs(Mathf.Min(source.Joints[13].y,source.Joints[16].y)-.05f)>.02f)throw new Exception("Standing feet float above the stage.");
    var locked=frame(false,1);Array.Resize(ref locked,124);locked[119]=2;locked[122]=1;source.AcceptObservation(locked);if(!source.CanCast)throw new Exception("Background person interrupted the locked performer.");
    var multi=frame(false,1);multi[119]=2;source.AcceptObservation(multi);if(source.CanCast)throw new Exception("Ambiguous multiple people can cast.");
    source.AcceptObservation(new float[122]);if(source.Tracking||source.CanCast)throw new Exception("Blank frame remained active.");
    source.StopCamera();source.Demo=false;PoseSource.DemoPose(6.6f,positions);var wristOnly=frame(false,1);wristOnly[9*7+3]=wristOnly[9*7+6]=0;source.AcceptObservation(wristOnly);if(source.Joints[10].y<source.Joints[8].y+.15f)throw new Exception("Visible wrist cannot drive an inferred elbow.");
    var q=Enumerable.Repeat(1f,17).ToArray();var controller=new GestureController();int attacks=0;controller.Fired+=(id,power)=>{if(id=="rasengan"||id=="water"||id=="fire")attacks++;};
    PoseSource.DemoPose(30,positions);for(int i=0;i<90;i++)controller.Update(positions,q,true,i/60f,1/60f);if(controller.Power<.3f)throw new Exception("Water seal did not charge.");q[7]=0;controller.Update(positions,q,true,2,1/60f);if(controller.Power!=0||attacks!=0)throw new Exception("Missing hand retained or fired charge.");
    string result="PASS: upper body without feet/calibration, 3D mapping, near/far image scale, automatic full-body switch, standing foot placement, locked performer with background, ambiguous/blank rejection, visible-wrist IK, missing-hand charge cancel.";
    File.WriteAllText("Evidence/adaptive-tracking-verification.txt",result);return result;
   }finally{UnityEngine.Object.DestroyImmediate(go);}
  }
  [MenuItem("Kagemusha/匯出 Mac 展示程式")]
  public static string BuildMac(){
   if(EditorApplication.isPlaying)throw new Exception("Stop Play mode before building.");Directory.CreateDirectory("Builds");
   PlayerSettings.bundleVersion="0.4.2";PlayerSettings.macOS.targetOSVersion="14.0";
   const string scenePath="Assets/Kagemusha/Scenes/Kagemusha.unity";
   var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);
   if(!scene.IsValid()||!scene.isLoaded)scene=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Additive);
   var stage=scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<KagemushaStage>(true)).FirstOrDefault();
   if(!stage)throw new Exception("Kagemusha stage missing from build scene.");
   stage.NinjaShader=Shader.Find("Kagemusha/NinjaToon");stage.FlameShader=Shader.Find("Kagemusha/NinjaFlame");stage.StageFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/Kagemusha/Fonts/StageUnicode.otf");
   if(!stage.StageFont)throw new Exception("Shareable stage font missing.");
   if(!stage.NinjaShader||!stage.FlameShader)throw new Exception("Jutsu shaders missing.");
   EditorUtility.SetDirty(stage);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
   var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Kagemusha/Scenes/Kagemusha.unity"},locationPathName="Builds/Kagemusha Stage.app",target=BuildTarget.StandaloneOSX,options=BuildOptions.None});
   if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Build failed: "+result.summary.result);return "Built Builds/Kagemusha Stage.app";
  }
  [PostProcessBuild(50)]static void CameraUsage(BuildTarget target,string path){if(target!=BuildTarget.StandaloneOSX)return;
   string plist=Path.Combine(path,"Contents/Info.plist");var doc=XDocument.Load(plist);doc.DocumentType?.Remove();var dict=doc.Root.Element("dict");
   Set(dict,"NSCameraUsageDescription",new XElement("string","影武者使用鏡頭在本機辨識骨架，影像不會上傳。"));Set(dict,"NSCameraUseContinuityCameraDeviceType",new XElement("true"));Set(dict,"LSMinimumSystemVersion",new XElement("string","14.0"));doc.Save(plist);
   string notices=Path.Combine(path,"Contents/Resources/ThirdPartyNotices");Directory.CreateDirectory(notices);File.Copy("Assets/Kagemusha/Fonts/NotoSansTC-OFL.txt",Path.Combine(notices,"NotoSansTC-OFL.txt"),true);File.Copy("THIRD_PARTY_NOTICES.md",Path.Combine(notices,"THIRD_PARTY_NOTICES.md"),true);
   using(var process=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("/usr/bin/codesign"){Arguments="--force --deep --sign - \""+Path.GetFullPath(path).Replace("\"","\\\"")+"\"",UseShellExecute=false,RedirectStandardError=true})){string error=process.StandardError.ReadToEnd();process.WaitForExit();if(process.ExitCode!=0)throw new Exception("Local signing failed: "+error);}
  }
  static void Set(XElement dict,string key,XElement value){var item=dict.Elements("key").FirstOrDefault(e=>e.Value==key);if(item!=null)item.ElementsAfterSelf().FirstOrDefault()?.ReplaceWith(value);else dict.Add(new XElement("key",key),value);}
 }
}
