#ifndef CANAAN_SURFACE_LIB_INCLUDED
#define CANAAN_SURFACE_LIB_INCLUDED

// Shared by Canaan/Surface: the surface texture arrays (set as globals by Mats.Load), noise, wind sway.
//   _SurfColor   rgb: colour detail around 0.8 (multiplied by 1.25 and by the vertex tint), a: metallic
//   _SurfDetail  rg: tangent normal, b: ambient occlusion, a: smoothness

TEXTURE2D_ARRAY(_SurfColor);
SAMPLER(sampler_SurfColor);
TEXTURE2D_ARRAY(_SurfDetail);
SAMPLER(sampler_SurfDetail);

float4 _CanaanWind;        // xy: direction, z: strength, w: phase
float4 _CanaanWet;         // x: wetness 0..1
float _CanaanWearOff;      // 1: no weathering (the perf probe)

half CanaanHash(float3 p)
{
    p = frac(p * 0.3183099 + 0.1);
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}

half CanaanNoise(float3 x)
{
    float3 i = floor(x);
    float3 f = frac(x);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(lerp(CanaanHash(i + float3(0, 0, 0)), CanaanHash(i + float3(1, 0, 0)), f.x),
                     lerp(CanaanHash(i + float3(0, 1, 0)), CanaanHash(i + float3(1, 1, 0)), f.x), f.y),
                lerp(lerp(CanaanHash(i + float3(0, 0, 1)), CanaanHash(i + float3(1, 0, 1)), f.x),
                     lerp(CanaanHash(i + float3(0, 1, 1)), CanaanHash(i + float3(1, 1, 1)), f.x), f.y), f.z);
}

float3 CanaanSway(float3 positionOS, half weight)
{
#if defined(_WIND)
    float3 pivotWS = TransformObjectToWorld(float3(0, 0, 0));
    float3 posWS = TransformObjectToWorld(positionOS);
    float ph = dot(pivotWS.xz, float2(0.13, 0.11)) + _CanaanWind.w;
    float sway = (sin(ph) + 0.5 * sin(ph * 2.3 + 1.7)) * _CanaanWind.z * weight;
    posWS.xz += _CanaanWind.xy * sway;
    return TransformWorldToObject(posWS);
#else
    return positionOS;
#endif
}

// Beds of sandstone in a cliff, by height: each bed its own colour from brick red to buff, now and then a grey
// shale; darker partings between them; the beds warp and tilt a little across the country; dark streaks of
// desert varnish run down the face. 'p' is the world position. A multiplier around 1.
half3 CanaanStrata(float3 p)
{
    float y = p.y + (CanaanNoise(float3(p.xz * 0.006, 1.3)) - 0.5) * 14.0 + (CanaanNoise(float3(p.xz * 0.03, 5.1)) - 0.5) * 2.5;
    float t = y / 2.9;
    float bed = floor(t);
    float f = t - bed;
    half k = CanaanHash(float3(bed, 3.7, 1.9));
    half3 c = lerp(half3(1.02, 0.80, 0.70), half3(1.16, 1.03, 0.88), k);
    c = k > 0.86 ? half3(0.86, 0.82, 0.80) : c;
    c *= 0.88 + 0.12 * smoothstep(0.0, 0.16, f) * smoothstep(1.0, 0.84, f);
    c *= 0.86 + 0.28 * CanaanNoise(float3(p.xz * 0.011, p.y * 0.01 + 4.4));            // whole stretches of the face lighter or darker
    half group = CanaanHash(float3(floor(y / 7.6), 8.1, 2.4));
    c *= 0.9 + 0.2 * group;
    half streak = CanaanNoise(float3((p.x + p.z) * 0.22, p.y * 0.015, 9.3)) * CanaanNoise(float3((p.x - p.z) * 0.07, p.y * 0.01, 2.2));
    c *= 1.0 - 0.38 * smoothstep(0.22, 0.5, streak);
    return c;
}

