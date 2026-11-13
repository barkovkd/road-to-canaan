"""Builds the game's sound clips from recorded sources (CC0, a few CC-BY: see Tools/sound/SOURCES.md for every file,
its licence and author). Runs inside Blender in the background for its audio decoder (aud: ogg, mp3, flac, wav)
and numpy:
    Tools/sounds.sh <sources dir>
The sources dir holds the downloads as Tools/sound/SOURCES.md lists them (one folder per source, archives unpacked
into x/). Writes Assets/Canaan/Resources/Sounds: '<id>__<n>.wav' are the variants of a one-shot, '<id>.wav' a loop.
The game (Core/Sfx.cs) loads these and synthesises only what is missing. Every clip is trimmed of silence, faded at
its ends and levelled; loops are cut so their end flows into their start.
"""
import glob
import math
import os
import sys
import wave

import aud
import numpy as np

ARGS = sys.argv[sys.argv.index("--") + 1:]
SRC, PROJECT = ARGS[0], ARGS[1]
OUT = os.path.join(PROJECT, "Assets/Canaan/Resources/Sounds")
written = []


# ---------------------------------------------------------------------- reading and writing

def find(folder, name):
    hits = sorted(glob.glob(os.path.join(SRC, folder, "**", name), recursive=True))
    if not hits:
        raise FileNotFoundError(os.path.join(folder, name))
    return hits


def load(path):
    """(samples x channels float32, rate)."""
    s = aud.Sound(path)
    d = s.data().astype(np.float32)
    if d.ndim == 1:
        d = d[:, None]
    return d, int(s.specs[0])


def mono(x):
    return x.mean(axis=1, keepdims=True)


def write(name, x, rate):
    """16-bit PCM; 'name' without extension."""
    x = np.clip(x, -1.0, 1.0)
    pcm = (x * 32767.0).astype("<i2")
    path = os.path.join(OUT, name + ".wav")
    with wave.open(path, "wb") as w:
        w.setnchannels(x.shape[1])
        w.setsampwidth(2)
        w.setframerate(int(rate))
        w.writeframes(pcm.tobytes())
    written.append((name, x.shape[0] / rate, x.shape[1], int(rate)))


# ---------------------------------------------------------------------- shaping

def band(x, rate, lo=None, hi=None, slope=2):
    """Zero-phase filtering in the frequency domain with gentle (order 'slope') edges."""
    n = x.shape[0]
    X = np.fft.rfft(x, axis=0)
    f = np.fft.rfftfreq(n, 1.0 / rate)[:, None]
    g = np.ones_like(f)
    if lo:
        g = g / np.sqrt(1.0 + (lo / np.maximum(f, 1.0)) ** (2 * slope))
    if hi:
        g = g / np.sqrt(1.0 + (f / hi) ** (2 * slope))
    return np.fft.irfft(X * g, n=n, axis=0).astype(np.float32)


def fade(x, rate, fin=0.002, fout=0.02):
    x = x.copy()
    a, b = int(fin * rate), int(fout * rate)
    if a > 0:
        x[:a] *= np.linspace(0.0, 1.0, a)[:, None]
    if b > 0:
        x[-b:] *= np.linspace(1.0, 0.0, b)[:, None] ** 2
    return x


def trim(x, rate, start_db=-45.0, end_db=-58.0, pre=0.004):
    env = np.abs(mono(x))[:, 0]
    peak = env.max() + 1e-9
    on = np.nonzero(env > peak * 10 ** (start_db / 20))[0]
    off = np.nonzero(env > peak * 10 ** (end_db / 20))[0]
    a = max(0, on[0] - int(pre * rate))
    b = min(len(env), off[-1] + int(0.03 * rate))
    return fade(x[a:b], rate)


def peak(x, db=-1.0):
    return x * (10 ** (db / 20) / (np.abs(x).max() + 1e-9))


def rms(x, db):
    r = math.sqrt(float((x.astype(np.float64) ** 2).mean()) + 1e-12)
    y = x * (10 ** (db / 20) / r)
    m = np.abs(y).max()
    return y * (0.98 / m) if m > 0.98 else y


