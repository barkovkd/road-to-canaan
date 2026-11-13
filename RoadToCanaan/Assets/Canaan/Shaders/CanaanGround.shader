// What feet, hooves and wheels leave on the ground (GroundMarks.cs): thin meshes draped over the land, drawn after the
// opaque world and before the air (so the haze lies over them), blended as a 2x multiply: 0.5 leaves the ground as it
// is, less darkens it, more lightens it. A mark never brings a colour of its own - it shades the ground's own texture,
// which is why it does not read as a strip laid on top (the trouble with the first ruts).
// Vertex colour: r the kind (0 rut, 1 trodden, 2 hoof print, 3 loose sand, 4 mud), g the strength.
// uv: a rut has u in metres along it and v 0..1 across; the other kinds have 0..1 over their patch.
Shader "Canaan/GroundMark"
{
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Geometry+460" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Marks"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend DstColor SrcColor
            ZWrite Off
            ZTest LEqual
            Offset -2, -2
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                float3 positionWS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = input.uv;
                o.color = input.color;
                return o;
            }

            float Hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            half4 frag(Varyings input) : SV_Target
            {
                int kind = (int)round(input.color.r * 255.0);
                half strength = input.color.g;
                float2 uv = input.uv;
                float2 w = input.positionWS.xz;
                half n = Noise(w * 2.7) * 0.55 + Noise(w * 9.0) * 0.3 + Noise(w * 31.0) * 0.15;
                half dark = 0.0, light = 0.0;
                if (kind == 0)
                {
                    // a wheel rut: pressed dark in the groove, a lip of loose sand pushed up along both edges
                    half x = abs(uv.y - 0.5) * 2.0;
                    // deep in places, all but filled with blown sand in others: the depth wanders along the rut
                    half depth = saturate(Noise(w * 0.075 + 17.0) * 1.8 - 0.35) * (0.7 + 0.3 * Noise(w * 0.4));
                    dark = saturate(1.0 - x * 1.35) * (0.6 + 0.6 * n) * depth;
                    light = saturate(1.0 - abs(x - 0.86) * 7.0) * (0.3 + 0.7 * n) * depth;
                }
                else if (kind == 1)
                {
                    // ground trodden flat and dark, in blotches
                    half r = length(uv * 2.0 - 1.0);
                    dark = saturate((1.0 - r) * 1.7) * saturate((n - 0.2) * 1.8);
                }
                else if (kind == 2)
                {
                    // a hoof print: the rim of the shoe pressed in, open at the heel, the frog a softer dent
                    half2 q = uv * 2.0 - 1.0;
                    half r = length(q * half2(1.0, 0.86));
                    dark = saturate(1.0 - abs(r - 0.6) * 5.5) * step(-0.4, q.y) + saturate(1.0 - r * 1.9) * 0.3;
                    dark *= 0.8 + 0.4 * n;
                }
                else if (kind == 3)
                {
                    // loose sand blown against something: paler, in soft ripples
                    half r = length(uv * 2.0 - 1.0);
                    half ripple = 0.75 + 0.25 * sin((w.x * 0.8 + w.y * 0.5) * 9.0 + n * 3.0);
                    light = saturate((1.0 - r) * 1.6) * (0.55 + 0.45 * n) * ripple;
                }
                else
                {
                    // mud where water is spilled: dark, its edge ragged
                    half r = length(uv * 2.0 - 1.0);
                    dark = saturate((1.0 - r) * 2.2 + (n - 0.55) * 1.4) * 1.15;
                }
                // a near-field detail: gone by 160 m, before the haze would show it up
                float dist = distance(input.positionWS, _WorldSpaceCameraPos);
                half fade = saturate((160.0 - dist) / 70.0) * strength;
                half f = 0.5 * (1.0 - saturate(dark) * 0.45 * fade) * (1.0 + saturate(light) * 0.2 * fade);
                half3 tint = kind == 4 ? half3(0.96, 0.98, 1.02) : (kind == 3 ? half3(1.01, 1.0, 0.97) : half3(1.0, 0.985, 0.96));
                return half4(f * lerp(half3(1.0, 1.0, 1.0), tint, fade * (dark + light > 0.01 ? 1.0 : 0.0)), 1.0);
            }
            ENDHLSL
        }
    }
}
