"""Original synthesized hull impacts; no external samples or Python dependencies."""
import math
from pathlib import Path
import random
import struct
import wave

OUTPUT = Path(__file__).resolve().parents[1] / "Assets/04_Soundtrack/SFX/Departure"
RATE = 44100


def write_impact(name, duration, landing=False):
    rng = random.Random(82 if landing else 24)
    samples = []
    low = 0.0
    phase = 0.0
    for i in range(round(duration * RATE)):
        t = i / RATE
        noise = rng.uniform(-1, 1)
        low += .09 * (noise - low)
        attack = min(1, t / .003)
        phase += 2 * math.pi * ((43 if landing else 88) + 100 * math.exp(-t * 28)) / RATE
        thud = math.sin(phase) * math.exp(-t * (3 if landing else 10))
        clang = sum(math.sin(2 * math.pi * f * t) * math.exp(-t * decay)
                    for f, decay in ([(137, 3), (239, 4), (431, 6)] if landing
                                     else [(331, 11), (577, 13), (941, 18)])) / 3
        transient = noise * math.exp(-t * 75)
        rubble = low * math.exp(-t * (1.8 if landing else 8))
        # Delayed smaller rattles suggest loose panels after the initial collision.
        rattle = 0
        for delay in ([.16, .31, .54, .81] if landing else [.065, .12]):
            if t >= delay:
                rattle += noise * math.exp(-(t-delay) * 28) * .10
        samples.append(attack * (.65 * thud + .30 * clang + .35 * transient + .65 * rubble + rattle))
    peak = max(abs(x) for x in samples)
    # Peak headroom for the quieter siren mixed by Unity.
    pcm = b"".join(struct.pack("<h", round(max(-1, min(1, x / peak * .8)) * 32767)) for x in samples)
    OUTPUT.mkdir(parents=True, exist_ok=True)
    with wave.open(str(OUTPUT / name), "wb") as wav:
        wav.setparams((1, 2, RATE, 0, "NONE", "not compressed"))
        wav.writeframes(pcm)


if __name__ == "__main__":
    write_impact("MeteorHullImpact.wav", .65)
    write_impact("HardLanding.wav", 2.4, landing=True)
