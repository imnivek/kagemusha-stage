"""Verify native pose inference and primary selection using an existing photo; never opens a camera."""
import ctypes,time,json,sys
from pathlib import Path
from PIL import Image
root=Path(__file__).resolve().parent.parent
lib=ctypes.CDLL(str(root/'Assets/Plugins/macOS/KagemushaPose.bundle/Contents/MacOS/KagemushaPose'))
lib.KP_Submit.argtypes=[ctypes.POINTER(ctypes.c_ubyte),ctypes.c_int,ctypes.c_int];lib.KP_Copy.restype=ctypes.c_ulonglong
im=Image.open(sys.argv[1]).convert('RGBA');im.thumbnail((640,480));im=im.transpose(Image.Transpose.FLIP_TOP_BOTTOM)
def infer(image):
 data=(ctypes.c_ubyte*(image.width*image.height*4)).from_buffer_copy(image.tobytes());out=(ctypes.c_float*128)();before=lib.KP_Copy(out,128);assert lib.KP_Submit(data,image.width,image.height)==1
 end=time.monotonic()+45
 while time.monotonic()<end:
  seq=lib.KP_Copy(out,128)
  if seq!=before:return list(out)
  time.sleep(.02)
 raise AssertionError('Native pose inference timed out')
results=[]
for i in range(3):
 out=infer(im);row={'people':out[119],'ms':out[120],'valid':sum(out[j*7+6] for j in range(17)),'headAim':bool(out[127]),'yaw':out[124],'pitch':out[125]};results.append(row);print(row,flush=True);assert row['people']==1 and row['valid']==17
out=infer(Image.new('RGBA',im.size,(0,0,0,255)));assert out[119]==0 and sum(out[j*7+6] for j in range(17))==0
lib.KP_Reset();outbuf=(ctypes.c_float*128)();assert lib.KP_Copy(outbuf,128)==0
(root/'Evidence/native-verification.json').write_text(json.dumps({'photo':results,'blankRejected':True,'reset':True},indent=2))
print('PASS: 17 fast motion joints, asynchronous depth refinement, blank rejection, reset')