def limit(x, rate, gain_db, ceiling_db=-1.0, look=0.001, release=0.012):
    """Raises a shot's body by 'gain_db' under a look-ahead peak limiter: the crack stays under the ceiling, the
    boom and the tail come up (a recorded shot is a needle of a peak over a quiet body; the game wants weight)."""
    y = x * 10 ** (gain_db / 20.0)
    c = 10 ** (ceiling_db / 20.0)
    need = np.minimum(1.0, c / np.maximum(np.abs(y).max(axis=1), 1e-9))
    k = max(1, int(look * rate))
    ahead = np.lib.stride_tricks.sliding_window_view(np.concatenate([need, np.ones(k)]), k)[:len(need)].min(axis=1)
    rel = 1.0 - math.exp(-1.0 / (release * rate))
    g = np.empty_like(ahead)
    prev = 1.0
    for i in range(len(ahead)):
        prev = min(float(ahead[i]), prev + (1.0 - prev) * rel)
        g[i] = prev
    return np.clip(y * g[:, None], -c, c).astype(np.float32)


def loop(x, rate, start, length, cross=1.5):
    """A seamless loop 'length' s long from 'start' s: the extra 'cross' s after it is faded into its beginning."""
    a, n, c = int(start * rate), int(length * rate), int(cross * rate)
    seg = x[a:a + n + c].copy()
    t = np.linspace(0.0, 1.0, c)[:, None]
    head = seg[:c] * np.sqrt(t) + seg[n:n + c] * np.sqrt(1.0 - t)
    out = seg[:n].copy()
    out[:c] = head
    return out


def half_rate(x, rate):
    """96 kHz recordings down to 48 kHz: everything above 21 kHz cut away first."""
    return band(x, rate, hi=21000.0, slope=8)[::2].copy(), rate // 2


def onsets(x, rate, gap=0.07, rise_db=12.0):
    """Starts of the hits in a recording of several (hooves in a stride): where the short envelope jumps over the slow one."""
    env = np.abs(mono(x))[:, 0]
    k = max(1, int(0.002 * rate))
    fast = np.convolve(env, np.ones(k) / k, mode="same")
    slow = np.convolve(env, np.ones(int(0.03 * rate)) / int(0.03 * rate), mode="same") + 1e-6
    hit = (fast > slow * 10 ** (rise_db / 40)) & (fast > fast.max() * 0.12)
    out = []
    for i in np.nonzero(hit)[0]:
        if not out or i - out[-1] > gap * rate:
            out.append(int(i))
    return out


def quiet_span(x, rate, length, lo=20.0, hi=180.0, step=1.0):
    """Start (s) of the 'length' s stretch with the least low rumble (a stretch of rain without thunder)."""
    m = band(mono(x), rate, lo=lo, hi=hi)[:, 0]
    w = int(step * rate)
    energy = np.array([float((m[i:i + w] ** 2).mean()) for i in range(0, len(m) - w, w)])
    n = int(length / step) + 1
    best, at = None, 0
    for i in range(0, len(energy) - n):
        e = energy[i:i + n].max()
        if best is None or e < best:
            best, at = e, i
    return at * step


def loud_peaks(x, rate, count, lo=20.0, hi=180.0, apart=12.0):
    """Times (s) of the 'count' strongest low-frequency events at least 'apart' s from each other (thunderclaps)."""
    m = band(mono(x), rate, lo=lo, hi=hi)[:, 0]
    w = int(0.25 * rate)
    energy = np.array([float((m[i:i + w] ** 2).mean()) for i in range(0, len(m) - w, w)])
    order = np.argsort(energy)[::-1]
    picked = []
    for i in order:
        t = i * 0.25
        if all(abs(t - p) >= apart for p in picked):
            picked.append(t)
        if len(picked) == count:
            break
    return sorted(picked)


# ---------------------------------------------------------------------- the sounds

def one_shots(sid, paths, level=-1.0, lo=None, hi=None, speed=1.0, to_mono=True, start_db=-45.0):
    for i, p in enumerate(paths):
        x, r = load(p)
        if to_mono:
            x = mono(x)
        if lo or hi:
            x = band(x, r, lo, hi)
        x = trim(x, r, start_db=start_db)
        write("%s__%d" % (sid, i), peak(x, level), int(r * speed))


