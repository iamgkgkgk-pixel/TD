// ============================================================
// 文件名：MapBlendMaskGenerator.cs
// 功能描述：地图混合遮罩生成器 — 根据GridSystem数据生成BlendMask
//   遍历地图格子，按类型写入RGBA通道：
//     R = 路径/出生点/基地
//     G = 障碍物（岩石）
//     B = 塔位（花朵）
//     草地 = 默认底层（1 - R - G - B）
//   然后对每个通道做高斯模糊，产生自然的过渡带
// 创建时间：2026-03-25
// 所属模块：Battle/Map
// 对应交互：方案C — Shader Blend（扩展为4纹理）
// ============================================================

using UnityEngine;
using AetheraSurvivors.Framework;
using Logger = AetheraSurvivors.Framework.Logger;

namespace AetheraSurvivors.Battle.Map
{
    /// <summary>
    /// 地图混合遮罩生成器（RGBA四通道）
    /// 
    /// 职责：
    /// 1. 从GridSystem读取地图数据，生成原始mask
    ///    R=路径, G=岩石, B=花朵, 草地=默认
    /// 2. 对mask每个通道执行高斯模糊，产生平滑过渡带
    /// 3. 输出Texture2D供MapBlendShader使用
    /// </summary>
    public static class MapBlendMaskGenerator
    {
        // ========== 配置常量 ==========

        /// <summary>每格子对应像素数</summary>
        private const int PIXELS_PER_CELL = 8;

        /// <summary>高斯模糊半径</summary>
        private const int BLUR_RADIUS = 5;

        /// <summary>高斯模糊迭代次数</summary>
        private const int BLUR_ITERATIONS = 3;

        // ========== 核心方法 ==========

        /// <summary>
        /// 生成混合遮罩纹理（RGBA四通道）
        /// </summary>
        public static Texture2D GenerateBlendMask()
        {
            var grid = GridSystem.Instance;
            if (grid == null || !grid.IsMapLoaded)
            {
                Logger.W("MapBlendMask", "生成失败：地图未加载");
                return null;
            }

            int mapWidth = grid.Width;
            int mapHeight = grid.Height;
            int texWidth = mapWidth * PIXELS_PER_CELL;
            int texHeight = mapHeight * PIXELS_PER_CELL;

            Logger.I("MapBlendMask", "开始生成BlendMask(RGBA): 地图{0}×{1} → 纹理{2}×{3}",
                mapWidth, mapHeight, texWidth, texHeight);

            // 步骤1：生成原始Mask（三个通道各自独立）
            float[] rawR, rawG, rawB;
            GenerateRawMask(grid, texWidth, texHeight, out rawR, out rawG, out rawB);

            // 步骤2：对每个通道分别做高斯模糊
            float[] blurR = rawR;
            float[] blurG = rawG;
            float[] blurB = rawB;
            for (int i = 0; i < BLUR_ITERATIONS; i++)
            {
                blurR = GaussianBlur(blurR, texWidth, texHeight, BLUR_RADIUS);
                blurG = GaussianBlur(blurG, texWidth, texHeight, BLUR_RADIUS);
                blurB = GaussianBlur(blurB, texWidth, texHeight, BLUR_RADIUS);
            }

            // 步骤3：合成为RGBA Texture2D
            Texture2D maskTex = CreateMaskTexture(blurR, blurG, blurB, texWidth, texHeight);

            Logger.I("MapBlendMask", "✅ BlendMask(RGBA)生成完成: {0}×{1}", texWidth, texHeight);
            return maskTex;
        }

        /// <summary>
        /// 可自定义参数版本
        /// </summary>
        public static Texture2D GenerateBlendMask(int pixelsPerCell, int blurRadius, int blurIterations)
        {
            var grid = GridSystem.Instance;
            if (grid == null || !grid.IsMapLoaded) return null;

            int texWidth = grid.Width * pixelsPerCell;
            int texHeight = grid.Height * pixelsPerCell;

            float[] rawR, rawG, rawB;
            GenerateRawMask(grid, texWidth, texHeight, out rawR, out rawG, out rawB);

            float[] blurR = rawR;
            float[] blurG = rawG;
            float[] blurB = rawB;
            for (int i = 0; i < blurIterations; i++)
            {
                blurR = GaussianBlur(blurR, texWidth, texHeight, blurRadius);
                blurG = GaussianBlur(blurG, texWidth, texHeight, blurRadius);
                blurB = GaussianBlur(blurB, texWidth, texHeight, blurRadius);
            }

            return CreateMaskTexture(blurR, blurG, blurB, texWidth, texHeight);
        }

