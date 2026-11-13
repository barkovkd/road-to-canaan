#!/usr/bin/env python3
"""Builds Canaan/Lit from the installed URP SimpleLit shader.

The game colours everything through vertex colours, so the stock shader is copied and its forward pass is
patched: vertex colour tint, stable procedural detail noise, per-vertex gloss, wind sway for vegetation.
Copying (instead of hand-writing) keeps keywords and passes in step with the installed URP version.

Usage: make_shaders.py <unity_editor_root> <project_root>
"""
import re
import sys
from pathlib import Path

editor = Path(sys.argv[1])
project = Path(sys.argv[2])
urp = editor / "Unity.app/Contents/Resources/PackageManager/BuiltInPackages/com.unity.render-pipelines.universal"
out = project / "Assets/Canaan/Shaders"
out.mkdir(parents=True, exist_ok=True)

DROP_PASSES = {"GBuffer", "Meta", "Universal2D", "MotionVectors", "XRMotionVectors"}


def split_passes(text):
    """Yields (start, end, name) for every Pass { ... } block."""
    for m in re.finditer(r"^[ \t]*Pass\s*\n[ \t]*\{", text, re.M):
        depth = 0
        i = text.index("{", m.start())
        j = i
        while True:
            ch = text[j]
            if ch == "{":
                depth += 1
            elif ch == "}":
                depth -= 1
                if depth == 0:
                    break
            j += 1
        block = text[m.start():j + 1]
        name = re.search(r'Name\s+"([^"]+)"', block)
        yield m.start(), j + 1, name.group(1) if name else ""


def must_replace(text, old, new, count=1):
    if old not in text:
        raise SystemExit(f"make_shaders: pattern not found: {old[:70]!r}")
    return text.replace(old, new, count)


# ---------------------------------------------------------------- Lit.shader
src = (urp / "Shaders/SimpleLit.shader").read_text()
src = must_replace(src, 'Shader "Universal Render Pipeline/Simple Lit"', 'Shader "Canaan/Lit"')
for start, end, name in reversed(list(split_passes(src))):
    if name in DROP_PASSES:
        src = src[:start] + src[end:]
src = must_replace(
    src,
    '#include "Packages/com.unity.render-pipelines.universal/Shaders/SimpleLitForwardPass.hlsl"',
    '#include "CanaanForwardPass.hlsl"')
src = must_replace(
    src,
    "#pragma shader_feature_local _NORMALMAP",
    "#pragma shader_feature_local _NORMALMAP\n            #pragma shader_feature_local _ _WIND _GRASS")
src = re.sub(r'^\s*CustomEditor.*\n', '', src, flags=re.M)
(out / "CanaanLit.shader").write_text(src)

# ------------------------------------------------------- forward pass
fp = (urp / "Shaders/SimpleLitForwardPass.hlsl").read_text()
fp = must_replace(fp, '#include "SimpleLitInput.hlsl"',
                  '#include "Packages/com.unity.render-pipelines.universal/Shaders/SimpleLitInput.hlsl"')
fp = must_replace(fp, "UNIVERSAL_SIMPLE_LIT_PASS_INCLUDED", "CANAAN_FORWARD_PASS_INCLUDED", 2)

fp = must_replace(fp, "    float2 texcoord      : TEXCOORD0;",
                  "    float4 texcoord      : TEXCOORD0;   // xyz: stable noise position, w: gloss\n"
                  "    float4 color         : COLOR;       // rgb: tint (sRGB), a: wind weight")
fp = must_replace(fp, "    float4 positionCS                  : SV_POSITION;",
                  "    half4 vcolor                       : TEXCOORD10;\n"
                  "    float4 noisePos                    : TEXCOORD11;\n"
                  "    float4 positionCS                  : SV_POSITION;")

helpers = r'''
float4 _CanaanWind;        // xy: direction, z: strength, w: phase
float4 _CanaanGrass;       // x: fade start, y: fade end
float4 _CanaanWet;         // x: wetness 0..1

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
#if defined(_WIND) || defined(_GRASS)
    float3 pivotWS = TransformObjectToWorld(float3(0, 0, 0));
    float3 posWS = TransformObjectToWorld(positionOS);
    float ph = dot(pivotWS.xz, float2(0.13, 0.11)) + _CanaanWind.w;
    float sway = (sin(ph) + 0.5 * sin(ph * 2.3 + 1.7)) * _CanaanWind.z * weight;
    posWS.xz += _CanaanWind.xy * sway;
#if defined(_GRASS)
    float d = distance(pivotWS, _WorldSpaceCameraPos);
    float s = saturate((_CanaanGrass.y - d) / max(_CanaanGrass.y - _CanaanGrass.x, 0.01));
    posWS = lerp(float3(posWS.x, pivotWS.y, posWS.z), posWS, s);
#endif
    return TransformWorldToObject(posWS);
#else
    return positionOS;
#endif
}
'''
fp = must_replace(fp, "struct Attributes", helpers + "\nstruct Attributes")

