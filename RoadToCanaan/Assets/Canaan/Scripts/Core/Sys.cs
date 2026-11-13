using System;
using System.IO;
using UnityEngine;

namespace Canaan
{
    /// <summary>Entry point: the Boot scene is empty, the game object is created from code.</summary>
    public static class Boot
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start()
        {
            var go = new GameObject("Game");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<Game>();
        }
    }

    /// <summary>Command line of the player.</summary>
    public static class Args
    {
        static readonly string[] argv = Environment.GetCommandLineArgs();

        public static bool Has(string flag) { return Array.IndexOf(argv, flag) >= 0; }

        public static string Get(string flag, string fallback = null)
        {
            int i = Array.IndexOf(argv, flag);
            return i >= 0 && i + 1 < argv.Length ? argv[i + 1] : fallback;
        }

        public static string AutoTest => Get("-autotest");
        public static bool IsTest => AutoTest != null;
        /// <summary>A test that runs in a real window at real speed (frame-rate measurement).</summary>
        public static bool RealRender => Has("-realrender");
        public static bool Offscreen => IsTest && !RealRender;
        public static string OutDir => Get("-outdir", Path.Combine(Application.persistentDataPath, "test_out"));
    }

    /// <summary>Renders a camera (with its overlay stack) into a JPG. Works headless.</summary>
    public static class Shot
    {
        /// <summary>Renders the camera into a cell of a larger texture (contact sheets).</summary>
        public static void CaptureInto(Camera cam, Texture2D sheet, int x, int y, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture prevTarget = cam.targetTexture;
            RenderTexture prevActive = RenderTexture.active;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prevTarget;
            RenderTexture.active = rt;
            sheet.ReadPixels(new Rect(0, 0, w, h), x, y);
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);
        }

        public static void Capture(Camera cam, string path, int w = 1280, int h = 720)
        {
            // a camera that already renders into a texture of this size keeps it (the interface is laid out for it)
            RenderTexture prevTarget = cam.targetTexture;
            bool own = prevTarget != null && prevTarget.width == w && prevTarget.height == h;
            var rt = own ? prevTarget : RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture prevActive = RenderTexture.active;
            bool wasEnabled = cam.enabled;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prevTarget;
            cam.enabled = wasEnabled;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;
            if (!own) RenderTexture.ReleaseTemporary(rt);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToJPG(86));
            UnityEngine.Object.Destroy(tex);
        }
    }
}
