# Assets/Editor/tools/trim_tts_silence.py
# Trims leading/trailing silence from every baked TTS wav, keeping an 80ms pad.
# The game holds on clip.length everywhere, so trimmed silence = faster gameplay.
# Re-run whenever new lines are baked into Resources/TTS.
import wave, os, struct, contextlib

ROOT = os.path.join(os.path.dirname(__file__), "..", "..", "Resources", "TTS")
THRESHOLD = 0.02   # 2% of peak amplitude counts as sound
PAD_S = 0.08       # keep 80ms of natural silence on each side

for f in sorted(os.listdir(ROOT)):
    if not f.lower().endswith(".wav"):
        continue
    p = os.path.join(ROOT, f)
    with contextlib.closing(wave.open(p)) as w:
        n, sr, ch, sw = w.getnframes(), w.getframerate(), w.getnchannels(), w.getsampwidth()
        raw = w.readframes(n)
    assert sw == 2, f"{f}: expected 16-bit"
    samples = struct.unpack(f"<{n*ch}h", raw)
    peak = max(1, max(abs(s) for s in samples))
    th = peak * THRESHOLD
    first = next((i for i in range(0, len(samples), ch) if abs(samples[i]) > th), 0) // ch
    last = next((i for i in range(len(samples)-ch, -1, -ch) if abs(samples[i]) > th), len(samples)-1) // ch
    pad = int(PAD_S * sr)
    start = max(0, first - pad)
    end = min(n, last + 1 + pad)
    if start == 0 and end == n:
        print(f"skip  {f}")
        continue
    body = raw[start*ch*2 : end*ch*2]
    with wave.open(p, "w") as w:
        w.setnchannels(ch); w.setsampwidth(2); w.setframerate(sr)
        w.writeframes(body)
    print(f"trim  {f}: {n/sr:.2f}s -> {(end-start)/sr:.2f}s")