fp = must_replace(
    fp,
    "    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);",
    "    input.positionOS.xyz = CanaanSway(input.positionOS.xyz, input.color.a);\n"
    "    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);\n"
    "    output.vcolor = half4(SRGBToLinear(input.color.rgb), input.color.a);\n"
    "    output.noisePos = input.texcoord;")
# grass is the one thing that reads a texture here: tufts cut out of the atlas (make_grass.py)
fp = must_replace(fp, "    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);",
                  "#if defined(_GRASS)\n    output.uv = input.texcoord.xy;\n#else\n    output.uv = float2(0, 0);\n#endif")
fp = must_replace(
    fp,
    "    InitializeSimpleLitSurfaceData(input.uv, surfaceData);",
    "    InitializeSimpleLitSurfaceData(input.uv, surfaceData);\n"
    "#if defined(_GRASS)\n"
    "    // patches of the plain run greener or drier, a few strides across\n"
    "    half patch = CanaanNoise(float3(input.positionWS.xz * 0.045, 1.7)) * 0.65 + CanaanNoise(float3(input.positionWS.xz * 0.21, 5.1)) * 0.35;\n"
    "    surfaceData.albedo *= input.vcolor.rgb * lerp(half3(1.04, 0.99, 0.90), half3(0.86, 0.99, 0.84), smoothstep(0.34, 0.72, patch));\n"
    "    surfaceData.albedo *= lerp(1.0, 0.76, _CanaanWet.x);\n"
    "    surfaceData.specular = half3(0.02, 0.02, 0.02);\n"
    "    surfaceData.smoothness = 0.08;\n"
    "#else\n"
    "    half grain = CanaanNoise(input.noisePos.xyz * 7.0) * 0.6 + CanaanNoise(input.noisePos.xyz * 29.0) * 0.4;\n"
    "    half gloss = saturate(input.noisePos.w);\n"
    "    surfaceData.albedo *= input.vcolor.rgb * (0.88 + 0.24 * grain);\n"
    "    surfaceData.albedo *= lerp(1.0, 0.72, _CanaanWet.x);\n"
    "    surfaceData.specular = lerp(half3(0.02, 0.02, 0.02), half3(0.45, 0.45, 0.45), gloss) + _CanaanWet.x * 0.12;\n"
    "    surfaceData.smoothness = lerp(0.12, 0.82, gloss) + _CanaanWet.x * 0.3;\n"
    "#endif")
# grass seen against the sun glows: light comes through the blades
fp = must_replace(
    fp,
    "    half4 color = UniversalFragmentBlinnPhong(inputData, surfaceData);",
    "#if defined(_GRASS)\n"
    "    Light canaanSun = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);\n"
    "    half through = pow(saturate(dot(-inputData.viewDirectionWS, canaanSun.direction)), 4.0);\n"
    "    surfaceData.emission += surfaceData.albedo * canaanSun.color * (through * 0.4 * canaanSun.shadowAttenuation * canaanSun.distanceAttenuation);\n"
    "#endif\n"
    "    half4 color = UniversalFragmentBlinnPhong(inputData, surfaceData);")
# thin two-sided geometry (canvas, awnings): light the side that is looked at
fp = must_replace(fp, "    Varyings input\n    , out half4 outColor : SV_Target0",
                  "    Varyings input\n    , FRONT_FACE_TYPE frontFace : FRONT_FACE_SEMANTIC\n    , out half4 outColor : SV_Target0")
fp = must_replace(
    fp,
    "    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);\n\n    SurfaceData surfaceData;",
    "    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);\n"
    "#if !defined(_GRASS)\n"
    "    input.normalWS.xyz = input.normalWS.xyz * IS_FRONT_VFACE(frontFace, 1.0, -1.0);\n"
    "#endif\n\n    SurfaceData surfaceData;")
(out / "CanaanForwardPass.hlsl").write_text(fp)
print("make_shaders: wrote", out / "CanaanLit.shader", "and CanaanForwardPass.hlsl")


# ================================================================ Canaan/Surface (PBR, from URP Lit)
# The physically based shader for everything solid. Texture comes from two texture arrays shared by the
# whole game (CanaanSurfaceLib.hlsl); the layer and the repeat are per-vertex (TEXCOORD3), so one material
# draws wood, iron and cloth alike. _TRIPLANAR: procedural meshes without UVs. Otherwise: Blender models.
src = (urp / "Shaders/Lit.shader").read_text()
src = must_replace(src, 'Shader "Universal Render Pipeline/Lit"', 'Shader "Canaan/Surface"')
for start, end, name in reversed(list(split_passes(src))):
    if name in DROP_PASSES:
        src = src[:start] + src[end:]
src = must_replace(
    src,
    '#include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"',
    '#include "CanaanSurfacePass.hlsl"')
src = must_replace(
    src,
    "#pragma shader_feature_local _NORMALMAP",
    "#pragma shader_feature_local _NORMALMAP\n            #pragma shader_feature_local _ _TRIPLANAR\n            #pragma shader_feature_local _ _WIND\n            #pragma shader_feature_local _ _STRATA")
src = re.sub(r'^\s*CustomEditor.*\n', '', src, flags=re.M)
(out / "CanaanSurface.shader").write_text(src)

