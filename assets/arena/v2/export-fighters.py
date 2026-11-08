from PIL import Image
import os,json,shutil
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'../../..'))
OUT=os.path.join(ROOT,'web/wwwroot/assets/arena/v2')
SOURCE=os.path.join(ROOT,'assets/arena/v2/source')
SOURCES={name:'fighters-'+name+'-source.png' for name in ['starter','bip01','raymond','starter-long','starter-cap']}
ACTIONS={'idle':[0,1,2,3],'blink':[0,1,2,3],'walk':[4,5,6,7],'run':[4,5,6,7],'jump':[8,9],'land':[11],'hit':[14],'knockout':[14],'celebrate':[15],'respawn':[0,1,2,3],'interact':[11],'ability_1_pulse':[12,13],'ability_2_magnet':[12,13],'ability_3_shield':[0,1,2,3],'ability_4_dash':[10],'ability_5_repair':[12,13],'ultimate_charge':[12],'ultimate_release':[13]}
manifest={'version':2,'tool':'built-in image_gen','columns':4,'rows':4,'frameWidth':256,'frameHeight':256,'anchor':{'x':128,'y':230},'actions':ACTIONS,'fighters':{}}
for name,filename in SOURCES.items():
 source=os.path.join(SOURCE,filename)
 im=Image.open(source).convert('RGBA')
 alpha=im.getchannel('A')
 if alpha.getextrema()[0]!=0: raise RuntimeError(name+' not genuinely transparent')
 cells=[]; bounds=[]
 for row in range(4):
  for col in range(4):
   cell=im.crop((round(col*im.width/4),round(row*im.height/4),round((col+1)*im.width/4),round((row+1)*im.height/4)))
   bbox=cell.getchannel('A').point(lambda a:255 if a>=128 else 0).getbbox()
   if not bbox:raise RuntimeError(name+' empty cell')
   cells.append(cell);bounds.append(bbox)
 # One scale for every pose keeps crouches and jumps anatomically stable.
 height=max(b[3]-b[1] for b in bounds[:4]); scale=204/height
 maxWidth=max(b[2]-b[0] for b in bounds)
 scale=min(scale,232/maxWidth)
 sheet=Image.new('RGBA',(1024,1024));normalized=[]
 for index,(cell,bbox) in enumerate(zip(cells,bounds)):
  # Keep the generated cell horizontal centre; do not centre on the extended punch.
  resized=cell.resize((round(cell.width*scale),round(cell.height*scale)),Image.Resampling.LANCZOS)
  left=round(128-cell.width*scale/2);top=round(230-bbox[3]*scale)
  frame=Image.new('RGBA',(256,256));frame.alpha_composite(resized,(left,top))
  normalized.append(frame);sheet.alpha_composite(frame,((index%4)*256,(index//4)*256))
 outname='fighters-'+name
 # Raw source PNGs are already retained beside the packaged atlas.
 sheet.save(os.path.join(ROOT,'assets/arena/v2/source',outname+'.png'),optimize=True)
 sheet.save(os.path.join(OUT,outname+'.webp'),lossless=True,method=6)
 if name in ['starter','bip01','raymond']:
  # First idle head and upper torso, scaled directly from the generated artwork.
  portrait=normalized[0].crop((28,13,228,193)).resize((500,450),Image.Resampling.LANCZOS)
  portrait.save(os.path.join(OUT,'portrait-'+name+'.webp'),lossless=True,method=6)
 manifest['fighters'][name]={'atlas':'/assets/arena/v2/'+outname+'.webp','source':'assets/arena/v2/source/'+filename,'sourceWidth':im.width,'sourceHeight':im.height,'scale':scale,'frameWidth':256,'frameHeight':256,'anchor':{'x':128,'y':230},'alpha':list(sheet.getchannel('A').getextrema()),'fileBytes':os.path.getsize(os.path.join(OUT,outname+'.webp'))}
 print(name,sheet.size,'scale',round(scale,4),'alpha',sheet.getchannel('A').getextrema(),'bytes',manifest['fighters'][name]['fileBytes'])
with open(os.path.join(ROOT,'assets/arena/v2/fighter-art-manifest.json'),'w',encoding='utf-8') as f:json.dump(manifest,f,indent=2,ensure_ascii=False)
with open(os.path.join(OUT,'fighter-art-manifest.json'),'w',encoding='utf-8') as f:json.dump(manifest,f,indent=2,ensure_ascii=False)