def build():
    os.makedirs(OUT, exist_ok=True)
    for old in glob.glob(os.path.join(OUT, "*.wav")):
        os.remove(old)

    # ---- feet: boots on dirt (Kenney RPG), on boards (Kenney Impact, deep thuds), through grass
    one_shots("step_dirt", [find("kenney_rpg", "footstep0%d.ogg" % i)[0] for i in range(10)], -3.0, lo=70)
    one_shots("step_wood", find("kenney_impact", "footstep_wood_00*.ogg"), -2.0, lo=45)
    one_shots("step_grass", find("kenney_impact", "footstep_grass_00*.ogg"), -4.0, lo=90)

    # ---- hooves: single hits cut out of recorded strides (congusbongus' set; ground, grass and trot are CC0
    # recordings, hall is CC-BY - see SOURCES.md)
    def hooves(sid, names, level):
        n = 0
        for name in names:
            x, r = load(find("horse_gallop", name)[0])
            x = mono(x)
            starts = onsets(x, r)
            for k, a in enumerate(starts):
                b = starts[k + 1] if k + 1 < len(starts) else len(x)
                piece = x[max(0, a - int(0.003 * r)):min(b, a + int(0.22 * r))]
                if piece.shape[0] < int(0.03 * r):
                    continue
                write("%s__%d" % (sid, n), peak(fade(piece, r, 0.001, 0.03), level), r)
                n += 1
        if n == 0:
            raise RuntimeError("no hoof hits found for " + sid)
    hooves("hoof_dirt", ["ground.mp3", "grass.mp3", "muffled.mp3"], -2.0)
    hooves("hoof_wood", ["hall.mp3"], -2.0)

    # ---- the horse: a whinny (a zebra's, CC-BY, brought down in pitch to a horse's), snorts made from its breath
    x, r = load(find("zebra", "zebra.flac")[0])
    x = trim(mono(x), r)
    write("whinny__0", peak(x, -2.0), int(r * 0.78))
    write("whinny__1", peak(x, -3.0), int(r * 0.7))

    # ---- hands and things (Kenney RPG and Impact)
    one_shots("door_open", find("kenney_rpg", "doorOpen_*.ogg") + find("kenney_rpg", "creak*.ogg"), -4.0)
    one_shots("door_close", find("kenney_rpg", "doorClose_*.ogg"), -3.0)
    one_shots("coin", find("kenney_rpg", "handleCoins*.ogg"), -6.0, to_mono=False)
    one_shots("pickup", find("kenney_rpg", "cloth*.ogg") + find("kenney_rpg", "handleSmallLeather*.ogg") + find("kenney_rpg", "dropLeather.ogg"), -6.0, to_mono=False)
    one_shots("punch", find("kenney_impact", "impactPunch_heavy_00*.ogg") + find("kenney_impact", "impactPunch_medium_00*.ogg"), -1.0)
    one_shots("flesh", find("kenney_rpg", "knifeSlice*.ogg") + find("kenney_impact", "impactPunch_medium_00[01].ogg"), -2.0)
    one_shots("knife_draw", find("kenney_rpg", "drawKnife*.ogg"), -4.0)
    one_shots("impact_wood", find("kenney_impact", "impactPlank_medium_00*.ogg"), -3.0)
    one_shots("impact_metal", find("kenney_impact", "impactMetal_light_00*.ogg"), -4.0)
    one_shots("click", find("kenney_rpg", "metalClick.ogg"), -6.0)
    one_shots("reload", find("kenney_rpg", "metalLatch.ogg") + find("kenney_rpg", "metalClick.ogg"), -5.0)

    # ---- the interface (Kenney Interface), stereo and quiet
    one_shots("ui_move", find("kenney_interface", "tick_00*.ogg"), -12.0, to_mono=False)
    one_shots("ui_select", find("kenney_interface", "select_00[1-4].ogg"), -9.0, to_mono=False)
    one_shots("ui_back", find("kenney_interface", "back_00*.ogg"), -10.0, to_mono=False)

    # ---- guns (The Free Firearm Sound Library): see guns() below, run when the library is there
    guns()

    # ---- the land: wind, birds and a river (field recordings, Thimras), crickets (Wolfgang_), fire (AntumDeluge),
    # rain and thunder (kindland)
    x, r = load(find("park", "park_ambience_wind.wav")[0])
    write("wind", rms(band(loop(x, r, 60.0, 42.0, 2.0), r, lo=40), -21.0), r)
    x, r = load(find("park", "park_ambience_birds.wav")[0])
    write("birds", rms(band(loop(x, r, 30.0, 48.0, 2.0), r, lo=300), -26.0), r)
    x, r = load(find("park", "park_ambience_river.wav")[0])
    write("river", rms(mono(loop(x, r, 40.0, 30.0, 2.0)), -20.0), r)
    x, r = load(find("crickets", "crickets_1.mp3")[0])
    write("crickets", rms(loop(x, r, 0.0, 10.0, 1.2), -24.0), r)
    x, r = load(find("fire", "fire-1.wav")[0])
    write("fire", rms(loop(mono(x), r, 0.0, 2.4, 0.45), -18.0), r)
    x, r = load(find("rain", "Dark_Rainy_Night*.ogg")[0])
    # thunder rolls through the recording about once a minute; the first 23 s are rain alone, and the rumble
    # under 120 Hz goes too, so no roll can come round again and again with the loop
    write("rain", rms(band(loop(x, r, 1.5, 20.0, 2.0), r, lo=120), -20.0), r)
    for i, t in enumerate(loud_peaks(x, r, 4)):
        a = max(0, int((t - 1.2) * r))
        clap = x[a:a + int(9.0 * r)]
        write("thunder__%d" % i, peak(fade(clap, r, 0.4, 2.5), -1.0), r)


