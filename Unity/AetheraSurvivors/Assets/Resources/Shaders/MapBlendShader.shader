// ============================================================
// 文件名：MapBlendShader.shader
// 功能描述：地图多纹理混合Shader — 草地/路径/岩石/花朵的自然过渡
//   通过BlendMask纹理RGBA通道控制四种地形纹理的混合比例
//   草地 = 底层（1 - R - G - B）
//   mask.r → 路径纹理
//   mask.g → 岩石纹理
//   mask.b → 花朵纹理
//   使用Stochastic Tiling技术消除纹理平铺接缝
// 创建时间：2026-03-25
// 所属模块：Battle/Map
// 兼容性：WebGL / 微信小游戏（仅使用基础纹理采样+lerp）
// ============================================================

Shader "AetheraSurvivors/MapBlend"
{
    Properties
    {
        // 草地纹理（底层默认）
        _GrassTex ("Grass Texture", 2D) = "green" {}
        // 路径纹理
        _PathTex ("Path Texture", 2D) = "white" {}
        // 岩石纹理
        _RockTex ("Rock Texture", 2D) = "gray" {}
        // 花朵纹理
        _FlowersTex ("Flowers Texture", 2D) = "green" {}
        // 混合遮罩（R=路径, G=岩石, B=花朵, 草地=1-R-G-B）
        _BlendMask ("Blend Mask", 2D) = "black" {}
        // 纹理平铺系数
        _TileScale ("Tile Scale", Float) = 1.0
        // 过渡边缘的平滑范围
        _BlendSoftness ("Blend Softness", Range(0.01, 0.5)) = 0.15
        // Perlin噪声强度
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
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;       // 原始UV（BlendMask采样）
                float2 tileUV : TEXCOORD1;    // 平铺UV（纹理采样）
                UNITY_FOG_COORDS(2)
                float4 vertex : SV_POSITION;
            };

            // ========== Shader属性 ==========

            sampler2D _GrassTex;
            float4 _GrassTex_ST;
            sampler2D _PathTex;
            float4 _PathTex_ST;
            sampler2D _RockTex;
            float4 _RockTex_ST;
            sampler2D _FlowersTex;
            float4 _FlowersTex_ST;
            sampler2D _BlendMask;
            float4 _BlendMask_ST;
            float _TileScale;
            float _BlendSoftness;
            float _NoiseStrength;
            float _NoiseScale;

            // ========== 哈希与噪声函数（WebGL兼容） ==========

            float hash1(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float2 hash2(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac(float2((p3.x + p3.y) * p3.z, (p3.x + p3.z) * p3.y));
            }

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

            fixed4 stochasticSample(sampler2D tex, float2 uv)
            {
                float2 skewedUV = uv;
                const float2x2 toSkewed = float2x2(1.0, 0.0, -0.57735027, 1.15470054);
                float2 skewed = mul(toSkewed, skewedUV);

                float2 baseVertex = floor(skewed);
                float2 frac_uv = frac(skewed);

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

                float2 offset1 = hash2(vertex1);
                float2 offset2 = hash2(vertex2);
                float2 offset3 = hash2(vertex3);

                fixed4 sample1 = tex2D(tex, uv + offset1);
                fixed4 sample2 = tex2D(tex, uv + offset2);
                fixed4 sample3 = tex2D(tex, uv + offset3);

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

                fixed4 blended = sample1 * expWeights.x + sample2 * expWeights.y + sample3 * expWeights.z;

                fixed4 dominant = sample1;
                float maxW = expWeights.x;
                if (expWeights.y > maxW) { dominant = sample2; maxW = expWeights.y; }
                if (expWeights.z > maxW) { dominant = sample3; maxW = expWeights.z; }

                float contrastPreserve = saturate(maxW * 1.5 - 0.3);
                fixed4 result = lerp(blended, dominant, contrastPreserve * 0.3);

                return result;
            }

            // ========== 顶点着色器 ==========

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _BlendMask);
                o.tileUV = v.uv * _TileScale;
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }

            // ========== 片元着色器 ==========

            fixed4 frag(v2f i) : SV_Target
            {
                // 采样四种地形纹理（Stochastic Tiling消除接缝）
                fixed4 grassColor = stochasticSample(_GrassTex, i.tileUV);
                fixed4 pathColor = stochasticSample(_PathTex, i.tileUV);
                fixed4 rockColor = stochasticSample(_RockTex, i.tileUV);
                fixed4 flowersColor = stochasticSample(_FlowersTex, i.tileUV);

                // 采样混合遮罩 RGBA
                fixed4 mask = tex2D(_BlendMask, i.uv);

                // 噪声扰动（让过渡边缘不规则）
                float noise = valueNoise(i.uv * _NoiseScale) * _NoiseStrength;

                // 对每个通道做 smoothstep 平滑过渡
                float pathFactor = smoothstep(0.5 - _BlendSoftness, 0.5 + _BlendSoftness,
                    saturate(mask.r + noise - _NoiseStrength * 0.5));
                float rockFactor = smoothstep(0.5 - _BlendSoftness, 0.5 + _BlendSoftness,
                    saturate(mask.g + noise - _NoiseStrength * 0.5));
                float flowersFactor = smoothstep(0.5 - _BlendSoftness, 0.5 + _BlendSoftness,
                    saturate(mask.b + noise - _NoiseStrength * 0.5));

                // 归一化权重：草地 = 剩余部分
                float totalOverlay = pathFactor + rockFactor + flowersFactor;
                // 防止总权重超过1（多种地形重叠的过渡区域）
                if (totalOverlay > 1.0)
                {
                    float invTotal = 1.0 / totalOverlay;
                    pathFactor *= invTotal;
                    rockFactor *= invTotal;
                    flowersFactor *= invTotal;
                    totalOverlay = 1.0;
                }
                float grassFactor = 1.0 - totalOverlay;

                // 四纹理加权混合
                fixed4 finalColor = grassColor * grassFactor
                                  + pathColor * pathFactor
                                  + rockColor * rockFactor
                                  + flowersColor * flowersFactor;

                UNITY_APPLY_FOG(i.fogCoord, finalColor);
                return finalColor;
            }
            ENDCG
        }
    }

    FallBack "Sprites/Default"
}
