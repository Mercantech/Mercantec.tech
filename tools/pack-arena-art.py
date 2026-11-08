"""Export generated Arena art to production WebP; no game art is drawn here."""
import argparse, hashlib, json
from pathlib import Path
from PIL import Image

def pack(config_path):
    config = json.loads(Path(config_path).read_text(encoding='utf-8'))
    destination = Path(config['destination'])
    destination.mkdir(parents=True, exist_ok=True)
    reports=[]
    for item in config['assets']:
        source=Path(item['source'])
        with Image.open(source) as original:
            rgba=original.convert('RGBA')
            if not item.get('transparent', False):
                rgba=rgba.convert('RGB')
                rgba.thumbnail((1920,1080), Image.Resampling.LANCZOS)
            output=destination/(item['key']+'.webp')
            rgba.save(output, 'WEBP', quality=92, method=6, lossless=item.get('transparent',False))
            alpha=rgba.getchannel('A').getextrema() if rgba.mode=='RGBA' else None
            reports.append({'id':item['key'],'file':output.name,'width':rgba.width,'height':rgba.height,'bytes':output.stat().st_size,'transparent':bool(alpha and alpha[0]<255),'sha256':hashlib.sha256(output.read_bytes()).hexdigest(),'prompt':item['prompt']})
    report={'generator':'built-in image_gen','assets':reports}
    Path(config['manifest']).write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'assets':[{k:a[k] for k in ('id','file','width','height','bytes','transparent')} for a in reports]},ensure_ascii=False))

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('config');args=parser.parse_args();pack(args.config)
