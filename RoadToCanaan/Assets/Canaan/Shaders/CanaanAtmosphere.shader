// Full-screen pass run before the transparent queue: lays the air over everything opaque (height fog, haze,
// warm scattering towards the sun) and adds light shafts where the sun is partly hidden.
// Output is "light scattered in" with alpha = what survives of the scene, blended One / SrcAlpha.
Shader "Canaan/Atmosphere"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend One SrcAlpha, Zero One

        Pass
        {
            Name "Atmosphere"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Random.hlsl"
            #include "CanaanAtmosphere.hlsl"
            #include "CanaanClouds.hlsl"

            #define SHAFT_STEPS 22
            float _CloudShadowOff;   // 1: no cloud shadows (the perf probe)

            bool IsSky(float raw)
            {
            #if UNITY_REVERSED_Z
                return raw < 1e-7;
            #else
                return raw > 0.9999999;
            #endif
            }

            // Share of the way from this pixel to the sun that is open sky.
            half SunVisibility(float2 uv, float2 sunUV, float jitter)
            {
                float2 step = (sunUV - uv) * (0.92 / SHAFT_STEPS);
                float2 p = uv + step * jitter;
                half sum = 0.0, weight = 1.0, total = 0.0;
                for (int i = 0; i < SHAFT_STEPS; i++)
                {
                    float2 q = saturate(p);
                    sum += IsSky(SampleSceneDepth(q)) ? weight : 0.0;
                    total += weight;
                    weight *= 0.95;
                    p += step;
                }
                return sum / total;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.positionCS.xy / _ScaledScreenParams.xy;
                float raw = SampleSceneDepth(uv);
                bool sky = IsSky(raw);
            #if UNITY_REVERSED_Z
                float depth = raw;
            #else
                float depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, raw);
            #endif
                float3 positionWS = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                float3 v = positionWS - _WorldSpaceCameraPos;
                float dist = length(v);
                float3 dir = v / max(dist, 1e-4);

                // light shafts: only when the sun is in front of the camera
                half open = 1.0;
                half shaft = 0.0;
                float3 sunWS = _WorldSpaceCameraPos + _AtmoSunDir.xyz * 5000.0;
                float facing = dot(_AtmoSunDir.xyz, -UNITY_MATRIX_V[2].xyz);
                if (_AtmoSunDir.w > 0.001 && facing > 0.05)
                {
                    float2 sunUV = ComputeNormalizedDeviceCoordinatesWithZ(sunWS, UNITY_MATRIX_VP).xy;
                    float jitter = InterleavedGradientNoise(input.positionCS.xy, 0);
                    half vis = SunVisibility(uv, sunUV, jitter);
                    float2 off = sunUV - 0.5;
                    half onScreen = saturate(1.6 - max(abs(off.x), abs(off.y)) * 2.0) * saturate(facing * 4.0);
                    open = lerp(1.0, vis, onScreen);
                    half towards = pow(saturate(dot(dir, _AtmoSunDir.xyz)), 5.0);
                    shaft = vis * towards * onScreen * _AtmoSunDir.w;
                }

                half f = sky ? 0.0 : CanaanFogAmount(_WorldSpaceCameraPos, dir, dist);
                half3 scattered = CanaanFogColor(dir, lerp(0.35, 1.0, open)) * f;
                scattered += _AtmoSunColor.rgb * shaft * (sky ? 0.35 : 1.0);
                // the shadows of the clouds sail over the land: where one lies, the sun is gone and the sky's light
                // stays - so only what the sun reaches darkens (to about the tone of a house's shadow), and the
                // shadows already there stay as they are
                half shade = 1.0;
                if (!sky && _CloudShadowOff < 0.5)
                {
                    half cloud = CloudShadow(positionWS);
                    half sunlit = cloud < 0.999 ? MainLightRealtimeShadow(TransformWorldToShadowCoord(positionWS)) : 1.0;
                    shade = 1.0 - (1.0 - cloud) * sunlit * 0.5;
                }
                return half4(scattered, (1.0 - f) * shade);
            }
            ENDHLSL
        }
    }
}
