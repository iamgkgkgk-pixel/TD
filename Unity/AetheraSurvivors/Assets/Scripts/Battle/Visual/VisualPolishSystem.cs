// ============================================================
// 文件名：VisualPolishSystem.cs
// 功能描述：画面增强系统 — 运行时修复纹理质量、添加后处理效果
//          解决锯齿、画面平淡、缺乏氛围感等问题
//          纯代码实现，不依赖URP/PostProcessing包
// 创建时间：2026-03-26
// 所属模块：Battle/Visual
// ============================================================

using UnityEngine;
using AetheraSurvivors.Framework;
using Logger = AetheraSurvivors.Framework.Logger;

namespace AetheraSurvivors.Battle.Visual
{
    /// <summary>
    /// 画面增强系统 — 全局视觉质量提升
    /// 
    /// 功能：
    /// 1. 运行时纹理质量修复（自动将所有Point过滤改为Bilinear/Trilinear）
    /// 2. 相机后处理效果（暗角Vignette + 色彩增强 + 轻微Bloom）
    /// 3. 环境光照模拟（动态色温变化）
    /// 4. 全局抗锯齿设置
    /// </summary>
    public class VisualPolishSystem : MonoSingleton<VisualPolishSystem>
    {
        // ========== 后处理材质 ==========

        /// <summary>暗角+色彩增强材质</summary>
        private Material _postProcessMat;

        /// <summary>Bloom提取材质</summary>
        private Material _bloomExtractMat;

        /// <summary>Bloom模糊材质</summary>
        private Material _bloomBlurMat;

        /// <summary>Bloom合成材质</summary>
        private Material _bloomCompositeMat;

        // ========== 配置参数 ==========

        [Header("暗角效果")]
        [SerializeField] private float _vignetteIntensity = 0.05f;
        [SerializeField] private float _vignetteSmoothness = 0.2f;



        [Header("色彩增强")]
        [SerializeField] private float _saturationBoost = 1.15f;
        [SerializeField] private float _contrastBoost = 1.08f;
        [SerializeField] private float _brightnessBoost = 1.02f;

        [Header("Bloom光晕")]
        [SerializeField] private float _bloomThreshold = 0.75f;
        [SerializeField] private float _bloomIntensity = 0.3f;
        [SerializeField] private int _bloomIterations = 3;

        [Header("环境色温")]
        [SerializeField] private Color _warmTint = new Color(1f, 0.97f, 0.92f, 1f);

        // ========== 运行时数据 ==========

        private Camera _camera;
        private bool _postProcessReady = false;
        private RenderTexture _bloomRT1;
        private RenderTexture _bloomRT2;

        // ========== 初始化 ==========

        protected override void OnInit()
        {
            _camera = Camera.main;
            if (_camera == null) _camera = FindObjectOfType<Camera>();

            // 1. 全局纹理质量修复
            FixGlobalTextureQuality();

            // 2. 全局抗锯齿
            SetupAntiAliasing();

            // 3. 初始化后处理Shader
            InitPostProcessing();

            // 4. 修复场景中所有SpriteRenderer的纹理
            FixAllSpriteRenderers();

            Logger.I("VisualPolish", "画面增强系统初始化完成");
        }

        protected override void OnDispose()
        {
            CleanupRenderTextures();
            if (_postProcessMat != null) Destroy(_postProcessMat);
            if (_bloomExtractMat != null) Destroy(_bloomExtractMat);
            if (_bloomBlurMat != null) Destroy(_bloomBlurMat);
            if (_bloomCompositeMat != null) Destroy(_bloomCompositeMat);
        }

        // ========== 1. 全局纹理质量修复 ==========

        /// <summary>
        /// 设置全局纹理质量参数
        /// </summary>
        private void FixGlobalTextureQuality()
        {
            // 全局各向异性过滤
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;

            // 全局纹理质量（0=Full, 1=Half, 2=Quarter）
            QualitySettings.globalTextureMipmapLimit = 0;

            Logger.I("VisualPolish", "全局纹理质量已设置: aniso=ForceEnable, mipmapLimit=0");
        }

