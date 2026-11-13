// The sky: gradient with a warm side towards the sun, HDR sun disc and halo, moon, stars, a high veil of cirrus,
// the cumulus layer (marched as a volume at half resolution by CanaanCloudPass: bright flanks and a silver edge
// towards the sun, dark bases) and a haze band at the horizon that uses the same colour as the fog on the land.
// All colours come from globals set by SkyWeather, so the same values drive fog and ambient light.
Shader "Canaan/Sky"
{
    Properties { }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "CanaanAtmosphere.hlsl"
            #include "CanaanClouds.hlsl"

            half4 _SkyTop;
            half4 _SkyHorizon;
            half4 _SkyGround;
            half4 _SkySunColor;
            half4 _SkyCloudLit;
            half4 _SkyCloudDark;
            float4 _SkyMoonDir;
            TEXTURE2D(_CloudTex);
            SAMPLER(sampler_CloudTex);
            float4 _CloudRight;
            float4 _CloudUp;
            float4 _CloudFwd;
            float4 _CloudSize;

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dir : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.dir = mul((float3x3)UNITY_MATRIX_M, input.positionOS.xyz);
                return output;
            }

            float Hash2(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float Hash3(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float Noise2(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash2(i), Hash2(i + float2(1, 0)), f.x),
                            lerp(Hash2(i + float2(0, 1)), Hash2(i + float2(1, 1)), f.x), f.y);
            }

            float Fbm(float2 p, int octaves)
            {
                float a = 0.5;
                float s = 0.0;
                for (int i = 0; i < octaves; i++)
                {
                    s += a * Noise2(p);
                    p = p * 2.03 + float2(17.1, 9.2);
                    a *= 0.5;
                }
                return s;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 d = normalize(input.dir);
                float h = d.y;
                float sd = dot(d, _SkySunDir.xyz);
                half clear = _SkyParams.w;

                // gradient, warmer and brighter on the sun's side
                half up = pow(saturate(h), 0.45);
                half3 col = lerp(_SkyHorizon.rgb, _SkyTop.rgb, up);
                half side = pow(saturate(sd * 0.5 + 0.5), 5.0);
                col += _SkySunColor.rgb * side * (0.22 * clear) * (1.0 - up * 0.7);

                // stars and a faint band of them
                float3 q = floor(d * 330.0);
                float rnd = Hash3(q);
                float twinkle = 0.75 + 0.25 * sin(_Time.y * (1.5 + rnd * 4.0) + rnd * 40.0);
                float star = step(0.9984, rnd) * _SkyParams.x * saturate(h * 5.0) * twinkle;
                col += star * (0.7 + 0.6 * Hash3(q + 7.0));
                float band = exp(-abs(dot(d, normalize(float3(0.35, 0.5, -0.8)))) * 7.0);
                col += half3(0.05, 0.06, 0.09) * band * _SkyParams.x * saturate(h * 3.0) * (0.6 + 0.8 * Noise2(d.xz * 9.0));

                // sun: bright disc for the bloom, tight and wide halos
                float above = saturate(h * 30.0 + 0.6);
                float disc = smoothstep(0.99950, 0.99985, sd);
                float halo = pow(saturate(sd), 600.0) * 2.0 + pow(saturate(sd), 60.0) * 0.35 + pow(saturate(sd), 9.0) * 0.14;
                col += _SkySunColor.rgb * (disc * 26.0 + halo * clear) * above;

                // moon
                float md = dot(d, _SkyMoonDir.xyz);
                float moon = smoothstep(0.99950, 0.99972, md);
                col += half3(0.78, 0.84, 0.95) * (moon * 2.2 + pow(saturate(md), 250.0) * 0.3 + pow(saturate(md), 30.0) * 0.05) * _SkyParams.x;

                // clouds on a plane overhead
                if (h > 0.004)
                {
                    float cover = _SkyParams.y;
                    float2 plane = d.xz / (h + 0.13);
                    float2 drift = float2(_SkyParams.z, _SkyParams.z * 0.37);
                    float2 toSun = normalize(_SkySunDir.xz + float2(1e-4, 0.0));

                    // high thin layer
                    float2 uh = plane * 0.33 + drift * 0.4 + 31.0;
                    float nh = Fbm(float2(uh.x * 0.55 + uh.y * 0.2, uh.y * 1.6), 4);
                    // streaks of cirrus in any weather (the cumulus hides them when it closes over)
                    float ch = smoothstep(0.6, 0.92, nh) * 0.32 * saturate(h * 5.0);
                    half3 highCol = lerp(_SkyCloudDark.rgb, _SkyCloudLit.rgb, 0.8) + _SkySunColor.rgb * pow(saturate(sd), 8.0) * 0.35;
                    col = lerp(col, highCol, ch);

                }

                // cumulus: marched at half resolution before the camera renders (CanaanCloudPass, CloudLayer.cs);
                // the texel for this direction is found on the plane one unit in front of the camera
                if (h > -0.02)
                {
                    float3 dv = d / max(dot(d, _CloudFwd.xyz), 1e-4);
                    float2 cuv = float2(0.5 + dot(dv, _CloudRight.xyz) * _CloudRight.w, 0.5 - dot(dv, _CloudUp.xyz) * _CloudUp.w);
                    if (dot(d, _CloudFwd.xyz) > 0.0 && all(cuv >= 0.0) && all(cuv <= 1.0))
                    {
                        // four bilinear taps half a texel apart: a tent over 3 x 3 texels that smooths the march's grain
                        float2 o = _CloudSize.zw * 0.5;
                        half4 cl = (SAMPLE_TEXTURE2D_LOD(_CloudTex, sampler_CloudTex, cuv + float2(-o.x, -o.y), 0)
                                  + SAMPLE_TEXTURE2D_LOD(_CloudTex, sampler_CloudTex, cuv + float2(o.x, -o.y), 0)
                                  + SAMPLE_TEXTURE2D_LOD(_CloudTex, sampler_CloudTex, cuv + float2(-o.x, o.y), 0)
                                  + SAMPLE_TEXTURE2D_LOD(_CloudTex, sampler_CloudTex, cuv + float2(o.x, o.y), 0)) * 0.25;
                        col = lerp(col, col * cl.a + cl.rgb, smoothstep(-0.015, 0.03, h));
                    }
                }

                // below the horizon, and the haze band that meets the fog on the land
                col = lerp(col, _SkyGround.rgb, saturate(-h * 6.0));
                half haze = exp(-max(h, 0.0) * 13.0) * _AtmoHeight.w;
                col = lerp(col, CanaanFogColor(d, 1.0), saturate(haze));

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
