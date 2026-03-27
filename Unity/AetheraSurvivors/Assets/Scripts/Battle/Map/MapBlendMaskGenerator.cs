// ============================================================
// 文件名：MapBlendMaskGenerator.cs
// 功能描述：地图混合遮罩生成器 — 根据GridSystem数据生成BlendMask
//   遍历地图格子，路径格子写白色(1)，草地格子写黑色(0)
//   然后对mask做高斯模糊，产生自然的0→1渐变过渡带
//   生成的Texture2D传给MapBlendShader作为_BlendMask
// 创建时间：2026-03-25
// 所属模块：Battle/Map
// 对应交互：方案C — Shader Blend
// ============================================================

using UnityEngine;
using AetheraSurvivors.Framework;
using Logger = AetheraSurvivors.Framework.Logger;

namespace AetheraSurvivors.Battle.Map
{
    /// <summary>
    /// 地图混合遮罩生成器
    /// 
    /// 职责：
    /// 1. 从GridSystem读取地图数据，生成原始mask（路径=白，草地=黑）
    /// 2. 对mask执行高斯模糊，产生平滑过渡带
    /// 3. 输出Texture2D供MapBlendShader使用
    /// 
    /// 生成流程：
    ///   GridSystem数据 → 原始Mask(0/1) → 高斯模糊 → 最终BlendMask
    /// </summary>
    public static class MapBlendMaskGenerator
    {
        // ========== 配置常量 ==========

        /// <summary>
        /// Mask分辨率倍率（每个格子对应多少像素）
        /// 倍率越高过渡越精细，但内存占用越大
        /// 8 = 每格8×8像素，10×10地图 → 80×80 mask → 过渡更平滑
        /// </summary>
        private const int PIXELS_PER_CELL = 8;

        /// <summary>
        /// 高斯模糊半径（像素）
        /// 控制过渡带的宽度，越大越柔和
        /// </summary>
        private const int BLUR_RADIUS = 5;

        /// <summary>
        /// 高斯模糊迭代次数
        /// 多次模糊效果更平滑
        /// </summary>
        private const int BLUR_ITERATIONS = 3;


        // ========== 核心方法 ==========

        /// <summary>
        /// 生成混合遮罩纹理
        /// </summary>
        /// <returns>BlendMask纹理（R通道：0=草地，1=路径），失败返回null</returns>
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

            Logger.I("MapBlendMask", "开始生成BlendMask: 地图{0}×{1} → 纹理{2}×{3}",
                mapWidth, mapHeight, texWidth, texHeight);

            // ===== 步骤1：生成原始Mask（路径=1，草地=0）=====
            float[] rawMask = GenerateRawMask(grid, texWidth, texHeight);

            // ===== 步骤2：高斯模糊 =====
            float[] blurredMask = rawMask;
            for (int i = 0; i < BLUR_ITERATIONS; i++)
            {
                blurredMask = GaussianBlur(blurredMask, texWidth, texHeight, BLUR_RADIUS);
            }

            // ===== 步骤3：生成Texture2D =====
            Texture2D maskTex = CreateMaskTexture(blurredMask, texWidth, texHeight);

            Logger.I("MapBlendMask", "✅ BlendMask生成完成: {0}×{1}", texWidth, texHeight);
            return maskTex;
        }

        /// <summary>
        /// 生成指定模糊半径的混合遮罩（可自定义参数版本）
        /// </summary>
        /// <param name="pixelsPerCell">每格子像素数</param>
        /// <param name="blurRadius">模糊半径</param>
        /// <param name="blurIterations">模糊迭代次数</param>
        public static Texture2D GenerateBlendMask(int pixelsPerCell, int blurRadius, int blurIterations)
        {
            var grid = GridSystem.Instance;
            if (grid == null || !grid.IsMapLoaded) return null;

            int texWidth = grid.Width * pixelsPerCell;
            int texHeight = grid.Height * pixelsPerCell;

            float[] rawMask = GenerateRawMask(grid, texWidth, texHeight);

            float[] blurredMask = rawMask;
            for (int i = 0; i < blurIterations; i++)
            {
                blurredMask = GaussianBlur(blurredMask, texWidth, texHeight, blurRadius);
            }

            return CreateMaskTexture(blurredMask, texWidth, texHeight);
        }

        // ========== 内部方法 ==========

        /// <summary>
        /// 生成原始Mask数据（路径相关=1，其他=0）
        /// </summary>
        private static float[] GenerateRawMask(GridSystem grid, int texWidth, int texHeight)
        {
            float[] mask = new float[texWidth * texHeight];
            int mapWidth = grid.Width;
            int mapHeight = grid.Height;

            for (int py = 0; py < texHeight; py++)
            {
                for (int px = 0; px < texWidth; px++)
                {
                    // 像素坐标 → 网格坐标
                    int cellX = px * mapWidth / texWidth;
                    int cellY = py * mapHeight / texHeight;

                    // 确保不越界
                    cellX = Mathf.Clamp(cellX, 0, mapWidth - 1);
                    cellY = Mathf.Clamp(cellY, 0, mapHeight - 1);

                    var cell = grid.GetCell(cellX, cellY);

                    // 路径、出生点、基地 → 白色(1)，表示使用路径纹理
                    // 其他类型 → 黑色(0)，表示使用草地纹理
                    float value = 0f;
                    switch (cell.Type)
                    {
                        case GridCellType.Path:
                        case GridCellType.SpawnPoint:
                        case GridCellType.BasePoint:
                            value = 1f;
                            break;
                        default:
                            value = 0f;
                            break;
                    }

                    mask[py * texWidth + px] = value;
                }
            }

            return mask;
        }

        /// <summary>
        /// 高斯模糊（CPU端，分离式两遍模糊）
        /// 先水平模糊，再垂直模糊，性能 O(n*r) 而非 O(n*r²)
        /// </summary>
        private static float[] GaussianBlur(float[] input, int width, int height, int radius)
        {
            // 生成高斯核
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

            // 归一化
            for (int i = 0; i < size; i++)
            {
                kernel[i] /= sum;
            }

            return kernel;
        }

        /// <summary>
        /// 将float数组转换为Texture2D
        /// </summary>
        private static Texture2D CreateMaskTexture(float[] data, int width, int height)
        {
            // 使用R8格式节省内存（只需要R通道）
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear; // 双线性过滤让过渡更平滑
            tex.wrapMode = TextureWrapMode.Clamp;  // 边缘不重复

            Color[] pixels = new Color[width * height];
            for (int i = 0; i < data.Length; i++)
            {
                float v = Mathf.Clamp01(data[i]);
                pixels[i] = new Color(v, v, v, 1f);
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false); // 不生成mipmap，不设为只读（方便调试）

            return tex;
        }
    }
}
