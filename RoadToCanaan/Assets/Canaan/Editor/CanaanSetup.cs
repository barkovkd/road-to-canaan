using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Canaan.EditorTools
{
    /// <summary>
    /// One-off project configuration, run headless by Tools/setup.sh: player settings, the URP assets,
    /// the sample materials that pull the game's shaders into the build, and the empty Boot scene.
    /// Safe to run again after changes.
    /// </summary>
    public static class CanaanSetup
    {
        public const string Root = "Assets/Canaan";
        public const string BootScene = Root + "/Scenes/Boot.unity";

        public static void Run()
        {
            try
            {
                Directory.CreateDirectory(Root + "/Scenes");
                Directory.CreateDirectory(Root + "/Settings");
                Directory.CreateDirectory(Root + "/Resources/Mat");
                Directory.CreateDirectory(Root + "/Resources/Surf");
                Directory.CreateDirectory(Root + "/Resources/Terrain");
                AssetDatabase.Refresh();
                Player();
                Pipeline();
                Surfaces();
                Grass();
                Clouds();
                Sounds();
                Materials();
                CanaanModelImport.BuildLibrary();
                Features();
                Scene();
                AssetDatabase.SaveAssets();
                Debug.Log("CANAAN_SETUP_OK");
            }
            catch (Exception e)
            {
                Debug.LogError("CANAAN_SETUP_FAIL " + e);
                EditorApplication.Exit(1);
            }
        }

        static void Player()
        {
            PlayerSettings.companyName = "Canaan";
            PlayerSettings.productName = "RoadToCanaan";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.canaan.roadtocanaan");
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 1000;
            PlayerSettings.macRetinaSupport = false;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.usePlayerLog = true;
            PlayerSettings.gcIncremental = true;

            // Input System only: the game polls devices through GameInput and never touches UnityEngine.Input.
            var ps = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (ps != null && ps.Length > 0)
            {
                var so = new SerializedObject(ps[0]);
                // skinning on the graphics card, in batches: people and horses carry 10-20 thousand vertices each since
                // the Blender models, and the processor has the rest of the world to run
                var deform = so.FindProperty("meshDeformation");
                var gpu = so.FindProperty("gpuSkinning");
                if (deform != null && deform.intValue != 2) { deform.intValue = 2; if (gpu != null) gpu.intValue = 1; so.ApplyModifiedPropertiesWithoutUndo(); Debug.Log("CANAAN_SETUP mesh deformation set to GPU (batched)"); }
                var handler = so.FindProperty("activeInputHandler");
                if (handler != null && handler.intValue != 1)
                {
                    handler.intValue = 1;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    Debug.Log("CANAAN_SETUP activeInputHandler set to Input System (takes effect on the next launch)");
                }
            }

            QualitySettings.vSyncCount = 1;
            QualitySettings.skinWeights = SkinWeights.FourBones;
            QualitySettings.lodBias = 1.5f;
        }

        static void Set(SerializedObject so, string name, Action<SerializedProperty> apply)
        {
            var p = so.FindProperty(name);
            if (p == null) { Debug.LogWarning("CANAAN_SETUP missing URP field " + name); return; }
            apply(p);
        }

        static void Pipeline()
        {
            string rendererPath = Root + "/Settings/CanaanRenderer.asset";
            string assetPath = Root + "/Settings/CanaanURP.asset";
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            if (asset == null)
            {
                var create = typeof(UniversalRenderPipelineAsset).GetMethod(
                    "CreateRendererAsset", BindingFlags.Static | BindingFlags.NonPublic);
                if (create == null) throw new Exception("UniversalRenderPipelineAsset.CreateRendererAsset not found");
                var data = (ScriptableRendererData)create.Invoke(
                    null, new object[] { rendererPath, RendererType.UniversalRenderer, false, "Renderer" });
                asset = UniversalRenderPipelineAsset.Create(data);
                AssetDatabase.CreateAsset(asset, assetPath);
            }

            var so = new SerializedObject(asset);
            Set(so, "m_SupportsHDR", p => p.boolValue = true);
            Set(so, "m_MSAA", p => p.intValue = 1);
            Set(so, "m_RenderScale", p => p.floatValue = 1f);
            Set(so, "m_MainLightShadowmapResolution", p => p.intValue = 4096);
            Set(so, "m_ShadowDistance", p => p.floatValue = 170f);
            Set(so, "m_ShadowCascadeCount", p => p.intValue = 4);
            Set(so, "m_Cascade4Split", p => p.vector3Value = new Vector3(0.045f, 0.13f, 0.36f));
            Set(so, "m_CascadeBorder", p => p.floatValue = 0.12f);
            Set(so, "m_ShadowDepthBias", p => p.floatValue = 1.2f);
            Set(so, "m_ShadowNormalBias", p => p.floatValue = 1.7f);           // rounded figures: no blocky self-shadow at the terminator
            Set(so, "m_SoftShadowsSupported", p => p.boolValue = true);
            Set(so, "m_SoftShadowQuality", p => p.intValue = 3);                 // High
            Set(so, "m_AdditionalLightsRenderingMode", p => p.intValue = 1);
            Set(so, "m_AdditionalLightShadowsSupported", p => p.boolValue = true);
            Set(so, "m_AdditionalLightsShadowmapResolution", p => p.intValue = 2048);
            Set(so, "m_RequireDepthTexture", p => p.boolValue = true);           // water, the atmosphere pass, depth of field
            Set(so, "m_RequireOpaqueTexture", p => p.boolValue = true);          // water refraction
            Set(so, "m_OpaqueDownsampling", p => p.intValue = 1);                // 2x bilinear
            Set(so, "m_ColorGradingMode", p => p.intValue = 1);
            Set(so, "m_SupportsLightCookies", p => p.boolValue = false);
            Set(so, "m_SupportDataDrivenLensFlare", p => p.boolValue = false);
            Set(so, "m_SupportScreenSpaceLensFlare", p => p.boolValue = false);
            so.ApplyModifiedPropertiesWithoutUndo();

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer != null)
            {
                var rso = new SerializedObject(renderer);
                Set(rso, "m_RenderingMode", p => p.intValue = 2); // Forward+
                Set(rso, "m_CopyDepthMode", p => p.intValue = 0); // after opaques: the atmosphere pass reads it before the transparents
                rso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(renderer);
            }
            EditorUtility.SetDirty(asset);

            GraphicsSettings.defaultRenderPipeline = asset;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }
            QualitySettings.SetQualityLevel(current, false);
        }

        /// <summary>Finds a renderer feature by name or adds it the way the inspector does (sub-asset plus id map).</summary>
        static T Feature<T>(ScriptableRendererData data, string name) where T : ScriptableRendererFeature
        {
            foreach (var f in data.rendererFeatures)
                if (f != null && f.name == name && f is T found) return found;
            var feature = ScriptableObject.CreateInstance<T>();
            feature.name = name;
            AssetDatabase.AddObjectToAsset(feature, data);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);
            var so = new SerializedObject(data);
            var list = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            Debug.Log("CANAAN_SETUP added renderer feature " + name);
            return feature;
        }

        /// <summary>Ambient occlusion first, then the air over everything opaque. Both run before the transparents, in this order.</summary>
        static void Features()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Root + "/Settings/CanaanRenderer.asset");
            if (renderer == null) throw new Exception("renderer asset missing");

            var ssao = Feature<ScreenSpaceAmbientOcclusion>(renderer, "AmbientOcclusion");
            var sso = new SerializedObject(ssao);
            Set(sso, "m_Settings.AOMethod", p => p.intValue = 1);               // interleaved gradient noise: no textures needed
            Set(sso, "m_Settings.Downsample", p => p.boolValue = false);
            Set(sso, "m_Settings.AfterOpaque", p => p.boolValue = true);        // no depth prepass: depth always matches what was drawn
            Set(sso, "m_Settings.Source", p => p.intValue = 0);                 // depth
            Set(sso, "m_Settings.NormalSamples", p => p.intValue = 1);
            Set(sso, "m_Settings.Intensity", p => p.floatValue = 1.5f);
            Set(sso, "m_Settings.DirectLightingStrength", p => p.floatValue = 0.35f);
            Set(sso, "m_Settings.Radius", p => p.floatValue = 0.5f);
            Set(sso, "m_Settings.Samples", p => p.intValue = 1);                // medium, 8 samples
            Set(sso, "m_Settings.BlurQuality", p => p.intValue = 0);            // bilateral
            Set(sso, "m_Settings.Falloff", p => p.floatValue = 140f);
            sso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ssao);

            var atmo = Feature<FullScreenPassRendererFeature>(renderer, "Atmosphere");
            atmo.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingTransparents;
            atmo.fetchColorBuffer = false;
            atmo.requirements = ScriptableRenderPassInput.Depth;
            atmo.passMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Resources/Mat/Atmosphere.mat");
            atmo.passIndex = 0;
            atmo.bindDepthStencilAttachment = false;
            if (atmo.passMaterial == null) throw new Exception("Atmosphere material missing");
            EditorUtility.SetDirty(atmo);
            EditorUtility.SetDirty(renderer);
        }

        const string TexDir = Root + "/Textures/Surf/";
        const string GrassAtlas = Root + "/Textures/Grass/grass_atlas.png";
        const float GrassCutoff = 0.36f;

        /// <summary>Import settings for the grass atlas (Tools/textures/make_grass.py): cut-out, with mips that keep the blades from thinning away.</summary>
        const string CloudNoise = "Assets/Canaan/Resources/Clouds/cloud_noise.png";

        /// <summary>The cloud noise is written as an 8 x 8 flipbook of slices; Unity stacks it into a Texture3D.</summary>
        static void Clouds()
        {
            AssetDatabase.Refresh();
            var imp = AssetImporter.GetAtPath(CloudNoise) as TextureImporter;
            if (imp == null) throw new Exception("cloud noise missing: run Tools/textures.sh clouds");
            var st = new TextureImporterSettings();
            imp.ReadTextureSettings(st);
            if (st.textureShape == TextureImporterShape.Texture3D && st.flipbookColumns == 8 && st.flipbookRows == 8 && !imp.sRGBTexture
                && imp.wrapMode == TextureWrapMode.Repeat && imp.textureCompression == TextureImporterCompression.Uncompressed
                && imp.ignorePngGamma) return;
            st.textureType = TextureImporterType.Default;
            st.textureShape = TextureImporterShape.Texture3D;
            st.flipbookColumns = 8;
            st.flipbookRows = 8;
            st.sRGBTexture = false;
            st.alphaSource = TextureImporterAlphaSource.FromInput;
            st.alphaIsTransparency = false;
            st.mipmapEnabled = true;
            st.wrapMode = TextureWrapMode.Repeat;
            st.filterMode = FilterMode.Bilinear;
            st.readable = false;
            imp.SetTextureSettings(st);
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.ignorePngGamma = true;                     // the file says gamma 1.0: honoured, it brightens the noise
            imp.SaveAndReimport();
        }

        /// <summary>The recorded sounds (Tools/sounds.sh): the long loops stay Vorbis-compressed in memory, the
        /// one-shots (short, played often) are decoded on load.</summary>
        static void Sounds()
        {
            string dir = Root + "/Resources/Sounds";
            if (!Directory.Exists(dir)) return;
            int changed = 0;
            foreach (string file in Directory.GetFiles(dir, "*.wav"))
            {
                string path = file.Replace('\\', '/');
                if (!(AssetImporter.GetAtPath(path) is AudioImporter imp)) continue;
                bool loop = !Path.GetFileNameWithoutExtension(path).Contains("__");
                AudioImporterSampleSettings st = imp.defaultSampleSettings;
                AudioClipLoadType load = loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                float quality = loop ? 0.5f : 0.8f;
                if (st.loadType == load && st.compressionFormat == AudioCompressionFormat.Vorbis && Mathf.Abs(st.quality - quality) < 0.01f) continue;
                st.loadType = load;
                st.compressionFormat = AudioCompressionFormat.Vorbis;
                st.quality = quality;
                imp.defaultSampleSettings = st;
                imp.SaveAndReimport();
                changed++;
            }
            Debug.Log("CANAAN sounds: import settings set on " + changed + " clips");
        }

        static void Grass()
        {
            AssetDatabase.Refresh();
            var imp = AssetImporter.GetAtPath(GrassAtlas) as TextureImporter;
            if (imp == null) throw new Exception("grass atlas missing: run Tools/textures.sh");
            var ps = imp.GetPlatformTextureSettings("Standalone");
            if (imp.alphaIsTransparency && imp.mipMapsPreserveCoverage && imp.wrapMode == TextureWrapMode.Clamp && ps.overridden && ps.format == TextureImporterFormat.BC7
                && Mathf.Approximately(imp.alphaTestReferenceValue, GrassCutoff) && imp.anisoLevel == 4) return;
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = true;
            imp.alphaSource = TextureImporterAlphaSource.FromInput;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = true;
            imp.mipMapsPreserveCoverage = true;
            imp.alphaTestReferenceValue = GrassCutoff;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.anisoLevel = 4;
            imp.isReadable = false;
            imp.maxTextureSize = 2048;
            ps.overridden = true;
            ps.maxTextureSize = 2048;
            ps.format = TextureImporterFormat.BC7;
            imp.SetPlatformTextureSettings(ps);
            imp.SaveAndReimport();
        }

        /// <summary>Import settings for the generated textures, the two surface arrays and the terrain layers.</summary>
        static void Surfaces()
        {
            AssetDatabase.Refresh();
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/Textures/Surf" }))
                ConfigureTexture(AssetDatabase.GUIDToAssetPath(guid));
            BuildArray("_c", false, Root + "/Resources/Surf/SurfColor.asset");
            BuildArray("_n", true, Root + "/Resources/Surf/SurfDetail.asset");
            TerrainLayerAsset("t_grass", 6f);
            TerrainLayerAsset("t_road", 5f);
            TerrainLayerAsset("t_rock", 10f);
            TerrainLayerAsset("t_scrub", 6f);
        }

        static void ConfigureTexture(string path)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            string name = Path.GetFileNameWithoutExtension(path);
            bool colour = name.EndsWith("_c"), terrain = name.StartsWith("t_");
            // Terrain normals are imported as plain linear data (x in red, y in green, alpha 1), not as "normal maps":
            // the normal-map import path gave the terrain shader normals that leaned east, whatever the format.
            var type = TextureImporterType.Default;
            // Normal maps keep Unity's automatic format: the platform decides how the two channels are laid out, and
            // the stock shaders (terrain) decode accordingly. Forcing BC5 here gave normals leaning east.
            bool auto = false;
            var format = TextureImporterFormat.BC7;
            var ps = imp.GetPlatformTextureSettings("Standalone");
            bool same = imp.textureType == type && (type != TextureImporterType.Default || imp.sRGBTexture == colour) && imp.isReadable == !terrain
                        && imp.mipmapEnabled && imp.anisoLevel == 8 && imp.wrapMode == TextureWrapMode.Repeat
                        && (auto ? !ps.overridden : ps.overridden && ps.format == format) && imp.ignorePngGamma;
            if (same) return;
            imp.textureType = type;
            if (type == TextureImporterType.Default) imp.sRGBTexture = colour;
            imp.isReadable = !terrain;                     // array slices are copied on the CPU
            imp.mipmapEnabled = true;
            imp.anisoLevel = 8;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.alphaSource = TextureImporterAlphaSource.FromInput;
            imp.alphaIsTransparency = false;
            // the generator marks linear files with a gamma-1.0 chunk; Unity would "correct" it and push every value
            // up (0.5 -> 0.73): normals leaning one way, occlusion washed out
            imp.ignorePngGamma = true;
            imp.maxTextureSize = 1024;
            ps.overridden = !auto;
            ps.maxTextureSize = 1024;
            ps.format = auto ? TextureImporterFormat.Automatic : format;
            imp.SetPlatformTextureSettings(ps);
            imp.SaveAndReimport();
        }

        /// <summary>Stacks the surface textures (in SurfaceTable order) into one compressed Texture2DArray.</summary>
        static void BuildArray(string suffix, bool linear, string outPath)
        {
            Texture2DArray array = null;
            for (int i = 0; i < SurfaceTable.Count; i++)
            {
                string path = TexDir + SurfaceTable.Names[i] + suffix + ".png";
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null) throw new Exception("surface texture missing: " + path + " — run Tools/textures.sh");
                if (array == null)
                    array = new Texture2DArray(tex.width, tex.height, SurfaceTable.Count, tex.format, tex.mipmapCount, linear)
                    { wrapMode = TextureWrapMode.Repeat, anisoLevel = 8, filterMode = FilterMode.Trilinear };
                if (tex.width != array.width || tex.format != array.format) throw new Exception("surface texture differs in size or format: " + path);
                for (int m = 0; m < tex.mipmapCount; m++) array.SetPixelData(tex.GetPixelData<byte>(m), m, i);
            }
            array.Apply(false, false);
            AssetDatabase.DeleteAsset(outPath);
            AssetDatabase.CreateAsset(array, outPath);
            Debug.Log("CANAAN_SETUP built " + outPath + " " + array.width + "px x" + array.depth + " " + array.format);
        }

        static void TerrainLayerAsset(string name, float tile)
        {
            string path = Root + "/Resources/Terrain/" + name + ".terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, path);
            }
            layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + name + "_c.png");
            layer.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + name + "_n.png");
            if (layer.diffuseTexture == null || layer.normalMapTexture == null) throw new Exception("terrain texture missing: " + name);
            layer.tileSize = new Vector2(tile, tile);
            layer.normalScale = 1f;
            layer.smoothness = 1f;                         // the diffuse alpha is the smoothness
            layer.metallic = 0f;
            EditorUtility.SetDirty(layer);
        }

        static void SurfaceBase(Material m, float cull, bool triplanar)
        {
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Cull", cull);
            if (triplanar) { m.EnableKeyword("_TRIPLANAR"); m.DisableKeyword("_NORMALMAP"); }
            else { m.DisableKeyword("_TRIPLANAR"); m.EnableKeyword("_NORMALMAP"); }
            m.enableInstancing = true;
        }

        static Material Mat(string name, string shaderName, Action<Material> configure)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new Exception("shader not found: " + shaderName);
            string path = Root + "/Resources/Mat/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else m.shader = shader;
            configure?.Invoke(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void LitBase(Material m, float cull)
        {
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Cull", cull);
            m.EnableKeyword("_SPECULAR_COLOR");
            m.enableInstancing = true;
        }

        static void Materials()
        {
            Mat("Lit", "Canaan/Lit", m => LitBase(m, 2f));
            Mat("LitDouble", "Canaan/Lit", m => LitBase(m, 0f));
            Mat("LitWind", "Canaan/Lit", m => { LitBase(m, 0f); m.EnableKeyword("_WIND"); });
            Mat("LitGrass", "Canaan/Lit", m =>
            {
                // tufts cut out of the atlas, both faces drawn
                LitBase(m, 0f);
                m.EnableKeyword("_GRASS");
                m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(GrassAtlas));
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", GrassCutoff);
                m.EnableKeyword("_ALPHATEST_ON");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            });
            Mat("LitEmissive", "Canaan/Lit", m =>
            {
                LitBase(m, 2f);
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            });
            Mat("LitTransparent", "Canaan/Lit", m =>
            {
                LitBase(m, 2f);
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)RenderQueue.Transparent;
                m.SetShaderPassEnabled("ShadowCaster", false);
                m.SetShaderPassEnabled("DepthOnly", false);
            });
            Mat("Surface", "Canaan/Surface", m => SurfaceBase(m, 2f, true));
            Mat("SurfaceDouble", "Canaan/Surface", m => SurfaceBase(m, 0f, true));
            Mat("SurfaceUV", "Canaan/Surface", m => SurfaceBase(m, 2f, false));
            Mat("SurfaceUVDouble", "Canaan/Surface", m => SurfaceBase(m, 0f, false));
            Mat("SurfaceUVWind", "Canaan/Surface", m => { SurfaceBase(m, 2f, false); m.EnableKeyword("_WIND"); });
            Mat("SurfaceCliff", "Canaan/Surface", m => { SurfaceBase(m, 2f, true); m.EnableKeyword("_STRATA"); });
            Mat("SurfaceEmissive", "Canaan/Surface", m =>
            {
                SurfaceBase(m, 2f, true);
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            });
            Mat("SurfaceTransparent", "Canaan/Surface", m =>
            {
                SurfaceBase(m, 2f, true);
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)RenderQueue.Transparent;
                m.SetShaderPassEnabled("ShadowCaster", false);
                m.SetShaderPassEnabled("DepthOnly", false);
            });
            Mat("Sky", "Canaan/Sky", null);
            Mat("Water", "Canaan/Water", null);
            Mat("ParticleAlpha", "Canaan/Particle", m =>
            {
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.enableInstancing = true;
            });
            Mat("ParticleAdd", "Canaan/Particle", m =>
            {
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.One);
                m.SetFloat("_FogAmount", 0f);
                m.enableInstancing = true;
            });
            Mat("Terrain", "Universal Render Pipeline/Terrain/Lit", m =>
            {
                // Plain (not instanced) patches with vertex normals. The instanced per-pixel-normal path lights the
                // ground as if it leaned west on this platform; layer normal maps work here when x is in alpha
                // and y in green (see make_textures.py).
                m.enableInstancing = true;
                m.EnableKeyword("_NORMALMAP");
                m.DisableKeyword("_TERRAIN_INSTANCED_PERPIXEL_NORMAL");
                m.SetFloat("_EnableInstancedPerPixelNormal", 0f);
            });
            Mat("Text3D", "Canaan/Text3D", null);
            Mat("Atmosphere", "Canaan/Atmosphere", null);
            Mat("CloudPass", "Hidden/Canaan/CloudPass", null);
            Mat("GroundMark", "Canaan/GroundMark", m => m.renderQueue = 2460);
        }

        static void Scene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, BootScene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(BootScene, true) };
        }
    }
}
