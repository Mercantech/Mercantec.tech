"""Render the four LUP voices with one local Danish Røst model load."""

import argparse
from array import array
import hashlib
import json
import os
from pathlib import Path
import random
import re
import subprocess
import sys
from tempfile import TemporaryDirectory
import wave


MODEL = "CoRal-project/roest-v3-chatterbox-500m"
SAMPLE_RATE = 44100
PHRASES = (
    "phrase_1_welcome", "phrase_2_start", "phrase_3_success",
    "phrase_4_retry", "phrase_5_milestone",
)
REFERENCE_TEXTS = (
    "Her er en lille fortælling om et nyt spil, hvor alle kan være med.",
    "Vi finder en vej gennem skoven og åbner døren til det næste rum.",
)
GENERATION = {
    "language_id": "da", "temperature": 0.6, "cfg_weight": 0.5,
    "exaggeration": 0.5, "repetition_penalty": 2.0, "top_p": 0.95,
}
# First shape a synthetic prompt for each speaker. Keep stylized effects out
# of the prompt so the model conditions on clear speech.
PROFILES = {
    "bip-01": (
        "BIP-01 · præcis, metallisk robot",
        "asetrate=46722,aresample=44100,atempo=0.97,highpass=f=150,"
        "equalizer=f=2200:t=q:w=1.4:g=2",
        "highpass=f=240,lowpass=f=4750,equalizer=f=1850:t=q:w=1.4:g=3,"
        "tremolo=f=38:d=0.32,aecho=0.85:0.82:13:0.09",
    ),
    "raymond": (
        "Raymond · dyb, rolig og rund",
        "asetrate=36028,aresample=44100,atempo=1.10,bass=f=205:g=3,treble=f=2800:g=-3",
        "asetrate=43000,aresample=44100,atempo=0.98,bass=f=190:g=2,treble=f=3200:g=-1.5",
    ),
    "santa": (
        "Julemanden · varm og fyldig",
        "asetrate=40675,aresample=44100,atempo=1.085,bass=f=175:g=2,"
        "equalizer=f=1250:t=q:w=0.9:g=2.5",
        "bass=f=170:g=1.8,equalizer=f=1500:t=q:w=1:g=2,aecho=0.9:0.85:36:0.075",
    ),
    "lupp": (
        "LUPP · lys, let og legende",
        "asetrate=54293,aresample=44100,atempo=0.89,highpass=f=115,treble=f=3200:g=2",
        "asetrate=45423,aresample=44100,atempo=1.02,highpass=f=110,treble=f=3200:g=1.5",
    ),
}
LIMIT = "alimiter=limit=0.76:level=false:latency=1"
FADE_EDGES = "afade=t=in:d=0.008,areverse,afade=t=in:d=0.012,areverse"


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def load_manifests(repo, bip_manifest):
    manifests = {}
    for avatar in PROFILES:
        path = repo / "web/wwwroot/assets/avatars" / avatar / "audio/audio-manifest.json"
        if avatar == "bip-01":
            path = bip_manifest
        manifest = read_json(path)
        if manifest.get("avatarId") != avatar or manifest.get("sampleRate") != SAMPLE_RATE or manifest.get("channels") != 1:
            raise ValueError(f"{path}: expected {avatar} mono 44.1 kHz manifest")
        phrases = manifest.get("phrases", [])
        by_id = {phrase["id"]: phrase for phrase in phrases}
        if len(phrases) != len(PHRASES) or set(by_id) != set(PHRASES):
            raise ValueError(f"{path}: expected five distinct standard phrase IDs")
        for phrase in phrases:
            line = phrase.get("text")
            if not isinstance(line, str) or not line.strip() or len(line) > 1600:
                raise ValueError(f"{path}: invalid text for {phrase['id']}")
        manifests[avatar] = (manifest, [by_id[phrase] for phrase in PHRASES])
    return manifests


def shape_wav(source, destination, profile):
    filters = f"aresample={SAMPLE_RATE},{profile},highpass=f=45,{LIMIT},{FADE_EDGES}"
    subprocess.run([
        "ffmpeg", "-nostdin", "-hide_banner", "-loglevel", "error", "-n",
        "-i", str(source), "-af", filters, "-ac", "1", "-ar", str(SAMPLE_RATE),
        "-c:a", "pcm_s16le", str(destination),
    ], check=True, timeout=45)
    return filters


def inspect_wav(path, minimum, maximum):
    with wave.open(str(path), "rb") as wav:
        if (wav.getnchannels(), wav.getsampwidth(), wav.getframerate(), wav.getcomptype()) != (1, 2, SAMPLE_RATE, "NONE"):
            raise ValueError(f"{path.name}: expected mono PCM16 at 44.1 kHz")
        frames = wav.getnframes()
        duration = frames / SAMPLE_RATE
        if not minimum <= duration <= maximum:
            raise ValueError(f"{path.name}: unexpected duration {duration:.2f}s")
        samples = array("h", wav.readframes(frames))
        if sys.byteorder != "little":
            samples.byteswap()
        peak = max((abs(sample) for sample in samples), default=0)
        if len(samples) != frames or not 100 < peak <= 25231:
            raise ValueError(f"{path.name}: truncated, silent or over-level audio (peak {peak})")
    return round(duration, 4)


