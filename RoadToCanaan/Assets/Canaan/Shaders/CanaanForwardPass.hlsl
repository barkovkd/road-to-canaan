#ifndef CANAAN_FORWARD_PASS_INCLUDED
#define CANAAN_FORWARD_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/Shaders/SimpleLitInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif


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

struct Attributes
{
    float4 positionOS    : POSITION;
    float3 normalOS      : NORMAL;
    float4 tangentOS     : TANGENT;
    float4 texcoord      : TEXCOORD0;   // xyz: stable noise position, w: gloss
    float4 color         : COLOR;       // rgb: tint (sRGB), a: wind weight
    float2 staticLightmapUV    : TEXCOORD1;
    float2 dynamicLightmapUV    : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 uv                       : TEXCOORD0;

    float3 positionWS                  : TEXCOORD1;    // xyz: posWS

    #ifdef _NORMALMAP
        half4 normalWS                 : TEXCOORD2;    // xyz: normal, w: viewDir.x
        half4 tangentWS                : TEXCOORD3;    // xyz: tangent, w: viewDir.y
        half4 bitangentWS              : TEXCOORD4;    // xyz: bitangent, w: viewDir.z
    #else
        half3  normalWS                : TEXCOORD2;
    #endif

    #ifdef _ADDITIONAL_LIGHTS_VERTEX
        half4 fogFactorAndVertexLight  : TEXCOORD5; // x: fogFactor, yzw: vertex light
    #else
        half  fogFactor                 : TEXCOORD5;
    #endif

    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
        float4 shadowCoord             : TEXCOORD6;
    #endif

    DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 7);

#ifdef DYNAMICLIGHTMAP_ON
    float2  dynamicLightmapUV : TEXCOORD8; // Dynamic lightmap UVs
#endif

#ifdef USE_APV_PROBE_OCCLUSION
    float4 probeOcclusion : TEXCOORD9;
#endif

    half4 vcolor                       : TEXCOORD10;
    float4 noisePos                    : TEXCOORD11;
    float4 positionCS                  : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

void InitializeInputData(Varyings input, half3 normalTS, out InputData inputData)
{
    inputData = (InputData)0;

    inputData.positionWS = input.positionWS;
#if defined(DEBUG_DISPLAY)
    inputData.positionCS = input.positionCS;
#endif

    #ifdef _NORMALMAP
        half3 viewDirWS = half3(input.normalWS.w, input.tangentWS.w, input.bitangentWS.w);
        inputData.tangentToWorld = half3x3(input.tangentWS.xyz, input.bitangentWS.xyz, input.normalWS.xyz);
        inputData.normalWS = TransformTangentToWorld(normalTS, inputData.tangentToWorld);
    #else
        half3 viewDirWS = GetWorldSpaceNormalizeViewDir(inputData.positionWS);
        inputData.normalWS = input.normalWS;
    #endif

    inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
    viewDirWS = SafeNormalize(viewDirWS);

    inputData.viewDirectionWS = viewDirWS;

    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
        inputData.shadowCoord = input.shadowCoord;
    #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
        inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
    #else
        inputData.shadowCoord = float4(0, 0, 0, 0);
    #endif

    #ifdef _ADDITIONAL_LIGHTS_VERTEX
        inputData.fogCoord = InitializeInputDataFog(float4(inputData.positionWS, 1.0), input.fogFactorAndVertexLight.x);
        inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
    #else
        inputData.fogCoord = InitializeInputDataFog(float4(inputData.positionWS, 1.0), input.fogFactor);
        inputData.vertexLighting = half3(0, 0, 0);
    #endif

    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

    #if defined(DEBUG_DISPLAY)
    #if defined(DYNAMICLIGHTMAP_ON)
    inputData.dynamicLightmapUV = input.dynamicLightmapUV.xy;
    #endif
    #if defined(LIGHTMAP_ON)
    inputData.staticLightmapUV = input.staticLightmapUV;
    #else
    inputData.vertexSH = input.vertexSH;
    #endif
    #if defined(USE_APV_PROBE_OCCLUSION)
    inputData.probeOcclusion = input.probeOcclusion;
    #endif
    #endif
}

void InitializeBakedGIData(Varyings input, inout InputData inputData)
{
#if defined(_SCREEN_SPACE_IRRADIANCE)
    inputData.bakedGI = SAMPLE_GI(_ScreenSpaceIrradiance, input.positionCS.xy, inputData.normalWS);
#elif defined(DYNAMICLIGHTMAP_ON)
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.dynamicLightmapUV, input.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
#elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
    inputData.bakedGI = SAMPLE_GI(input.vertexSH,
        GetAbsolutePositionWS(inputData.positionWS),
        inputData.normalWS,
        inputData.viewDirectionWS,
        input.positionCS.xy,
        input.probeOcclusion,
        inputData.shadowMask);
#else
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
#endif
}

