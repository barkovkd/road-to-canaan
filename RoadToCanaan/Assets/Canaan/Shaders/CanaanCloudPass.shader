// The cloud layer at half the screen's resolution: one full-screen triangle into CloudLayer's texture before the
// camera renders. Each texel marches the cumulus (CloudMarch in CanaanClouds.hlsl) along the view direction it
// stands for; the sky shader finds the texel again from its own direction (_CloudRight/_CloudUp/_CloudFwd), so the
// two never depend on which way a render target is flipped. Output: rgb the clouds' light, a what gets through.
Shader "Hidden/Canaan/CloudPass"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "CanaanClouds.hlsl"

            half4 _SkySunColor;
            half4 _SkyCloudLit;
            half4 _SkyCloudDark;
            float4 _SkyMoonDir;
            float4 _CloudRight;   // xyz: the camera's right, w: 0.5 / tan(half the horizontal view)
            float4 _CloudUp;      // xyz: up, w: 0.5 / tan(half the vertical view)
            float4 _CloudFwd;     // xyz: forward
            float4 _CloudEye;     // xyz: the camera's position
            float4 _CloudSize;    // xy: texels, zw: 1 / texels

            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(uint id : SV_VertexID)
            {
                Varyings o;
                float2 uv = float2((id << 1) & 2, id & 2);
                o.positionCS = float4(uv * 2.0 - 1.0, UNITY_NEAR_CLIP_VALUE, 1.0);
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // texel row 0 is the top of the view, as the sky reads it
                float2 uv = input.positionCS.xy * _CloudSize.zw;
                float3 d = normalize(_CloudFwd.xyz + _CloudRight.xyz * ((uv.x - 0.5) / _CloudRight.w) + _CloudUp.xyz * ((0.5 - uv.y) / _CloudUp.w));
                // white noise that changes every frame: the film grain hides it, where a fixed pattern would hatch the clouds
                float jitter = CloudHash(input.positionCS.xy + frac(_Time.y * 7.0) * float2(311.0, 173.0));
                return CloudMarch(d, _CloudEye.xyz, jitter, _SkyCloudLit.rgb, _SkyCloudDark.rgb, _SkySunColor.rgb, _SkyMoonDir.xyz);
            }
            ENDHLSL
        }
    }
}