def synthesizer(model_path):
    os.environ["HF_HUB_OFFLINE"] = "1"
    os.environ["TRANSFORMERS_OFFLINE"] = "1"
    sys.dont_write_bytecode = True
    import numpy as np
    import soundfile as sf
    import torch
    from chatterbox.mtl_tts import ChatterboxMultilingualTTS

    device = "cuda" if torch.cuda.is_available() else "cpu"
    torch.set_num_threads(4)
    print(f"Loading local Røst on {device}…", flush=True)
    model = ChatterboxMultilingualTTS.from_local(model_path, device=device)

    def generate(text, seed, reference=None):
        random.seed(seed)
        np.random.seed(seed)
        torch.manual_seed(seed)
        options = dict(GENERATION)
        if reference is not None:
            options["audio_prompt_path"] = str(reference)
        with torch.inference_mode():
            wav = model.generate(text, **options).detach().cpu().squeeze().numpy()
        if wav.ndim != 1 or not wav.size or not np.isfinite(wav).all():
            raise ValueError("Model did not return finite mono speech")
        peak = float(np.abs(wav).max())
        if peak < 0.003:
            raise ValueError("Model returned silent speech")
        return wav * min(1, 0.89 / peak)

    def write_wav(path, samples):
        sf.write(path, samples, model.sr, subtype="PCM_16")

    def reference_audio(path, seeds):
        chunks = [generate(text, seed) for text, seed in zip(REFERENCE_TEXTS, seeds)]
        pause = np.zeros(round(model.sr * 0.22), dtype=np.float32)
        write_wav(path, np.concatenate([chunks[0], pause, chunks[1]]))

    return generate, write_wav, reference_audio


def render_avatar(avatar, manifest, phrases, stage, raw, base_reference, revision, synthesizer_functions):
    generate, write_wav, _ = synthesizer_functions
    voice, prompt_filters, finish_filters = PROFILES[avatar]
    reference = stage / "references" / f"{avatar}.wav"
    prompt_chain = shape_wav(base_reference, reference, prompt_filters)
    reference_duration = inspect_wav(reference, 5, 25)
    reference_bytes = reference.read_bytes()
    voice_id = hashlib.sha256(MODEL.encode() + revision.encode() + reference_bytes).hexdigest()
    destination = stage / avatar
    destination.mkdir()
    clips = []
    for phrase in phrases:
        phrase_id = phrase["id"]
        seed = int.from_bytes(hashlib.sha256(f"{avatar}/{phrase_id}".encode()).digest()[:4], "big")
        print(f"{avatar} · {phrase_id}", flush=True)
        filename = f"{phrase_id}.wav"
        raw_path = raw / f"{avatar}-{filename}"
        output = destination / filename
        write_wav(raw_path, generate(phrase["text"], seed, reference))
        finish_chain = shape_wav(raw_path, output, finish_filters)
        duration = inspect_wav(output, max(0.9, len(phrase["text"].split()) * 0.11), 18)
        digest = hashlib.sha256(output.read_bytes()).hexdigest()
        clips.append(dict(
            phrase, file=filename, path=f"audio/{filename}", available=True, language="da-DK",
            duration=duration, durationSeconds=duration, source="local-roest-chatterbox-character",
            sha256=digest, seed=seed,
        ))
    voice_metadata = {
        "provider": "Røst", "model": MODEL, "modelRevision": revision, "voice": voice,
        "voiceId": voice_id, "style": "synthetic-character", "generation": GENERATION,
        "reference": {
            "file": f"references/{avatar}.wav", "source": "Røst synthetic built-in voice",
            "sha256": hashlib.sha256(reference_bytes).hexdigest(), "durationSeconds": reference_duration,
            "texts": REFERENCE_TEXTS, "seeds": [7101, 7102], "filters": prompt_chain,
        },
        "finishFilters": finish_chain,
    }
    manifest.update(phrases=clips, speech=dict(voice_metadata, available=True, language="da-DK", status="ready"))
    manifest["license"] = dict(
        manifest.get("license") or {},
        speech="Synthesized locally with CoRal Røst Chatterbox and original model-generated conditioning.",
    )
    provenance = dict(voice_metadata, schemaVersion=1, avatarId=avatar, language="da", phrases=[
        {key: phrase[key] for key in ("id", "file", "text", "sha256", "seed")} for phrase in clips
    ])
    write_json(destination / "audio-manifest.json", manifest)
    write_json(destination / "speech-provenance.json", provenance)


def render(args):
    output = args.output.absolute()
    if output.exists() or output.is_symlink():
        raise ValueError(f"Choose a new output directory: {output}")
    source = read_json(args.model / "source.json")
    revision = source.get("revision")
    if source.get("model") != MODEL or not isinstance(revision, str) or not re.fullmatch(r"[0-9a-f]{40}", revision):
        raise ValueError("The local model source.json must identify the Danish Røst checkpoint and revision")
    manifests = load_manifests(args.repo, args.bip_manifest)
    with TemporaryDirectory(prefix=".avatar-voices-", dir=output.parent) as temp:
        temporary = Path(temp)
        stage = temporary / "export"
        (stage / "references").mkdir(parents=True)
        print(f"Model: {MODEL} @ {revision}", flush=True)
        model_functions = synthesizer(args.model)
        base_reference = temporary / "synthetic-reference.wav"
        print("Generating the shared synthetic prompt text…", flush=True)
        model_functions[2](base_reference, (7101, 7102))
        for avatar, (manifest, phrases) in manifests.items():
            render_avatar(avatar, manifest, phrases, stage, temporary, base_reference, revision, model_functions)
        if output.exists() or output.is_symlink():
            raise ValueError(f"Output appeared during rendering: {output}")
        stage.rename(output)
    print(f"Exported 20 Røst phrases and four synthetic reference voices to {output}.", flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model", type=Path, required=True)
    parser.add_argument("--repo", type=Path, required=True)
    parser.add_argument("--bip-manifest", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True, help="New directory inside an existing parent")
    args = parser.parse_args()
    try:
        render(args)
    except (OSError, ValueError, KeyError, TypeError, RuntimeError, wave.Error, subprocess.SubprocessError) as error:
        print(f"Avatar voice render failed: {error}", file=sys.stderr, flush=True)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