        // ========== 2. 全局抗锯齿 ==========

        /// <summary>
        /// 设置抗锯齿（MSAA）
        /// </summary>
        private void SetupAntiAliasing()
        {
            // 2D游戏使用4x MSAA，有效消除Sprite边缘锯齿
            QualitySettings.antiAliasing = 4;

            Logger.I("VisualPolish", "抗锯齿已设置: MSAA 4x");

        }

        // ========== 3. 后处理效果 ==========

        /// <summary>
        /// 初始化后处理材质（使用内置Shader实现）
        /// </summary>
        private void InitPostProcessing()
        {
            // === 主后处理Shader（暗角 + 色彩增强 + 色温） ===
            string postProcessShaderCode = @"
Shader ""Hidden/VisualPolish_PostProcess""
{
    Properties
    {
        _MainTex (""Texture"", 2D) = ""white"" {}
        _VignetteIntensity (""Vignette Intensity"", Float) = 0.05
        _VignetteSmoothness (""Vignette Smoothness"", Float) = 0.2

        _Saturation (""Saturation"", Float) = 1.15
        _Contrast (""Contrast"", Float) = 1.08
        _Brightness (""Brightness"", Float) = 1.02
        _WarmTint (""Warm Tint"", Color) = (1, 0.97, 0.92, 1)
    }
    SubShader
    {
        Tags { ""RenderType""=""Opaque"" }
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include ""UnityCG.cginc""

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float2 uv : TEXCOORD0; float4 vertex : SV_POSITION; };

            sampler2D _MainTex;
            float _VignetteIntensity;
            float _VignetteSmoothness;
            float _Saturation;
            float _Contrast;
            float _Brightness;
            float4 _WarmTint;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);

                // 色温调整
                col.rgb *= _WarmTint.rgb;

                // 亮度
                col.rgb *= _Brightness;

                // 对比度（以0.5为中心）
                col.rgb = (col.rgb - 0.5) * _Contrast + 0.5;

                // 饱和度
                float luma = dot(col.rgb, float3(0.2126, 0.7152, 0.0722));
                col.rgb = lerp(float3(luma, luma, luma), col.rgb, _Saturation);

                // 暗角
                float2 uv = i.uv - 0.5;
                float dist = length(uv);
                float vignette = smoothstep(0.5, 0.5 - _VignetteSmoothness, dist * (1.0 + _VignetteIntensity));
                col.rgb *= vignette;

                return col;
            }
            ENDCG
        }
    }
}";

            // === Bloom提取Shader ===
            string bloomExtractShaderCode = @"
Shader ""Hidden/VisualPolish_BloomExtract""
{
    Properties
    {
        _MainTex (""Texture"", 2D) = ""white"" {}
        _Threshold (""Threshold"", Float) = 0.75
    }
    SubShader
    {
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include ""UnityCG.cginc""

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float2 uv : TEXCOORD0; float4 vertex : SV_POSITION; };

            sampler2D _MainTex;
            float _Threshold;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                float brightness = max(col.r, max(col.g, col.b));
                float contribution = max(0, brightness - _Threshold);
                contribution /= max(brightness, 0.001);
                return col * contribution;
            }
            ENDCG
        }
    }
}";

            // === Bloom模糊Shader（高斯模糊） ===
            string bloomBlurShaderCode = @"