def guns():
    """Shots, their tails and the working of the guns, from The Free Firearm Sound Library ('Prepared' set)."""
    lib = os.path.join(SRC, "firearms", "x")
    if not os.path.isdir(lib):
        print("guns: no library unpacked at", lib, "- shots stay synthesised")
        return
    import gunmap
    for sid, names, level in gunmap.SHOTS:
        far = sid.endswith("_far")
        keep = 4.5 if far else 3.2                 # the report and the land's answer; the rest of the tail goes
        punch_db = None if far else (-16.0 if sid.startswith("shot_small") else -8.0)
        for i, n in enumerate(names):
            x, r = load(find(os.path.join("firearms", "x"), n)[0])
            if r > 48000:
                x, r = half_rate(x, r)
            x = trim(mono(x), r, start_db=-40.0)
            x = cut_second(x[:int(keep * r)], r)
            x = fade(x, r, 0.0005, x.shape[0] / r * 0.45)
            x = peak(x, -1.0)
            if punch_db is not None:
                x = thump(x, r, punch_db)
            write("%s__%d" % (sid, i), limit(peak(x, -1.0), r, level), r)


def cut_second(x, rate, after=0.45, jump_db=9.0):
    """Ends a take before whatever came after the shot (the next one, the action worked, the microphone handled):
    the first 50 ms window past 'after' s that jumps 'jump_db' over the 300 ms before it."""
    m = np.abs(mono(x))[:, 0]
    b = int(0.05 * rate)
    env = 20 * np.log10(np.array([m[i:i + b].max() for i in range(0, len(m) - b, b)]) + 1e-9)
    for i in range(int(after / 0.05), len(env)):
        if env[i] > env[max(0, i - 6):i].max() + jump_db:
            return x[:max(b, (i - 1) * b)]
    return x


def thump(x, rate, level_db, f0=95.0, f1=50.0, decay=0.07):
    """A low punch under the crack - what a microphone at the range hardly takes and a game shot needs: a sine
    falling from f0 to f1 Hz, dying away in 'decay' s, 'level_db' under the shot's peak."""
    n = min(x.shape[0], int(0.4 * rate))
    t = np.arange(n) / rate
    f = f1 + (f0 - f1) * np.exp(-t / 0.04)
    env = np.exp(-t / decay) * np.minimum(1.0, t / 0.002)
    y = np.sin(2 * np.pi * np.cumsum(f) / rate) * env * 10 ** (level_db / 20.0) * float(np.abs(x).max())
    out = x.copy()
    out[:n, 0] += y.astype(np.float32)
    return out


if __name__ == "__main__":
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    build()
    total = 0.0
    for name, secs, ch, rate in written:
        total += secs
    print("make_sounds: wrote %d clips, %.0f s in all, to %s" % (len(written), total, OUT))
    for name, secs, ch, rate in written:
        print("  %-18s %6.2f s  %d ch  %d Hz" % (name, secs, ch, rate))
