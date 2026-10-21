#import <Foundation/Foundation.h>
#import <Vision/Vision.h>
#import <CoreVideo/CoreVideo.h>
#include <vector>
#include <mutex>
#include <atomic>
#include <memory>
#include <cstring>
#include <cmath>
#include <algorithm>

// ABI: 17 joints, seven floats each: root-relative metres xyz, 2D confidence,
// image uv (bottom-left), valid. Metadata: total people, inference ms, sequence, primary-selection status, reserved.
static std::mutex gate;
static std::atomic<bool> busy(false);
static std::atomic<unsigned long long> epoch(0);
static float result[128] = {};
static bool focusValid=false;static float focusX=.5f,focusY=.5f,focusSpan=0;
static unsigned long long sequence=0;
static std::mutex depthGate;
static std::atomic<bool> depthBusy(false);
static float cachedDepth[17]={};
static bool depthReady=false;
extern "C" __attribute__((visibility("default"))) void KP_Reset(){++epoch;std::lock_guard<std::mutex> lock(gate);memset(result,0,sizeof(result));sequence=0;focusValid=false;{std::lock_guard<std::mutex> d(depthGate);depthReady=false;memset(cachedDepth,0,sizeof(cachedDepth));}}
extern "C" __attribute__((visibility("default"))) unsigned long long KP_Copy(float* dest,int capacity){if(!dest||capacity<122)return 0;std::lock_guard<std::mutex> lock(gate);memcpy(dest,result,sizeof(float)*std::min(capacity,128));return sequence;}
extern "C" __attribute__((visibility("default"))) void KP_ResetFocus(){++epoch;std::lock_guard<std::mutex> lock(gate);focusValid=false;std::lock_guard<std::mutex> d(depthGate);depthReady=false;memset(cachedDepth,0,sizeof(cachedDepth));}
// Lock the central foreground performer, then associate by shoulder position and scale.
// No face recognition or identity data. An ambiguous match never changes performer.
static NSInteger ChooseSubject(NSArray<VNHumanBodyPoseObservation*>* observations,float aspect,float &status){
 struct Candidate{NSInteger index;float x,y,span,score;};std::vector<Candidate> candidates;
 std::lock_guard<std::mutex> lock(gate);
 for(NSInteger i=0;i<(NSInteger)observations.count;i++){
  auto l=[observations[i] recognizedPointForJointName:VNHumanBodyPoseObservationJointNameLeftShoulder error:nil];
  auto r=[observations[i] recognizedPointForJointName:VNHumanBodyPoseObservationJointNameRightShoulder error:nil];
  if(l.confidence<.25f||r.confidence<.25f)continue;
  float x=(l.x+r.x)*.5f,y=(l.y+r.y)*.5f,span=hypotf((l.x-r.x)*aspect,l.y-r.y);if(span<.025f)continue;
  float distance=hypotf(x-focusX,y-focusY),ratio=focusSpan>0?span/focusSpan:1;
  if(focusValid&&(distance>.18f+focusSpan*.35f||ratio<.55f||ratio>1.8f))continue;
  float score=focusValid?distance/fmax(.06f,focusSpan)+.6f*fabsf(logf(ratio)):-span*(1.3f-fabsf(x-.5f));
  candidates.push_back({i,x,y,span,score});
 }
 status=0;if(candidates.empty())return -1;
 std::sort(candidates.begin(),candidates.end(),[](const Candidate&a,const Candidate&b){return a.score<b.score;});
 if(candidates.size()>1){bool ambiguous=focusValid?(candidates[1].score-candidates[0].score<.25f):(-candidates[0].score<(-candidates[1].score)*1.35f);if(ambiguous){status=-1;return -1;}}
 auto c=candidates[0];focusX=c.x;focusY=c.y;focusSpan=c.span;focusValid=true;status=1;return c.index;
}
extern "C" __attribute__((visibility("default"))) int KP_Submit(const unsigned char* rgba,int width,int height){
 if(!rgba||width<32||height<32||width>1920||height>1080)return -1;
 bool expected=false;if(!busy.compare_exchange_strong(expected,true))return 0;
 auto bytes=std::make_shared<std::vector<unsigned char>>(rgba,rgba+width*height*4);auto generation=epoch.load();
 dispatch_async(dispatch_get_global_queue(QOS_CLASS_USER_INITIATED,0), ^{
 @autoreleasepool{
  double start=CFAbsoluteTimeGetCurrent();float output[128]={};CVPixelBufferRef buffer=nullptr;
  CVReturn code=CVPixelBufferCreate(kCFAllocatorDefault,width,height,kCVPixelFormatType_32BGRA,(__bridge CFDictionaryRef)@{(id)kCVPixelBufferIOSurfacePropertiesKey:@{}},&buffer);
  if(code==kCVReturnSuccess){
   CVPixelBufferLockBaseAddress(buffer,0);auto base=(unsigned char*)CVPixelBufferGetBaseAddress(buffer);size_t stride=CVPixelBufferGetBytesPerRow(buffer);
   for(int y=0;y<height;y++)for(int x=0;x<width;x++){auto s=bytes->data()+((height-1-y)*width+x)*4;auto d=base+y*stride+x*4;d[0]=s[2];d[1]=s[1];d[2]=s[0];d[3]=255;}
   CVPixelBufferUnlockBaseAddress(buffer,0);
   VNDetectHumanBodyPoseRequest* flat=[VNDetectHumanBodyPoseRequest new];
   VNDetectFaceRectanglesRequest* faces=[VNDetectFaceRectanglesRequest new];
   VNImageRequestHandler* handler=[[VNImageRequestHandler alloc] initWithCVPixelBuffer:buffer options:@{}];NSError* error=nil;
   if([handler performRequests:@[flat,faces] error:&error]){
    output[119]=(float)flat.results.count;
    float selectedStatus=0;NSInteger chosen=ChooseSubject(flat.results,(float)width/height,selectedStatus);output[122]=selectedStatus;
    if(chosen>=0){
     VNHumanBodyPoseObservation* projected=flat.results[chosen];
     NSArray* flatNames=@[VNHumanBodyPoseObservationJointNameRoot,VNHumanBodyPoseObservationJointNameRoot,VNHumanBodyPoseObservationJointNameNeck,VNHumanBodyPoseObservationJointNameNose,VNHumanBodyPoseObservationJointNameNose,VNHumanBodyPoseObservationJointNameLeftShoulder,VNHumanBodyPoseObservationJointNameLeftElbow,VNHumanBodyPoseObservationJointNameLeftWrist,VNHumanBodyPoseObservationJointNameRightShoulder,VNHumanBodyPoseObservationJointNameRightElbow,VNHumanBodyPoseObservationJointNameRightWrist,VNHumanBodyPoseObservationJointNameLeftHip,VNHumanBodyPoseObservationJointNameLeftKnee,VNHumanBodyPoseObservationJointNameLeftAnkle,VNHumanBodyPoseObservationJointNameRightHip,VNHumanBodyPoseObservationJointNameRightKnee,VNHumanBodyPoseObservationJointNameRightAnkle];
     VNRecognizedPoint* hip=[projected recognizedPointForJointName:VNHumanBodyPoseObservationJointNameRoot error:nil];
     VNRecognizedPoint* neck=[projected recognizedPointForJointName:VNHumanBodyPoseObservationJointNameNeck error:nil];
     VNRecognizedPoint* nose=[projected recognizedPointForJointName:VNHumanBodyPoseObservationJointNameNose error:nil];
     // Associate head orientation with the selected performer's nose, never a
     // background face. Only angles are returned; no face image is retained.
     VNFaceObservation* selectedFace=nil;float faceDistance=10;
     if(nose.confidence>.25f)for(VNFaceObservation* face in faces.results){
      CGRect box=CGRectInset(face.boundingBox,-.025,-.025);
      float d=hypotf(CGRectGetMidX(face.boundingBox)-nose.x,CGRectGetMidY(face.boundingBox)-nose.y);
      if(CGRectContainsPoint(box,CGPointMake(nose.x,nose.y))&&d<faceDistance){selectedFace=face;faceDistance=d;}
     }
     if(selectedFace&&selectedFace.yaw&&selectedFace.pitch){output[124]=selectedFace.yaw.floatValue;output[125]=selectedFace.pitch.floatValue;output[126]=selectedFace.confidence;output[127]=1;}
     VNRecognizedPoint* la=[projected recognizedPointForJointName:VNHumanBodyPoseObservationJointNameLeftAnkle error:nil];
     VNRecognizedPoint* ra=[projected recognizedPointForJointName:VNHumanBodyPoseObservationJointNameRightAnkle error:nil];
     // Fast image-plane motion owns XY; lower-frequency monocular 3D only refines Z.
     // A fixed reference height is an estimate, never a measured camera distance.
     float top=nose.y+fmax(.025f,(nose.y-neck.y)*.7f),bottom=fmin(la.y,ra.y);
     float scale=1.8f/fmax(.2f,top-bottom),aspect=(float)width/height;
     float depth[17]={};{std::lock_guard<std::mutex> d(depthGate);if(depthReady)memcpy(depth,cachedDepth,sizeof(depth));}
     for(int i=0;i<17;i++){
      VNRecognizedPoint* q=[projected recognizedPointForJointName:flatNames[i] error:nil];
      float u=q.x,v=q.y;if(i==1){u=(hip.x+neck.x)*.5f;v=(hip.y+neck.y)*.5f;}if(i==4)v=top;
      output[i*7]=(u-hip.x)*scale*aspect;output[i*7+1]=(v-hip.y)*scale;output[i*7+2]=depth[i];output[i*7+3]=q.confidence;output[i*7+4]=u;output[i*7+5]=v;output[i*7+6]=q.confidence>.05f?1:0;
     }
     bool depthExpected=false;
     if(flat.results.count==1&&depthBusy.compare_exchange_strong(depthExpected,true)){
      CVPixelBufferRetain(buffer);CVPixelBufferRef spatialBuffer=buffer;
      dispatch_async(dispatch_get_global_queue(QOS_CLASS_UTILITY,0), ^{
       @autoreleasepool{
        VNDetectHumanBodyPose3DRequest* spatial=[[VNDetectHumanBodyPose3DRequest alloc] initWithCompletionHandler:nil];
        VNImageRequestHandler* h3=[[VNImageRequestHandler alloc] initWithCVPixelBuffer:spatialBuffer options:@{}];
        if([h3 performRequests:@[spatial] error:nil]&&spatial.results.count==1&&generation==epoch.load()){
         VNHumanBodyPose3DObservation* body=spatial.results.firstObject;
         NSArray* names=@[VNHumanBodyPose3DObservationJointNameRoot,VNHumanBodyPose3DObservationJointNameSpine,VNHumanBodyPose3DObservationJointNameCenterShoulder,VNHumanBodyPose3DObservationJointNameCenterHead,VNHumanBodyPose3DObservationJointNameTopHead,VNHumanBodyPose3DObservationJointNameLeftShoulder,VNHumanBodyPose3DObservationJointNameLeftElbow,VNHumanBodyPose3DObservationJointNameLeftWrist,VNHumanBodyPose3DObservationJointNameRightShoulder,VNHumanBodyPose3DObservationJointNameRightElbow,VNHumanBodyPose3DObservationJointNameRightWrist,VNHumanBodyPose3DObservationJointNameLeftHip,VNHumanBodyPose3DObservationJointNameLeftKnee,VNHumanBodyPose3DObservationJointNameLeftAnkle,VNHumanBodyPose3DObservationJointNameRightHip,VNHumanBodyPose3DObservationJointNameRightKnee,VNHumanBodyPose3DObservationJointNameRightAnkle];
         std::lock_guard<std::mutex> d(depthGate);
         if(generation==epoch.load()){for(int j=0;j<17;j++){auto q=[body recognizedPointForJointName:names[j] error:nil];cachedDepth[j]=q?fmax(-.6f,fmin(.6f,q.position.columns[3].z)):0;}depthReady=true;}
        }
        CVPixelBufferRelease(spatialBuffer);depthBusy.store(false);
       }
      });
     }
    }
   }else output[119]=-1;
   CVPixelBufferRelease(buffer);
  }else output[119]=-1;
  output[120]=(float)((CFAbsoluteTimeGetCurrent()-start)*1000);
  {std::lock_guard<std::mutex> lock(gate);if(generation==epoch.load()){memcpy(result,output,sizeof(result));result[121]=(float)++sequence;}}
  busy.store(false);
 }
 });return 1;
}