///////////////////////////////////////////////////////////////////////////////
//                  Vertex and Fragment functions                            //
///////////////////////////////////////////////////////////////////////////////

// Used in Standard (Simple Lighting) shader
Varyings LitPassVertexSimple(Attributes input)
{
    Varyings output = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    input.positionOS.xyz = CanaanSway(input.positionOS.xyz, input.color.a);
    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    output.vcolor = half4(SRGBToLinear(input.color.rgb), input.color.a);
    output.noisePos = input.texcoord;
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

#if defined(_FOG_FRAGMENT)
        half fogFactor = 0;
#else
        half fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
#endif

#if defined(_GRASS)
    output.uv = input.texcoord.xy;
#else
    output.uv = float2(0, 0);
#endif
    output.positionWS.xyz = vertexInput.positionWS;
    output.positionCS = vertexInput.positionCS;

#ifdef _NORMALMAP
    half3 viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
    output.normalWS = half4(normalInput.normalWS, viewDirWS.x);
    output.tangentWS = half4(normalInput.tangentWS, viewDirWS.y);
    output.bitangentWS = half4(normalInput.bitangentWS, viewDirWS.z);
#else
    output.normalWS = NormalizeNormalPerVertex(normalInput.normalWS);
#endif

    OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
#ifdef DYNAMICLIGHTMAP_ON
    output.dynamicLightmapUV = input.dynamicLightmapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
#endif
    OUTPUT_SH4(vertexInput.positionWS, output.normalWS.xyz, GetWorldSpaceNormalizeViewDir(vertexInput.positionWS), output.vertexSH, output.probeOcclusion);

    #ifdef _ADDITIONAL_LIGHTS_VERTEX
        half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);
        output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);
    #else
        output.fogFactor = fogFactor;
    #endif

    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
        output.shadowCoord = GetShadowCoord(vertexInput);
    #endif

    return output;
}

// Used for StandardSimpleLighting shader
void LitPassFragmentSimple(
    Varyings input
    , FRONT_FACE_TYPE frontFace : FRONT_FACE_SEMANTIC
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
#if !defined(_GRASS)
    input.normalWS.xyz = input.normalWS.xyz * IS_FRONT_VFACE(frontFace, 1.0, -1.0);
#endif

    SurfaceData surfaceData;
    InitializeSimpleLitSurfaceData(input.uv, surfaceData);
#if defined(_GRASS)
    // patches of the plain run greener or drier, a few strides across
    half patch = CanaanNoise(float3(input.positionWS.xz * 0.045, 1.7)) * 0.65 + CanaanNoise(float3(input.positionWS.xz * 0.21, 5.1)) * 0.35;
    surfaceData.albedo *= input.vcolor.rgb * lerp(half3(1.04, 0.99, 0.90), half3(0.86, 0.99, 0.84), smoothstep(0.34, 0.72, patch));
    surfaceData.albedo *= lerp(1.0, 0.76, _CanaanWet.x);
    surfaceData.specular = half3(0.02, 0.02, 0.02);
    surfaceData.smoothness = 0.08;
#else
    half grain = CanaanNoise(input.noisePos.xyz * 7.0) * 0.6 + CanaanNoise(input.noisePos.xyz * 29.0) * 0.4;
    half gloss = saturate(input.noisePos.w);
    surfaceData.albedo *= input.vcolor.rgb * (0.88 + 0.24 * grain);
    surfaceData.albedo *= lerp(1.0, 0.72, _CanaanWet.x);
    surfaceData.specular = lerp(half3(0.02, 0.02, 0.02), half3(0.45, 0.45, 0.45), gloss) + _CanaanWet.x * 0.12;
    surfaceData.smoothness = lerp(0.12, 0.82, gloss) + _CanaanWet.x * 0.3;
#endif

#ifdef LOD_FADE_CROSSFADE
    LODFadeCrossFade(input.positionCS);
#endif

    InputData inputData;
    InitializeInputData(input, surfaceData.normalTS, inputData);
    SETUP_DEBUG_TEXTURE_DATA(inputData, UNDO_TRANSFORM_TEX(input.uv, _BaseMap));

#if defined(_DBUFFER)
    ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
#endif

    InitializeBakedGIData(input, inputData);

#if defined(_GRASS)
    Light canaanSun = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
    half through = pow(saturate(dot(-inputData.viewDirectionWS, canaanSun.direction)), 4.0);
    surfaceData.emission += surfaceData.albedo * canaanSun.color * (through * 0.4 * canaanSun.shadowAttenuation * canaanSun.distanceAttenuation);
#endif
    half4 color = UniversalFragmentBlinnPhong(inputData, surfaceData);
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    color.a = OutputAlpha(color.a, IsSurfaceTypeTransparent());

    outColor = color;

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

#endif
