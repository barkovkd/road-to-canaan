// River and pond surface. Refracts what lies under it (the opaque texture), absorbs light with depth,
// reflects the sky with a Fresnel term and a sun glint, breaks into foam at the shore, and takes the same
// air as everything else. Vertex colour alpha carries the depth of the bed (0 = shore, 1 = deep), written
// by the region generator; the depth texture gives the soft waterline.
Shader "Canaan/Water"
{
    Properties
    {
        _Shallow("Shallow", Color) = (0.42, 0.52, 0.42, 1)
        _Deep("Deep", Color) = (0.07, 0.17, 0.19, 1)
        _Flow("Flow (xz speed)", Vector) = (0.0, 0.0, 0.35, 0.0)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha, Zero One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "CanaanAtmosphere.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Shallow;
                half4 _Deep;
                float4 _Flow;
            CBUFFER_END

            half4 _SkyHorizon;
            half4 _SkyTop;
            half4 _SkySunColor;
            float4 _SkySunDir;
            float4 _SkyParams;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half bed : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash2(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float Noise2(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash2(i), Hash2(i + float2(1, 0)), f.x),
                            lerp(Hash2(i + float2(0, 1)), Hash2(i + float2(1, 1)), f.x), f.y);
            }

            float Ripple(float2 p)
            {
                float2 t = _Flow.xz * _Time.y;
                return Noise2(p * 0.55 + t) * 0.55 + Noise2(p * 1.7 - t * 1.5 + 5.0) * 0.32 + Noise2(p * 4.3 + t * 2.2 + 11.0) * 0.13;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.bed = input.color.a;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 p = input.positionWS.xz;
                float e = 0.22;
                float h0 = Ripple(p);
                float3 n = float3((h0 - Ripple(p + float2(e, 0))) * 0.75, 1.0, (h0 - Ripple(p + float2(0, e))) * 0.75);
                n.xz *= lerp(0.18, 1.0, saturate(14.0 / (input.positionCS.w + 1.0)));     // far water is calm: no sparkle of aliased ripples
                n = normalize(n);
                float3 v = normalize(GetWorldSpaceViewDir(input.positionWS));

                // how much water the view ray crosses before it meets the bed
                float2 suv = input.positionCS.xy / _ScaledScreenParams.xy;
                float surface = input.positionCS.w;
                float thick = max(0.0, LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams) - surface);

                // refraction: look a little aside, but never at something in front of the water
                float2 ruv = suv + n.xz * (0.035 * saturate(thick * 0.6)) / max(1.0, surface * 0.08);
                float rthick = LinearEyeDepth(SampleSceneDepth(ruv), _ZBufferParams) - surface;
                if (rthick < 0.0) { ruv = suv; rthick = thick; }
                half3 under = SampleSceneColor(ruv);

                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half sunlit = light.shadowAttenuation;
                half3 ambient = lerp(_SkyHorizon.rgb, _SkyTop.rgb, 0.5) * 0.55;
                half3 lighting = light.color * (saturate(light.direction.y) * (0.25 + 0.75 * sunlit)) + ambient;

                // absorption: clear at the edge, the colour of the deep further in
                half deep = saturate(max(input.bed, 1.0 - exp(-rthick * 0.55)));
                half3 body = lerp(_Shallow.rgb, _Deep.rgb, deep) * lighting;
                half3 col = lerp(under * lerp(half3(1, 1, 1), _Shallow.rgb * 1.6, saturate(rthick * 0.8)), body, deep * 0.92);

                // reflection of the sky and the sun
                float3 r = reflect(-v, n);
                r.y = abs(r.y);
                half3 sky = lerp(_SkyHorizon.rgb, _SkyTop.rgb, pow(saturate(r.y), 0.5));
                sky = lerp(sky, CanaanFogColor(r, 1.0), exp(-r.y * 9.0) * _AtmoHeight.w);
                half fresnel = 0.02 + 0.98 * pow(1.0 - saturate(dot(n, v)), 5.0);
                half glint = pow(saturate(dot(r, _SkySunDir.xyz)), 380.0) * 14.0 + pow(saturate(dot(r, _SkySunDir.xyz)), 30.0) * 0.25;
                col = lerp(col, sky, fresnel * 0.9);
                col += _SkySunColor.rgb * glint * sunlit * _SkyParams.w;

                // foam where the water is thin
                half foamNoise = Noise2(p * 3.5 + _Flow.xz * _Time.y * 2.0) * 0.6 + Noise2(p * 11.0 - _Flow.xz * _Time.y) * 0.4;
                half foam = saturate((0.22 - thick) * 5.0) * smoothstep(0.35, 0.75, foamNoise);
                col = lerp(col, lighting * 0.85, foam * 0.7);

                col = CanaanApplyAtmo(col, input.positionWS);
                half alpha = saturate(thick * 9.0);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}
