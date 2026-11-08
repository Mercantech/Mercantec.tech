"""Losslessly export original avatar VFX. No alpha editing, thresholding or painted masks."""
from pathlib import Path
from PIL import Image, ImageChops
import hashlib, json, shutil

ROOT=Path(__file__).resolve().parents[3]
SOURCE_ARCHIVE=ROOT/'assets/arena/v3/source/bip01'
OUTPUT=ROOT/'web/wwwroot/assets/arena/v3/effects'
EFFECTS=['shield','magnet_field','dash_trail','repair_field','ultimate_charge','ultimate_release','impact','unlock']
PROJECTILES={'bip01':['pulse_cyan','pulse_amber'],'raymond':['pulse_blue','pulse_gold']}

def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def clean_frames(image,columns):
    assert image.size==(512*columns,512),f'Unexpected VFX size {image.size}'
    frames=[]
    for index in range(columns):
        tile=image.crop((index*512,0,(index+1)*512,512));a=tile.getchannel('A');values=list(a.get_flattened_data())
        border=[values[y*512+x] for y in range(512) for x in range(512) if x<8 or y<8 or x>=504 or y>=504]
        assert max(border)==0,f'VFX frame {index} has non-transparent guard border'
        zero=sum(v==0 for v in values)/len(values)
        assert zero>=.5,f'VFX frame {index} may contain a rectangular matte: transparent fraction {zero}'
        frames.append({'index':index,'bounds':[index*512,0,512,512],'alphaRange':a.getextrema(),'visibleBounds':a.getbbox(),'guardBorderMaxAlpha':max(border),'transparentFraction':round(zero,5)})
    return frames

def export():
    OUTPUT.mkdir(parents=True,exist_ok=True);assets=[]
    bip_external=ROOT.parent/'robot-avatar-v3'
    if bip_external.exists():
        for folder,names in [('effects',EFFECTS),('projectiles',PROJECTILES['bip01'])]:
            (SOURCE_ARCHIVE/folder).mkdir(parents=True,exist_ok=True)
            for name in names:shutil.copy2(bip_external/folder/(name+'.png'),SOURCE_ARCHIVE/folder/(name+'.png'))
        shutil.copy2(bip_external/'external-assets.json',SOURCE_ARCHIVE/'external-assets.json')
    for avatar in ['bip01','raymond']:
        base=SOURCE_ARCHIVE if avatar=='bip01' else ROOT/'web/wwwroot/assets/avatars/raymond'
        external=json.loads((base/'external-assets.json').read_text(encoding='utf-8'))
        definitions={x['id']:x for kind in ['effects','projectiles'] for x in external[kind]}
        for folder,names,columns,role in [('effects',EFFECTS,1,'effect'),('projectiles',PROJECTILES[avatar],4,'projectile')]:
            for name in names:
                source=base/folder/(name+'.png');image=Image.open(source).convert('RGBA');frames=clean_frames(image,columns)
                output=OUTPUT/(avatar+'-'+name+'.webp');image.save(output,'WEBP',lossless=True,quality=100,method=6,exact=True)
                decoded=Image.open(output).convert('RGBA')
                assert ImageChops.difference(image.getchannel('A'),decoded.getchannel('A')).getbbox() is None,'Alpha changed during export'
                visible=image.getchannel('A').point(lambda v:255 if v else 0)
                rgbdiff=ImageChops.multiply(ImageChops.difference(image.convert('RGB'),decoded.convert('RGB')),Image.merge('RGB',(visible,visible,visible)))
                assert rgbdiff.getbbox() is None,'Visible source pixels changed during lossless export'
                definition=definitions[name]
                assets.append({'key':avatar+'-'+name,'avatarId':avatar,'id':name,'role':role,'url':'/assets/arena/v3/effects/'+output.name,'source':source.relative_to(ROOT).as_posix(),'originalSource':('design/robot-avatar-v3/' if avatar=='bip01' else 'web/wwwroot/assets/avatars/raymond/')+folder+'/'+name+'.png','width':image.width,'height':image.height,'columns':columns,'rows':1,'frameCount':columns,'frameWidth':512,'frameHeight':512,'fps':definition.get('fps',0),'pivot':definition.get('pivot',{'x':.5,'y':.5}),'naturalSize':definition.get('naturalSize',{'width':definition.get('width',512),'height':definition.get('height',512)}),'sourceSha256':sha(source),'sha256':sha(output),'bytes':output.stat().st_size,'frames':frames})
    manifest={'version':3,'origin':'Original BIP-01 and Raymond avatar packages','export':'lossless WebP, original dimensions and alpha preserved','guardPixels':8,'assets':assets}
    (ROOT/'assets/arena/v3/effects.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'files':len(assets),'frames':sum(a['frameCount'] for a in assets),'bytes':sum(a['bytes'] for a in assets),'alphaPreserved':True,'guardPixels':8}))

if __name__=='__main__':export()
