using UnityEngine;
using UnityEngine.Rendering;

namespace Canaan
{
    /// <summary>The cumulus are marched at half the screen's resolution into a texture just before the main camera
    /// renders (CanaanCloudPass.shader), and the sky shader lays that texture over its gradient: a quarter of the
    /// pixels to march, and the bilinear upsampling smooths the march's grain. Both shaders map texels to view
    /// directions through the same camera frame (_CloudRight, _CloudUp, _CloudFwd).</summary>
    public static class CloudLayer
    {
        static Camera cam;
        static Material mat;
        static RenderTexture rt;
        static CommandBuffer cmd;
        static readonly int TexId = Shader.PropertyToID("_CloudTex"), RightId = Shader.PropertyToID("_CloudRight"),
            UpId = Shader.PropertyToID("_CloudUp"), FwdId = Shader.PropertyToID("_CloudFwd"), EyeId = Shader.PropertyToID("_CloudEye"),
            SizeId = Shader.PropertyToID("_CloudSize");

        public static void Init(Camera camera)
        {
            cam = camera;
            mat = Resources.Load<Material>("Mat/CloudPass");
            if (mat == null)
            {
                Debug.LogError("CloudLayer: missing material Mat/CloudPass — run Tools/setup.sh");
                return;
            }
            // until the first pass: no clouds (nothing added, everything gets through)
            var clear = new Texture2D(1, 1, TextureFormat.RGBAHalf, false);
            clear.SetPixel(0, 0, new Color(0f, 0f, 0f, 1f));
            clear.Apply();
            Shader.SetGlobalTexture(TexId, clear);
            cmd = new CommandBuffer { name = "Canaan clouds" };
            RenderPipelineManager.beginCameraRendering -= Render;
            RenderPipelineManager.beginCameraRendering += Render;
        }

        static void Render(ScriptableRenderContext context, Camera c)
        {
            if (c != cam || mat == null) return;
            int w = Mathf.Max(1, c.pixelWidth / 2), h = Mathf.Max(1, c.pixelHeight / 2);
            if (rt == null || rt.width != w || rt.height != h)
            {
                if (rt != null) rt.Release();
                rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
                { name = "Clouds", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, useMipMap = false };
                rt.Create();
            }
            Transform t = c.transform;
            float tanV = Mathf.Tan(c.fieldOfView * 0.5f * Mathf.Deg2Rad), tanH = tanV * c.aspect;
            cmd.Clear();
            cmd.SetGlobalVector(RightId, new Vector4(t.right.x, t.right.y, t.right.z, 0.5f / tanH));
            cmd.SetGlobalVector(UpId, new Vector4(t.up.x, t.up.y, t.up.z, 0.5f / tanV));
            cmd.SetGlobalVector(FwdId, t.forward);
            cmd.SetGlobalVector(EyeId, t.position);
            cmd.SetGlobalVector(SizeId, new Vector4(w, h, 1f / w, 1f / h));
            cmd.SetRenderTarget(rt);
            cmd.DrawProcedural(Matrix4x4.identity, mat, 0, MeshTopology.Triangles, 3, 1);
            cmd.SetGlobalTexture(TexId, rt);
            Graphics.ExecuteCommandBuffer(cmd);
        }
    }
}
