// Cumulus: a shell of cloud between CLOUD_BASE and CLOUD_TOP over a round earth, shaped from a tiling 3D noise
// (Resources/Clouds/cloud_noise, Tools/textures/make_clouds.py). A coverage map decides where clouds stand (cover
// from SkyWeather: scattered heaps in fair weather, a crowd of them with blue between when cloudy, a closed deck in
// rain), the noise carves them, a height profile gives them flat bases and rounded tops, a finer noise frays the
// edges. The march (CloudMarch) runs at half the screen's resolution in CanaanCloudPass.shader before the camera
// renders (CloudLayer.cs); the sky lays the result over its gradient (CanaanSky). The atmosphere pass looks up along
// the sun from the ground for the clouds' shadows (CanaanAtmosphere). Tune the shape offline first:
// Tools/textures/cloud_preview.py renders the same model without the game.
#ifndef CANAAN_CLOUDS_INCLUDED
#define CANAAN_CLOUDS_INCLUDED

TEXTURE3D(_CloudNoise);
SAMPLER(sampler_CloudNoise);
float4 _SkyParams;   // x: stars, y: cloud cover, z: cloud drift, w: clear-sky factor (sun glow)
float4 _SkySunDir;

#define CLOUD_BASE 1500.0
#define CLOUD_TOP 3700.0
#define CLOUD_EARTH 6371000.0
#define CLOUD_SHAPE (1.0 / 5600.0)      // one repeat of the shape noise: 5.6 km
#define CLOUD_DETAIL (1.0 / 1300.0)

float CloudRemap(float v, float lo, float hi, float a, float b) { return a + (v - lo) * (b - a) / (hi - lo); }

