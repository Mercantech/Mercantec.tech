from PIL import Image
import os,json
root=os.path.abspath(os.path.join(os.path.dirname(__file__),'../../..'))
report={}
for name in ['starter','bip01','raymond','starter-long','starter-cap']:
 im=Image.open(os.path.join(root,'web/wwwroot/assets/arena/v2/fighters-'+name+'.webp')).convert('RGBA')
 bounds=[]
 for i in range(16):
  a=im.crop(((i%4)*256,(i//4)*256,(i%4+1)*256,(i//4+1)*256)).getchannel('A')
  b=a.point(lambda x:255 if x>=128 else 0).getbbox()
  assert b and b[0]>0 and b[1]>0 and b[2]<256 and b[3]<256,(name,i,b)
  assert 228<=b[3]<=232,(name,i,b)
  bounds.append(list(b))
 assert im.size==(1024,1024) and im.getchannel('A').getextrema()==(0,255)
 report[name]={'size':list(im.size),'alpha':[0,255],'frames':16,'baseline':230,'allFramesUnclipped':True,'frameOpaqueBounds':bounds}
for name in ['starter','bip01','raymond']:
 im=Image.open(os.path.join(root,'web/wwwroot/assets/arena/v2/portrait-'+name+'.webp'))
 assert im.size==(500,450) and im.mode=='RGBA'
with open(os.path.join(root,'assets/arena/v2/fighter-validation.json'),'w') as f:json.dump(report,f,indent=2)
print('Validated5RGBA atlases,80unclippedframes,stablebaseline230and3transparentportraits')