        // ========== 内部方法 ==========

        /// <summary>
        /// 生成三通道原始Mask数据
        /// R = 路径/出生点/基地
        /// G = 障碍物（岩石）
        /// B = 塔位（花朵）
        /// </summary>
        private static void GenerateRawMask(GridSystem grid, int texWidth, int texHeight,
            out float[] maskR, out float[] maskG, out float[] maskB)
        {
            int total = texWidth * texHeight;
            maskR = new float[total];
            maskG = new float[total];
            maskB = new float[total];

            int mapWidth = grid.Width;
            int mapHeight = grid.Height;

            for (int py = 0; py < texHeight; py++)
            {
                for (int px = 0; px < texWidth; px++)
                {
                    int cellX = Mathf.Clamp(px * mapWidth / texWidth, 0, mapWidth - 1);
                    int cellY = Mathf.Clamp(py * mapHeight / texHeight, 0, mapHeight - 1);

                    var cell = grid.GetCell(cellX, cellY);
                    int idx = py * texWidth + px;

                    switch (cell.Type)
                    {
                        case GridCellType.Path:
                        case GridCellType.SpawnPoint:
                        case GridCellType.BasePoint:
                            maskR[idx] = 1f;
                            break;
                        case GridCellType.Obstacle:
                            maskG[idx] = 1f;
                            break;
                        case GridCellType.TowerSlot:
                            maskB[idx] = 1f;
                            break;
                        default:
                            // Empty = 草地（全0，底层默认）
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// 高斯模糊（CPU端，分离式两遍）
        /// </summary>
        private static float[] GaussianBlur(float[] input, int width, int height, int radius)
        {
            float[] kernel = GenerateGaussianKernel(radius);
            int kernelSize = kernel.Length;
            int halfKernel = kernelSize / 2;

            // 水平模糊
            float[] horizontal = new float[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float sum = 0f;
                    float weightSum = 0f;

                    for (int k = 0; k < kernelSize; k++)
                    {
                        int sampleX = Mathf.Clamp(x + k - halfKernel, 0, width - 1);
                        float weight = kernel[k];
                        sum += input[y * width + sampleX] * weight;
                        weightSum += weight;
                    }

                    horizontal[y * width + x] = sum / weightSum;
                }
            }

            // 垂直模糊
            float[] result = new float[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float sum = 0f;
                    float weightSum = 0f;

                    for (int k = 0; k < kernelSize; k++)
                    {
                        int sampleY = Mathf.Clamp(y + k - halfKernel, 0, height - 1);
                        float weight = kernel[k];
                        sum += horizontal[sampleY * width + x] * weight;
                        weightSum += weight;
                    }

                    result[y * width + x] = sum / weightSum;
                }
            }

            return result;
        }

        /// <summary>
        /// 生成一维高斯核
        /// </summary>
        private static float[] GenerateGaussianKernel(int radius)
        {
            int size = radius * 2 + 1;
            float[] kernel = new float[size];
            float sigma = radius / 2.0f;
            if (sigma < 0.5f) sigma = 0.5f;

            float sum = 0f;
            for (int i = 0; i < size; i++)
            {
                float x = i - radius;
                kernel[i] = Mathf.Exp(-(x * x) / (2f * sigma * sigma));
                sum += kernel[i];
            }

            for (int i = 0; i < size; i++)
            {
                kernel[i] /= sum;
            }

            return kernel;
        }

        /// <summary>
        /// 将三个float通道合成为RGBA Texture2D
        /// </summary>
        private static Texture2D CreateMaskTexture(float[] dataR, float[] dataG, float[] dataB,
            int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color(
                    Mathf.Clamp01(dataR[i]),
                    Mathf.Clamp01(dataG[i]),
                    Mathf.Clamp01(dataB[i]),
                    1f
                );
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);

            return tex;
        }
    }
}