float CloudHash(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

// metres the cloud field has drifted with the wind
float2 CloudWind() { return float2(_SkyParams.z, _SkyParams.z * 0.37) * 2600.0; }

// where clouds stand: the weather's cover, varied from place to place - here a clear patch, there a crowd of heaps;
// in overcast and rain the variation dies out and the deck closes. One fetch of the shape noise stretched over 40 km
// (lumps of about ten kilometres): it runs at every step of the march and every pixel of the shadows
float CloudCoverage(float2 xz)
{
    float cover = _SkyParams.y;
    float2 q = (xz + CloudWind()) * (1.0 / 40000.0);
    float n = SAMPLE_TEXTURE3D_LOD(_CloudNoise, sampler_CloudNoise, float3(q.x, 0.31, q.y), 0).r;
    float deck = saturate((cover - 0.8) * 5.0);
    return saturate(cover + (n - 0.55) * 0.75 * (1.0 - deck));
}

// 0 below the base, rising fast above it, rounding off towards the top
float CloudProfile(float hf) { return saturate(hf / 0.1) * saturate((1.0 - hf) / 0.6); }

// 0..1: empty air to the dense core of a heap. The shape noise lies between about 0.5 and 0.9; stretched over 0..1,
// the cover is the share of the air the heaps fill (0.3: a third of the sky, 0.6: two thirds, with blue between),
// and the steep ramp past the threshold gives the heaps hard edges and solid bodies
float CloudDensity(float3 p, float cov, bool detail)
{
    float hf = (p.y - CLOUD_BASE) / (CLOUD_TOP - CLOUD_BASE);
    if (hf <= 0.0 || hf >= 1.0 || cov < 0.005) return 0.0;
    float2 w = CloudWind();
    float4 n = SAMPLE_TEXTURE3D_LOD(_CloudNoise, sampler_CloudNoise, (p + float3(w.x, hf * 600.0, w.y)) * CLOUD_SHAPE, 0);
    float low = n.g * 0.625 + n.b * 0.25 + n.a * 0.125;
    float shape = saturate(CloudRemap(n.r, low - 1.0, 1.0, 0.0, 1.0));
    shape = saturate((shape - 0.56) / 0.3) * CloudProfile(hf);
    float edge = 1.0 - cov;
    if (shape <= edge) return 0.0;
    if (detail)
    {
        // a finer noise bites into the surface: cauliflower on the flanks, frayed wisps under the base
        float4 m = SAMPLE_TEXTURE3D_LOD(_CloudNoise, sampler_CloudNoise, (p + float3(w.x * 1.7, 0.0, w.y * 1.7)) * CLOUD_DETAIL, 0);
        float fine = m.g * 0.625 + m.b * 0.25 + m.a * 0.125;
        shape -= (1.0 - fine) * 0.16 * lerp(1.6, 1.0, saturate(hf * 4.0));
    }
    return saturate((shape - edge) * 3.0);
}

// where a ray from the ground leaves a sphere of radius r round the earth's centre (the camera stands on the surface)
float CloudShell(float3 rd, float camY, float r)
{
    float3 o = float3(0.0, CLOUD_EARTH + camY, 0.0);
    float b = dot(o, rd);
    float c = dot(o, o) - r * r;
    float disc = b * b - c;
    return -b + sqrt(max(disc, 0.0));
}

// The cumulus along a ray from 'eye' in direction d: the light they send (rgb) and the share of what lies behind
// them that gets through (a). Each step that touches a heap is lit by a short march towards the sun - by night
// towards the moon, in SkyWeather's moonlit colours. Long steps through empty air; on the first touch of a heap the
// ray backs off and goes on in steps a third as long, so the surface the eye sees is found closely (less grain),
// then long again once it is out.
half4 CloudMarch(float3 d, float3 eye, float jitter, half3 lit, half3 dark, half3 sunColor, float3 moonDir)
{
    if (d.y <= -0.02) return half4(0.0, 0.0, 0.0, 1.0);
    float t0 = CloudShell(d, eye.y, CLOUD_EARTH + CLOUD_BASE);
    float t1 = min(min(CloudShell(d, eye.y, CLOUD_EARTH + CLOUD_TOP), t0 + 24000.0), 130000.0);
    if (t1 <= t0) return half4(0.0, 0.0, 0.0, 1.0);

    int steps = (int)lerp(26.0, 14.0, saturate(d.y * 2.5));
    float coarse = (t1 - t0) / steps;
    float stepLen = coarse;
    bool fine = false;
    int empty = 0;
    float t = t0 + coarse * jitter;
    bool byMoon = _SkySunDir.y < -0.05;
    float3 s = byMoon ? moonDir : _SkySunDir.xyz;
    float sunUp = saturate(s.y * 8.0 + 0.4) * (byMoon ? _SkyParams.x : 1.0);
    half3 glow = byMoon ? lit : sunColor;
    float forward = pow(saturate(dot(d, s)), 6.0);
    float T = 1.0;
    half3 acc = 0.0;
    float3 earth = float3(0.0, CLOUD_EARTH + eye.y, 0.0);
    for (int i = 0; i < 44; i++)
    {
        if (t > t1 || T < 0.02) break;
        float alt = length(earth + d * t) - CLOUD_EARTH;
        float3 p = float3(eye.x + d.x * t, alt, eye.z + d.z * t);
        float cov = CloudCoverage(p.xz);
        float den = CloudDensity(p, cov, true);
        if (den > 0.002 && !fine)
        {
            fine = true;
            empty = 0;
            stepLen = coarse * 0.33;
            t -= coarse * 0.67;
            continue;
        }
        if (den > 0.002)
        {
            empty = 0;
            float ld = 0.0;
            [unroll] for (int k = 1; k <= 4; k++)
                ld += CloudDensity(p + s * (k * k * 70.0), cov, false) * ((k * 2 - 1) * 70.0);
            // light that goes straight through, plus what scatters on inside the heap and still gets out: the
            // shaded side greys instead of going black
            float x = ld * 0.01;
            float sunT = (exp(-x) * 0.6 + exp(-x * 0.3) * 0.28 + exp(-x * 0.08) * 0.12) * sunUp;
            float powder = 1.0 - exp(-den * 4.0);
            float hf = saturate((alt - CLOUD_BASE) / (CLOUD_TOP - CLOUD_BASE));
            float light = sunT * lerp(1.0, powder * 1.5, 0.4 * (1.0 - forward));
            half3 c = lerp(dark * (0.55 + 0.6 * hf), lit, saturate(light)) + glow * sunT * forward * 1.4 * (1.0 - den * 0.5);
            float a = 1.0 - exp(-den * 0.02 * stepLen);
            acc += T * a * c;
            T *= 1.0 - a;
        }
        else if (fine && ++empty >= 3)
        {
            fine = false;
            stepLen = coarse;
        }
        t += stepLen;
    }
    return half4(acc, T);
}

// the share of sunlight the clouds let through to a point on the ground (1: open sky): two looks up along the sun,
// through the dense lower part of the layer
float CloudShadow(float3 positionWS)
{
    float3 s = _SkySunDir.xyz;
    if (s.y < 0.04 || _SkyParams.y < 0.02) return 1.0;
    float3 p = positionWS + s * ((CLOUD_BASE + 600.0 - positionWS.y) / s.y);
    float cov = CloudCoverage(p.xz);
    float d = CloudDensity(p, cov, false) + CloudDensity(p + s * (700.0 / s.y), cov, false);
    return 1.0 - saturate(d * 2.0);
}

#endif