Shader ""Hidden/VisualPolish_BloomBlur""
{
    Properties
    {
        _MainTex (""Texture"", 2D) = ""white"" {}
        _BlurSize (""Blur Size"", Float) = 1.0
    }
    SubShader
    {
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include ""UnityCG.cginc""

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float2 uv : TEXCOORD0; float4 vertex : SV_POSITION; };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _BlurSize;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 texelSize = _MainTex_TexelSize.xy * _BlurSize;
                fixed4 col = fixed4(0,0,0,0);

                // 9-tap高斯模糊
                col += tex2D(_MainTex, i.uv + float2(-4, -4) * texelSize) * 0.01621622;
                col += tex2D(_MainTex, i.uv + float2(-3, -3) * texelSize) * 0.05405405;
                col += tex2D(_MainTex, i.uv + float2(-2, -2) * texelSize) * 0.12162162;
                col += tex2D(_MainTex, i.uv + float2(-1, -1) * texelSize) * 0.19459459;
                col += tex2D(_MainTex, i.uv) * 0.22702703;
                col += tex2D(_MainTex, i.uv + float2(1, 1) * texelSize) * 0.19459459;
                col += tex2D(_MainTex, i.uv + float2(2, 2) * texelSize) * 0.12162162;
                col += tex2D(_MainTex, i.uv + float2(3, 3) * texelSize) * 0.05405405;
                col += tex2D(_MainTex, i.uv + float2(4, 4) * texelSize) * 0.01621622;

                return col;
            }
            ENDCG
        }
    }
}";

            // === Bloom合成Shader ===
            string bloomCompositeShaderCode = @"