half3 CanaanUnpack(half2 rg)
{
    half3 n;
    n.xy = rg * 2.0 - 1.0;
    n.z = sqrt(saturate(1.0 - dot(n.xy, n.xy)));
    return n;
}

void CanaanSampleUV(float2 uv, float layer, out half4 colour, out half4 detail)
{
    colour = SAMPLE_TEXTURE2D_ARRAY(_SurfColor, sampler_SurfColor, uv, layer);
    detail = SAMPLE_TEXTURE2D_ARRAY(_SurfDetail, sampler_SurfDetail, uv, layer);
}

// Three planar projections blended by the normal. 'p' is the stable position times repeats per metre,
// 'n' the unit normal in the same space; 'turn' swaps u and v (grain running the other way).
// 'normal' comes back in the space of p and n.
void CanaanSampleTri(float3 p, half3 n, float layer, half turn, out half4 colour, out half4 detail, out half3 normal)
{
    half3 w = abs(n);
    w = w * w * w;
    w = w * w;
    w /= (w.x + w.y + w.z + 1e-5);

    float2 ux = p.zy, uy = p.xz, uz = p.xy;
    bool turned = turn > 0.5;
    if (turned) { ux = ux.yx; uy = uy.yx; uz = uz.yx; }

    half4 cx = SAMPLE_TEXTURE2D_ARRAY(_SurfColor, sampler_SurfColor, ux, layer);
    half4 cy = SAMPLE_TEXTURE2D_ARRAY(_SurfColor, sampler_SurfColor, uy, layer);
    half4 cz = SAMPLE_TEXTURE2D_ARRAY(_SurfColor, sampler_SurfColor, uz, layer);
    half4 dx = SAMPLE_TEXTURE2D_ARRAY(_SurfDetail, sampler_SurfDetail, ux, layer);
    half4 dy = SAMPLE_TEXTURE2D_ARRAY(_SurfDetail, sampler_SurfDetail, uy, layer);
    half4 dz = SAMPLE_TEXTURE2D_ARRAY(_SurfDetail, sampler_SurfDetail, uz, layer);
    colour = cx * w.x + cy * w.y + cz * w.z;
    detail = dx * w.x + dy * w.y + dz * w.z;

    half3 tx = CanaanUnpack(dx.rg), ty = CanaanUnpack(dy.rg), tz = CanaanUnpack(dz.rg);
    if (turned) { tx.xy = tx.yx; ty.xy = ty.yx; tz.xy = tz.yx; }
    // whiteout blend: each tangent normal leans on the surface normal seen in its own plane
    tx = half3(tx.xy + n.zy, tx.z * abs(n.x));
    ty = half3(ty.xy + n.xz, ty.z * abs(n.y));
    tz = half3(tz.xy + n.xy, tz.z * abs(n.z));
    half3 sx = half3(sign(n.x), 1.0, 1.0), sy = half3(1.0, sign(n.y), 1.0), sz = half3(1.0, 1.0, sign(n.z));
    normal = normalize(tx.zyx * sx * w.x + ty.xzy * sy * w.y + tz.xyz * sz * w.z);
}

