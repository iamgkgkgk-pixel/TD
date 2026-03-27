// ============================================================
// 文件名：MapBlendShader.shader
// 功能描述：地图双纹理混合Shader — 草地与路径的自然过渡
//   通过BlendMask纹理控制两种地形纹理的混合比例
//   mask.r = 0 → 草地纹理, mask.r = 1 → 路径纹理
//   使用Stochastic Tiling技术消除纹理平铺接缝
//   （将UV空间划分为三角网格，每个区域随机偏移纹理，边界平滑混合）
// 创建时间：2026-03-25
// 所属模块：Battle/Map
// 兼容性：WebGL / 微信小游戏（仅使用基础纹理采样+lerp）
// ============================================================

Shader "AetheraSurvivors/MapBlend"
{
    Properties
    {
        // 草地纹理（铺满底层）
        _GrassTex ("Grass Texture", 2D) = "green" {}
        // 路径纹理（路径区域）
        _PathTex ("Path Texture", 2D) = "white" {}
        // 混合遮罩（R通道：0=草地, 1=路径, 中间=过渡）
        _BlendMask ("Blend Mask", 2D) = "black" {}
        // 纹理平铺系数（控制草地/路径纹理的重复密度）
        _TileScale ("Tile Scale", Float) = 1.0
        // 过渡边缘的平滑范围（越大过渡越宽）
        _BlendSoftness ("Blend Softness", Range(0.01, 0.5)) = 0.15
        // Perlin噪声强度（让过渡边缘不规则，更自然）
        _NoiseStrength ("Noise Strength", Range(0.0, 0.3)) = 0.08
        // 噪声缩放
        _NoiseScale ("Noise Scale", Float) = 10.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // WebGL兼容：不使用geometry/tessellation/compute shader
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            // ========== 顶点输入/输出结构 ==========

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;       // 原始UV（用于BlendMask采样）
                float2 tileUV : TEXCOORD1;    // 平铺UV（用于纹理采样）
                UNITY_FOG_COORDS(2)
                float4 vertex : SV_POSITION;
            };

            // ========== Shader属性 ==========

            sampler2D _GrassTex;
            float4 _GrassTex_ST;
            sampler2D _PathTex;
            float4 _PathTex_ST;
            sampler2D _BlendMask;
            float4 _BlendMask_ST;
            float _TileScale;
            float _BlendSoftness;
            float _NoiseStrength;
            float _NoiseScale;

            // ========== 哈希与噪声函数（WebGL兼容） ==========

            // 伪随机哈希 — 输入float2，输出float
            float hash1(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // 伪随机哈希 — 输入float2，输出float2（用于随机UV偏移）
            float2 hash2(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac(float2((p3.x + p3.y) * p3.z, (p3.x + p3.z) * p3.y));
            }

            // Value Noise（简单值噪声，用于过渡边缘扰动）
            float valueNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);

                float a = hash1(i);
                float b = hash1(i + float2(1.0, 0.0));
                float c = hash1(i + float2(0.0, 1.0));
                float d = hash1(i + float2(1.0, 1.0));

                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // ========== Stochastic Tiling 核心 ==========
            // 基于三角网格的随机化平铺采样
            // 原理：将UV空间划分为三角形网格，每个三角形顶点对纹理做随机偏移
            //       在三角形内部用重心坐标混合三个顶点的采样结果
            //       这样相邻区域的纹理不再对齐，接缝消失

            // 对纹理进行Stochastic采样（消除平铺接缝）
            // 改进版：使用更平滑的权重曲线 + 对比度保持，避免三角形边界硬接缝
            fixed4 stochasticSample(sampler2D tex, float2 uv)
            {
                // 将UV空间变换到倾斜网格（simplex grid / 三角网格）
                float2 skewedUV = uv;
                const float2x2 toSkewed = float2x2(1.0, 0.0, -0.57735027, 1.15470054);
                float2 skewed = mul(toSkewed, skewedUV);

                // 找到所在三角形的基础顶点
                float2 baseVertex = floor(skewed);
                float2 frac_uv = frac(skewed);

                // 判断在三角形的哪一半（上三角 or 下三角）
                float3 weights;
                float2 vertex1, vertex2, vertex3;

                if (frac_uv.x > frac_uv.y)
                {
                    weights = float3(1.0 - frac_uv.x, frac_uv.x - frac_uv.y, frac_uv.y);
                    vertex1 = baseVertex;
                    vertex2 = baseVertex + float2(1.0, 0.0);
                    vertex3 = baseVertex + float2(1.0, 1.0);
                }
                else
                {
                    weights = float3(1.0 - frac_uv.y, frac_uv.y - frac_uv.x, frac_uv.x);
                    vertex1 = baseVertex;
                    vertex2 = baseVertex + float2(0.0, 1.0);
                    vertex3 = baseVertex + float2(1.0, 1.0);
                }

                // 为每个顶点生成随机UV偏移（同时随机旋转，增加变化性）
                float2 offset1 = hash2(vertex1);
                float2 offset2 = hash2(vertex2);
                float2 offset3 = hash2(vertex3);

                // 用偏移后的UV分别采样纹理
                fixed4 sample1 = tex2D(tex, uv + offset1);
                fixed4 sample2 = tex2D(tex, uv + offset2);
                fixed4 sample3 = tex2D(tex, uv + offset3);

                // 使用更平滑的权重曲线（指数3.0而非7.0，减少边界硬切换）
                // 同时使用smoothstep让权重过渡更柔和
                float3 smoothWeights = float3(
                    smoothstep(0.0, 1.0, weights.x),
                    smoothstep(0.0, 1.0, weights.y),
                    smoothstep(0.0, 1.0, weights.z)
                );
                float3 expWeights = float3(
                    pow(smoothWeights.x, 3.0),
                    pow(smoothWeights.y, 3.0),
                    pow(smoothWeights.z, 3.0)
                );
                float weightSum = expWeights.x + expWeights.y + expWeights.z + 0.0001;
                expWeights /= weightSum;

                // 加权混合三个采样结果
                fixed4 blended = sample1 * expWeights.x + sample2 * expWeights.y + sample3 * expWeights.z;

                // === 对比度保持（Histogram Preserving Blending 简化版）===
                // 混合会降低对比度（变灰），通过与最大权重样本做lerp来恢复
                // 找到权重最大的样本
                fixed4 dominant = sample1;
                float maxW = expWeights.x;
                if (expWeights.y > maxW) { dominant = sample2; maxW = expWeights.y; }
                if (expWeights.z > maxW) { dominant = sample3; maxW = expWeights.z; }

                // 当最大权重接近1时完全使用dominant，权重分散时使用blended
                // 这样在三角形中心（权重均匀）保持混合，在边缘（权重集中）保持清晰
                float contrastPreserve = saturate(maxW * 1.5 - 0.3);
                fixed4 result = lerp(blended, dominant, contrastPreserve * 0.3);

                return result;
            }


            // ========== 顶点着色器 ==========

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);

                // BlendMask使用原始UV（整个quad映射到整张mask）
                o.uv = TRANSFORM_TEX(v.uv, _BlendMask);

                // 纹理使用平铺UV（让草地/路径纹理重复平铺）
                o.tileUV = v.uv * _TileScale;

                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }

            // ========== 片元着色器 ==========

            fixed4 frag(v2f i) : SV_Target
            {
                // 使用Stochastic Tiling采样两种地形纹理（消除平铺接缝）
                fixed4 grassColor = stochasticSample(_GrassTex, i.tileUV);
                fixed4 pathColor = stochasticSample(_PathTex, i.tileUV);

                // 采样混合遮罩（使用原始UV，一一对应地图格子）
                fixed mask = tex2D(_BlendMask, i.uv).r;

                // 添加噪声扰动（让过渡边缘不规则，更自然）
                float noise = valueNoise(i.uv * _NoiseScale) * _NoiseStrength;
                mask = saturate(mask + noise - _NoiseStrength * 0.5);

                // 使用smoothstep做平滑过渡（避免硬边）
                float blendFactor = smoothstep(0.5 - _BlendSoftness, 0.5 + _BlendSoftness, mask);

                // 混合两种纹理
                fixed4 finalColor = lerp(grassColor, pathColor, blendFactor);

                UNITY_APPLY_FOG(i.fogCoord, finalColor);
                return finalColor;
            }
            ENDCG
        }
    }

    // WebGL回退：如果上面的Pass不支持，回退到最基础的Shader
    FallBack "Sprites/Default"
}

