"""Regression checks for rectangular VFX mattes and atlas-neighbour leakage."""
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw
import json,hashlib

ROOT=Path(__file__).resolve().parents[3]

def validate():
    manifest=json.loads((ROOT/'assets/arena/v3/effects.json').read_text(encoding='utf-8'));rows=[]
    assert len(manifest['assets'])==20,'The original per-avatar VFX set must remain complete'
    assert len({a['key'] for a in manifest['assets']})==20,'Duplicate effect IDs'
    contact=Image.new('RGB',(1200,1000),(8,22,41));draw=ImageDraw.Draw(contact)
    for index,entry in enumerate(manifest['assets']):
        output=ROOT/'web/wwwroot'/entry['url'].lstrip('/');source=ROOT/entry['source']
        assert output.is_file(),f'Missing effect {output}'
        image=Image.open(output).convert('RGBA');original=Image.open(source).convert('RGBA')
        assert image.size==(entry['columns']*entry['frameWidth'],entry['rows']*entry['frameHeight'])
        assert ImageChops.difference(image.getchannel('A'),original.getchannel('A')).getbbox() is None,entry['key']+' alpha differs from original'
        frames=[]
        for f in range(entry['frameCount']):
            x=f*entry['frameWidth'];tile=image.crop((x,0,x+entry['frameWidth'],entry['frameHeight']));w,h=tile.size;alpha=tile.getchannel('A');values=list(alpha.get_flattened_data())
            border=[values[y*w+x] for y in range(h) for x in range(w) if x<8 or y<8 or x>=w-8 or y>=h-8]
            assert max(border)==0,entry['key']+f' frame {f}: visible edge causes rectangle or filtering bleed'
            transparent=sum(v==0 for v in values)/len(values)
            assert transparent>=.5,entry['key']+f' frame {f}: suspicious rectangular matte'
            bbox=alpha.getbbox();assert bbox and bbox[0]>=8 and bbox[1]>=8 and bbox[2]<=w-8 and bbox[3]<=h-8,entry['key']+' has no clean guard'
            frames.append({'frame':f,'borderMaxAlpha':0,'cornersAlpha':[values[0],values[w-1],values[(h-1)*w],values[-1]],'transparentFraction':round(transparent,5),'visibleBounds':bbox})
        if entry['role']=='projectile':assert entry['columns']==4 and entry['frameCount']==4 and entry['fps']==12
        assert entry['sha256']==hashlib.sha256(output.read_bytes()).hexdigest(),'Production file changed after export'
        rows.append({'key':entry['key'],'frames':frames,'alphaPreserved':True})
        tile=image.crop((0,0,entry['frameWidth'],entry['frameHeight']));tile.thumbnail((220,215),Image.Resampling.LANCZOS);background=Image.new('RGBA',tile.size,(8,22,41,255));background.alpha_composite(tile)
        cx=(index%5)*240+(240-tile.width)//2;cy=(index//5)*250+25;contact.paste(background.convert('RGB'),(cx,cy));draw.text(((index%5)*240+6,(index//5)*250+6),entry['key'],fill=(235,242,255))
    report={'validatedFiles':20,'validatedFrames':sum(len(x['frames']) for x in rows),'guardPixels':8,'rectangularMatteFailures':0,'alphaPreserved':True,'assets':rows}
    (ROOT/'assets/arena/v3/effect-validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    output=ROOT/'output/arena-vfx-fix';output.mkdir(parents=True,exist_ok=True);contact.save(output/'original-effects-contact.png')
    print(json.dumps({key:report[key] for key in ['validatedFiles','validatedFrames','guardPixels','rectangularMatteFailures','alphaPreserved']}))

if __name__=='__main__':validate()