// Time on the town's materials, by surface layer (SurfaceTable): painted planks and boards (1, 2) bleach in the sun and
// flake to bare grey wood in patches, worst near the ground; a dirty splash along the foot of walls and streaks from
// the top; plaster (12) mottles and sheds in patches to the darker mud under it; tin (6) rusts. 'p' is the position in
// the object (metres; a building's ground is y = 0), 'n' the normal there. How far gone a thing is varies slowly over
// the land with the object's position (smoothly, so a moving car does not flicker).
void CanaanWeather(float3 p, half3 n, half layer, inout half3 tint, inout half smooth)
{
    // only boards, tin and plaster age here: everything else (cloth, skin, leather, stone...) leaves at once
    bool wood = layer > 0.5 && layer < 2.5, tin = layer > 5.5 && layer < 6.5, plaster = layer > 11.5 && layer < 12.5;
    if (!(wood || tin || plaster) || _CanaanWearOff > 0.5) return;
    float3 o = GetObjectToWorldMatrix()._m03_m13_m23;
    half wear = 0.35 + 0.65 * CanaanNoise(float3(o.x, 0.0, o.z) * 0.045 + 5.3);
    half vertical = saturate(1.0 - abs(n.y) * 1.6);
    half big = CanaanNoise(p * float3(0.9, 0.22, 0.9) + 3.1);
    half small = CanaanNoise(p * float3(5.0, 1.4, 5.0) + 7.7);
    half low = saturate(1.0 - p.y / 1.4);
    half splash = saturate((0.5 - p.y + (small - 0.5) * 0.35) * 2.5) * vertical;
    if (wood)
    {
        half grey = dot(tint, half3(0.3, 0.59, 0.11));
        tint = lerp(tint, grey * half3(1.08, 1.04, 0.98) + 0.025, 0.4 * wear);
        // paint lets go along the boards (streaks up and down the grain) in flakes, more of it near the ground
        half flake = CanaanNoise(p * float3(9.0, 1.1, 9.0) + 1.3);
        half peel = saturate(((big * 0.45 + small * 0.25 + flake * 0.3) - (0.8 - 0.42 * wear - 0.2 * low)) * 8.0) * vertical;
        tint = lerp(tint, half3(0.24, 0.215, 0.18), peel);
        half streak = CanaanNoise(float3((p.x + p.z) * 4.3, p.y * 0.18, 1.7));
        half grime = max(splash * 0.42, saturate((streak - 0.6) * 3.0) * 0.2 * vertical) * (0.5 + wear * 0.5);
        tint *= lerp(half3(1.0, 1.0, 1.0), half3(0.72, 0.65, 0.56), grime);
        // decks and steps: sand and dust trodden in, thickest at the edges and in patches
        half flat = saturate(abs(n.y) * 1.6 - 0.6);
        half dust = saturate((big * 0.5 + small * 0.5 - 0.42) * 2.2) * flat * (0.45 + 0.4 * wear);
        tint = lerp(tint, half3(0.52, 0.42, 0.29), dust * 0.6);
        smooth *= 1.0 - 0.6 * max(peel, dust);
    }
    else if (plaster)
    {
        // plaster: mottled, streaked by rain from the top, fallen away in patches (most near the ground and at the
        // corners) to the mud bricks under it, their joints showing
        half shed = saturate(((big * 0.55 + small * 0.45) - (0.8 - 0.32 * wear - 0.2 * low)) * 6.0) * vertical;
        float2 brick = float2((p.x + p.z) * 2.6, p.y * 9.0);
        brick.x += floor(brick.y) * 0.5;
        float2 f = frac(brick);
        half joint = saturate(1.0 - min(f.x * 14.0, f.y * 6.0));
        half3 mud = half3(0.74, 0.6, 0.46) * (0.92 + 0.16 * small) * lerp(1.0, 0.7, joint);
        half drip = saturate((CanaanNoise(float3((p.x + p.z) * 2.2, p.y * 0.12, 4.4)) - 0.55) * 3.0) * vertical;
        tint *= (half3(0.93, 0.93, 0.93) + 0.14 * big) * lerp(1.0, 0.86, drip);
        tint *= lerp(half3(1.0, 1.0, 1.0), mud, shed);
        tint *= lerp(half3(1.0, 1.0, 1.0), half3(0.78, 0.7, 0.6), splash * 0.6);
        smooth *= 1.0 - 0.5 * shed;
    }
    else
    {
        half rust = saturate(((big * 0.7 + small * 0.3) - (0.75 - 0.4 * wear)) * 5.0);
        tint = lerp(tint, half3(0.3, 0.11, 0.045), rust);
        smooth *= 1.0 - 0.7 * rust;
    }
}

#endif