Shader ""Hidden/VisualPolish_BloomComposite""
{
    Properties
    {
        _MainTex (""Texture"", 2D) = ""white"" {}
        _BloomTex (""Bloom"", 2D) = ""black"" {}
        _BloomIntensity (""Bloom Intensity"", Float) = 0.3
    }
    SubShader
    {
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include ""UnityCG.cginc""

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float2 uv : TEXCOORD0; float4 vertex : SV_POSITION; };

            sampler2D _MainTex;
            sampler2D _BloomTex;
            float _BloomIntensity;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                fixed4 bloom = tex2D(_BloomTex, i.uv);
                col.rgb += bloom.rgb * _BloomIntensity;
                return col;
            }
            ENDCG
        }
    }
}";

            // 尝试从代码创建Shader（Unity运行时不支持从字符串编译Shader）
            // 所以我们使用Shader.Find查找内置Shader，或者用简化的OnRenderImage方案
            // 对于WebGL平台，使用更轻量的方案

            // 尝试查找已有Shader
            Shader postShader = Shader.Find("Hidden/VisualPolish_PostProcess");
            Shader bloomExtractShader = Shader.Find("Hidden/VisualPolish_BloomExtract");
            Shader bloomBlurShader = Shader.Find("Hidden/VisualPolish_BloomBlur");
            Shader bloomCompositeShader = Shader.Find("Hidden/VisualPolish_BloomComposite");

            if (postShader != null)
            {
                _postProcessMat = new Material(postShader);
                _postProcessReady = true;
            }

            if (bloomExtractShader != null)
                _bloomExtractMat = new Material(bloomExtractShader);
            if (bloomBlurShader != null)
                _bloomBlurMat = new Material(bloomBlurShader);
            if (bloomCompositeShader != null)
                _bloomCompositeMat = new Material(bloomCompositeShader);

            // 如果Shader不存在（首次运行），使用SpriteRenderer叠加层方案作为替代
            if (!_postProcessReady)
            {
                Logger.I("VisualPolish", "后处理Shader未找到，使用SpriteRenderer叠加层替代方案");
                CreateOverlayEffects();
            }
            else
            {
                Logger.I("VisualPolish", "后处理Shader已就绪");
            }
        }

        // ========== 替代方案：SpriteRenderer叠加层 ==========

        /// <summary>暗角叠加层对象</summary>
        private GameObject _vignetteOverlay;

        /// <summary>
        /// 当Shader不可用时，使用SpriteRenderer叠加层实现暗角效果
        /// </summary>
        private void CreateOverlayEffects()
        {
            if (_camera == null) return;

            // === 暗角叠加层 ===
            CreateVignetteOverlay();

            // === 环境光色温叠加 ===
            CreateWarmTintOverlay();
        }

        /// <summary>创建暗角叠加层</summary>
        private void CreateVignetteOverlay()
        {
            _vignetteOverlay = new GameObject("VignetteOverlay");
            _vignetteOverlay.transform.SetParent(_camera.transform);
            _vignetteOverlay.transform.localPosition = new Vector3(0, 0, 1f);

            var sr = _vignetteOverlay.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 999; // 最顶层

            // 创建径向渐变纹理（中心透明，边缘黑色）
            int texSize = 256;
            var tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[texSize * texSize];
            Vector2 center = new Vector2(texSize / 2f, texSize / 2f);
            float maxDist = texSize / 2f;

            for (int y = 0; y < texSize; y++)
            {
                for (int x = 0; x < texSize; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                    // 暗角曲线：中心完全透明，仅最边缘轻微变暗
                    float alpha = Mathf.Pow(Mathf.Clamp01(dist - 0.55f) / 0.45f, 2f) * _vignetteIntensity;

                    pixels[y * texSize + x] = new Color(0f, 0f, 0f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            sr.sprite = Sprite.Create(tex, new Rect(0, 0, texSize, texSize), new Vector2(0.5f, 0.5f), 1f);

            // 缩放以覆盖整个视口
            UpdateOverlayScale();

            sr.material = new Material(Shader.Find("Sprites/Default"));
            sr.color = Color.white;
        }

        /// <summary>色温叠加层</summary>
        private GameObject _warmTintOverlay;

        /// <summary>创建暖色调叠加层</summary>
        private void CreateWarmTintOverlay()
        {
            _warmTintOverlay = new GameObject("WarmTintOverlay");
            _warmTintOverlay.transform.SetParent(_camera.transform);
            _warmTintOverlay.transform.localPosition = new Vector3(0, 0, 0.9f);

            var sr = _warmTintOverlay.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 998;

            // 创建纯色纹理
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;

            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

            // 非常淡的暖色叠加（Multiply混合模式效果）
            // 使用极低透明度的暖色
            sr.color = new Color(1f, 0.98f, 0.93f, 0.06f);

            // 使用Additive混合的Shader让效果更自然
            var mat = new Material(Shader.Find("Sprites/Default"));
            sr.material = mat;

            UpdateOverlayScale();
        }

        /// <summary>更新叠加层缩放以匹配相机视口</summary>
        private void UpdateOverlayScale()
        {
            if (_camera == null) return;

            float camHeight = _camera.orthographicSize * 2f;
            float camWidth = camHeight * _camera.aspect;

            if (_vignetteOverlay != null)
            {
                // 暗角纹理是256x256像素，PPU=1，所以原始大小是256x256世界单位
                // 需要缩放到相机视口大小
                float vignetteScale = Mathf.Max(camWidth, camHeight) / 256f * 1.2f;
                _vignetteOverlay.transform.localScale = new Vector3(vignetteScale, vignetteScale, 1f);
            }

            if (_warmTintOverlay != null)
            {
                _warmTintOverlay.transform.localScale = new Vector3(camWidth * 1.1f, camHeight * 1.1f, 1f);
            }
        }

        // ========== 4. 修复所有SpriteRenderer ==========

        /// <summary>
        /// 遍历场景中所有SpriteRenderer，修复纹理过滤模式
        /// </summary>
        private void FixAllSpriteRenderers()
        {
            int fixedCount = 0;
            var renderers = FindObjectsOfType<SpriteRenderer>();

            foreach (var sr in renderers)
            {
                if (sr.sprite == null) continue;

                var tex = sr.sprite.texture;
                if (tex == null) continue;

                // 修复过滤模式（像素风除外），优先使用Trilinear消除锯齿
                if (tex.width > 4)
                {
                    if (tex.filterMode != FilterMode.Trilinear)
                    {
                        tex.filterMode = FilterMode.Trilinear;
                        fixedCount++;
                    }
                }

                // 提高各向异性过滤等级
                if (tex.anisoLevel < 8)
                {
                    tex.anisoLevel = 8;
                }

            }

            if (fixedCount > 0)
            {
                Logger.I("VisualPolish", "已修复 {0} 个SpriteRenderer的纹理过滤模式", fixedCount);
            }
        }

        // ========== 后处理渲染 ==========

        /// <summary>
        /// OnRenderImage — 相机渲染完成后应用后处理
        /// </summary>
        private void OnRenderImage(RenderTexture src, RenderTexture dest)
        {
            if (!_postProcessReady || _postProcessMat == null)
            {
                Graphics.Blit(src, dest);
                return;
            }

            // 设置后处理参数
            _postProcessMat.SetFloat("_VignetteIntensity", _vignetteIntensity);
            _postProcessMat.SetFloat("_VignetteSmoothness", _vignetteSmoothness);
            _postProcessMat.SetFloat("_Saturation", _saturationBoost);
            _postProcessMat.SetFloat("_Contrast", _contrastBoost);
            _postProcessMat.SetFloat("_Brightness", _brightnessBoost);
            _postProcessMat.SetColor("_WarmTint", _warmTint);

            // 如果有Bloom
            if (_bloomExtractMat != null && _bloomBlurMat != null && _bloomCompositeMat != null)
            {
                int w = src.width / 2;
                int h = src.height / 2;

                EnsureRenderTexture(ref _bloomRT1, w, h);
                EnsureRenderTexture(ref _bloomRT2, w, h);

                // 提取高亮
                _bloomExtractMat.SetFloat("_Threshold", _bloomThreshold);
                Graphics.Blit(src, _bloomRT1, _bloomExtractMat);

                // 多次模糊
                for (int i = 0; i < _bloomIterations; i++)
                {
                    _bloomBlurMat.SetFloat("_BlurSize", 1f + i);
                    Graphics.Blit(_bloomRT1, _bloomRT2, _bloomBlurMat);
                    Graphics.Blit(_bloomRT2, _bloomRT1, _bloomBlurMat);
                }

                // 合成Bloom到原图
                _bloomCompositeMat.SetTexture("_BloomTex", _bloomRT1);
                _bloomCompositeMat.SetFloat("_BloomIntensity", _bloomIntensity);

                var tempRT = RenderTexture.GetTemporary(src.width, src.height);
                Graphics.Blit(src, tempRT, _bloomCompositeMat);

                // 最终后处理
                Graphics.Blit(tempRT, dest, _postProcessMat);
                RenderTexture.ReleaseTemporary(tempRT);
            }
            else
            {
                // 只有基础后处理
                Graphics.Blit(src, dest, _postProcessMat);
            }
        }

        // ========== 工具方法 ==========

        private void EnsureRenderTexture(ref RenderTexture rt, int w, int h)
        {
            if (rt != null && (rt.width != w || rt.height != h))
            {
                rt.Release();
                rt = null;
            }

            if (rt == null)
            {
                rt = new RenderTexture(w, h, 0, RenderTextureFormat.Default);
                rt.filterMode = FilterMode.Bilinear;
            }
        }

        private void CleanupRenderTextures()
        {
            if (_bloomRT1 != null) { _bloomRT1.Release(); _bloomRT1 = null; }
            if (_bloomRT2 != null) { _bloomRT2.Release(); _bloomRT2 = null; }
        }

        // ========== Update ==========

        private void LateUpdate()
        {
            // 持续更新叠加层缩放（相机可能缩放）
            if (!_postProcessReady)
            {
                UpdateOverlayScale();
            }

            // 每隔一段时间修复新生成的SpriteRenderer
            _fixTimer -= Time.deltaTime;
            if (_fixTimer <= 0f)
            {
                _fixTimer = FixInterval;
                FixAllSpriteRenderers();
            }
        }

        private float _fixTimer = 0f;
        private const float FixInterval = 3f; // 每3秒检查一次新生成的Sprite

        // ========== 公共API ==========

        /// <summary>
        /// 设置暗角强度（0=无暗角，1=最强）
        /// </summary>
        public void SetVignetteIntensity(float intensity)
        {
            _vignetteIntensity = Mathf.Clamp01(intensity);
        }

        /// <summary>
        /// 设置饱和度增强（1=原始，>1=更鲜艳）
        /// </summary>
        public void SetSaturation(float saturation)
        {
            _saturationBoost = Mathf.Clamp(saturation, 0.5f, 2f);
        }

        /// <summary>
        /// 设置Bloom强度
        /// </summary>
        public void SetBloomIntensity(float intensity)
        {
            _bloomIntensity = Mathf.Clamp(intensity, 0f, 1f);
        }
    }
}
