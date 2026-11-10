"""Copy the validated33clips and exact authoring scripts into the repository."""
from pathlib import Path
import argparse
import json
import shutil

parser=argparse.ArgumentParser()
parser.add_argument('--repo',type=Path,required=True)
args=parser.parse_args()
root=Path(__file__).resolve().parents[2]
audio=root/'audio'
manifest=json.loads((audio/'audio-manifest.json').read_text(encoding='utf8'))
assert manifest['avatarId']=='lupp' and len(manifest['sfx'])==28 and len(manifest['phrases'])==5
for report in ['audio-validation.json','browser-audio-validation.json']:
    assert json.loads((audio/report).read_text(encoding='utf8'))['passed']
runtime=args.repo.resolve()/'web/wwwroot/assets/avatars/lupp/audio'
scripts=args.repo.resolve()/'assets/avatars/lupp/scripts/audio'
runtime.mkdir(parents=True,exist_ok=True)
scripts.mkdir(parents=True,exist_ok=True)
for item in manifest['sfx']+manifest['phrases']:
    shutil.copy2(audio/item['file'],runtime/item['file'])
for name in ['audio-manifest.json','phrases.json','audio-validation.json','browser-audio-validation.json']:
    shutil.copy2(audio/name,runtime/name)
for name in ['generate_audio.py','render_danish_speech.ps1','verify_audio.py','verify_browser.cjs','publish_audio.py']:
    shutil.copy2(Path(__file__).resolve().parent/name,scripts/name)
shutil.copy2(audio/'README.md',args.repo.resolve()/'assets/avatars/lupp/AUDIO.md')
print(json.dumps(dict(runtimeClips=33,runtime=str(runtime),sourceScripts=str(scripts)),ensure_ascii=False))
