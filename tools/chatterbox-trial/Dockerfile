FROM python:3.11-slim
WORKDIR /trial
RUN apt-get update && apt-get install -y --no-install-recommends libsndfile1 && rm -rf /var/lib/apt/lists/*
RUN pip install --no-cache-dir torch==2.6.0 torchaudio==2.6.0 --index-url https://download.pytorch.org/whl/cpu
RUN pip install --no-cache-dir chatterbox-tts==0.1.7 setuptools==80.9.0 psutil
COPY scripts/chatterbox_trial.py /trial/chatterbox_trial.py
ENV HF_HOME=/cache/huggingface PKUSEG_HOME=/cache/pkuseg PYTHONUNBUFFERED=1 TQDM_DISABLE=1
CMD ["python", "chatterbox_trial.py", "--output", "/results", "--device", "cpu"]
