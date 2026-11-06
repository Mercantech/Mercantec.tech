"""Offline, loopback-only adapter for an existing CoRal Røst/Chatterbox installation."""
import os

os.environ["HF_HUB_OFFLINE"] = "1"
os.environ["TRANSFORMERS_OFFLINE"] = "1"
os.environ["PYTHONDONTWRITEBYTECODE"] = "1"

import asyncio
import io
import logging
import threading
from contextlib import asynccontextmanager
from pathlib import Path

from fastapi import FastAPI, HTTPException, Request, Response
from pydantic import BaseModel, ConfigDict, Field

logger = logging.getLogger("rost")
model = None
model_failed = False
generation_lock = threading.Lock()
MODEL_ID = "CoRal-project/roest-v3-chatterbox-500m"
MAX_AUDIO_BYTES = 8 * 1024 * 1024


def load_model():
    global model, model_failed
    try:
        import torch
        from chatterbox.mtl_tts import ChatterboxMultilingualTTS

        checkpoint = Path(os.environ["ROST_MODEL_PATH"])
        required = ("ve.pt", "s3gen.pt", "t3_mtl23ls_v2.safetensors", "conds.pt", "grapheme_mtl_merged_expanded_v1.json")
        if not all((checkpoint / name).is_file() for name in required):
            raise RuntimeError("Incomplete local Røst checkpoint")
        device = "cuda" if torch.cuda.is_available() else "cpu"
        torch.set_num_threads(4)
        loaded = ChatterboxMultilingualTTS.from_local(checkpoint, device=device)
        # First-use vocoder initialization can exceed a normal request timeout. Keep health
        # unavailable until a short, harmless utterance has warmed the complete pipeline.
        with torch.inference_mode():
            loaded.generate("Klar.", language_id="da", temperature=0.6, cfg_weight=0.5,
                            exaggeration=0.5, repetition_penalty=2.0, top_p=0.95)
        model = loaded
        logger.info("Local Røst model ready on %s", device)
    except Exception:
        model_failed = True
        logger.exception("Local Røst model could not start")


@asynccontextmanager
async def lifespan(app):
    loader = asyncio.create_task(asyncio.to_thread(load_model))
    yield
    # The existing Python process owns the model; no weights or installation are changed.
    if loader.done():
        await loader


app = FastAPI(lifespan=lifespan, docs_url=None, redoc_url=None, openapi_url=None)


@app.middleware("http")
async def local_only(request: Request, call_next):
    if request.client is None or request.client.host not in ("127.0.0.1", "::1"):
        return Response(status_code=403)
    length = request.headers.get("content-length")
    if length and (not length.isdigit() or int(length) > 8192):
        return Response(status_code=413)
    return await call_next(request)


@app.get("/health")
async def health():
    ready = model is not None
    return Response(content='{"ready":' + str(ready).lower() + ',"provider":"Røst","model":"' + MODEL_ID
                    + '","state":"' + ("ready" if ready else "failed" if model_failed else "warming") + '"}',
                    status_code=200 if ready else 503, media_type="application/json")


class SpeechRequest(BaseModel):
    model_config = ConfigDict(extra="forbid")
    text: str = Field(min_length=1, max_length=1600)


def generate(text):
    import numpy as np
    import soundfile as sf
    import torch

    # One local model owns the GPU. Never start parallel inference after a caller disconnects.
    if not generation_lock.acquire(blocking=False):
        raise HTTPException(status_code=503, detail="busy")
    try:
        with torch.inference_mode():
            wav = model.generate(text, language_id="da", temperature=0.6, cfg_weight=0.5,
                                 exaggeration=0.5, repetition_penalty=2.0, top_p=0.95)
        samples = wav.detach().cpu().squeeze().numpy()
        if samples.size == 0 or not np.isfinite(samples).all():
            raise HTTPException(status_code=503, detail="invalid_audio")
        output = io.BytesIO()
        sf.write(output, samples, model.sr, format="WAV", subtype="PCM_16")
        data = output.getvalue()
        if not 44 < len(data) <= MAX_AUDIO_BYTES:
            raise HTTPException(status_code=503, detail="invalid_audio")
        return data
    finally:
        generation_lock.release()


@app.post("/speech")
async def speech(request: SpeechRequest):
    if model is None:
        raise HTTPException(status_code=503, detail="not_ready")
    if not request.text.strip() or any(ord(char) < 32 for char in request.text):
        raise HTTPException(status_code=400, detail="invalid_text")
    try:
        audio = await asyncio.to_thread(generate, request.text)
    except HTTPException:
        raise
    except Exception:
        logger.exception("Local speech generation failed")
        raise HTTPException(status_code=503, detail="generation_failed") from None
    return Response(audio, media_type="audio/wav", headers={"Cache-Control": "no-store"})