sp = (urp / "Shaders/LitForwardPass.hlsl").read_text()
sp = must_replace(sp, '#include "LitInput.hlsl"',
                  '#include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"')
sp = must_replace(sp, "UNIVERSAL_FORWARD_LIT_PASS_INCLUDED", "CANAAN_SURFACE_PASS_INCLUDED", 2)
sp = must_replace(sp, "struct Attributes", '#include "CanaanSurfaceLib.hlsl"\n\nstruct Attributes')
sp = must_replace(
    sp, "    float2 texcoord     : TEXCOORD0;",
    "    float4 texcoord     : TEXCOORD0;   // UV models: xy = uv. Triplanar: xyz = stable position, w = gloss\n"
    "    float4 color        : COLOR;       // rgb: tint (sRGB), a: wind weight\n"
    "    float4 surf         : TEXCOORD3;   // x: layer of the surface arrays, y: repeats per metre, z: quarter turn\n"
    "    float3 bindNormal   : TEXCOORD4;   // normal in the bind pose: a triplanar blend that does not swim on skinned meshes")
sp = must_replace(
    sp, "    float4 positionCS               : SV_POSITION;",
    "    half4 vcolor                    : TEXCOORD11;\n"
    "    float4 stable                   : TEXCOORD12;  // xyz: stable position, w: gloss\n"
    "    float4 surf                     : TEXCOORD13;\n"
    "    half3 bindNormal                : TEXCOORD14;\n"
    "    float4 positionCS               : SV_POSITION;")
sp = must_replace(
    sp,
    "    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);",
    "    input.positionOS.xyz = CanaanSway(input.positionOS.xyz, input.color.a);\n"
    "    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);\n"
    "    output.vcolor = half4(SRGBToLinear(input.color.rgb), input.color.a);\n"
    "#if defined(_TRIPLANAR)\n"
    "    output.stable = input.texcoord;\n"
    "    output.bindNormal = input.bindNormal;\n"
    "#else\n"
    "    output.stable = float4(input.positionOS.xyz, 0.0);\n"
    "    output.bindNormal = input.normalOS;\n"
    "#endif\n"
    "    output.surf = input.surf;")
sp = must_replace(sp, "    Varyings input\n    , out half4 outColor : SV_Target0",
                  "    Varyings input\n    , FRONT_FACE_TYPE frontFace : FRONT_FACE_SEMANTIC\n    , out half4 outColor : SV_Target0")
sp = must_replace(
    sp,
    "    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);\n\n#if defined(_PARALLAXMAP)",
    "    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);\n"
    "    input.normalWS.xyz = input.normalWS.xyz * IS_FRONT_VFACE(frontFace, 1.0, -1.0);   // thin two-sided geometry\n\n"
    "#if defined(_PARALLAXMAP)")
sp = must_replace(
    sp,
    "    InitializeStandardLitSurfaceData(input.uv, surfaceData);",
    "    InitializeStandardLitSurfaceData(input.uv, surfaceData);\n"
    "    half4 sc, sd;\n"
    "    half3 triNormal = half3(0.0, 1.0, 0.0);\n"
    "    half3 bindN = normalize(input.bindNormal);\n"
    "#if defined(_TRIPLANAR)\n"
    "    CanaanSampleTri(input.stable.xyz * input.surf.y, bindN, input.surf.x, input.surf.z, sc, sd, triNormal);\n"
    "#else\n"
    "    CanaanSampleUV(input.uv * input.surf.y, input.surf.x, sc, sd);\n"
    "    surfaceData.normalTS = CanaanUnpack(sd.rg);\n"
    "#endif\n"
    "    half patch = CanaanNoise(input.stable.xyz * 0.31) * 0.6 + CanaanNoise(input.stable.xyz * 1.9) * 0.4;\n"
    "    half wet = _CanaanWet.x;\n"
    "    half3 tint = input.vcolor.rgb;\n"
    "    half aged = 1.0;\n"
    "    CanaanWeather(input.stable.xyz, bindN, input.surf.x, tint, aged);\n"
    "    surfaceData.albedo *= tint * sc.rgb * (1.25 * (0.9 + 0.2 * patch));\n"
    "#if defined(_STRATA)\n"
    "    surfaceData.albedo *= CanaanStrata(input.stable.xyz);\n"
    "#endif\n"
    "    surfaceData.albedo *= lerp(1.0, 0.66, wet * (1.0 - sc.a));\n"
    "    surfaceData.metallic = sc.a;\n"
    "    surfaceData.occlusion = sd.b;\n"
    "    surfaceData.smoothness = saturate(sd.a * aged + input.stable.w * 0.45 + wet * 0.42);")
sp = must_replace(
    sp,
    "    InitializeInputData(input, surfaceData.normalTS, inputData);",
    "    InitializeInputData(input, surfaceData.normalTS, inputData);\n"
    "#if defined(_TRIPLANAR)\n"
    "    inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS + TransformObjectToWorldDir(triNormal - bindN, false));\n"
    "#endif")
(out / "CanaanSurfacePass.hlsl").write_text(sp)
print("make_shaders: wrote", out / "CanaanSurface.shader", "and CanaanSurfacePass.hlsl")
