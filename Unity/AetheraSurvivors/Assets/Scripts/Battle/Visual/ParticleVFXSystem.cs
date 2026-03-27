// ============================================================
// 文件名：ParticleVFXSystem.cs
// 功能描述：战斗粒子特效系统
//          爆炸、冰冻、火焰、毒雾、雷电、治疗等粒子特效
//          统一管理创建、播放、对象池回收
// 创建时间：2026-03-25
// 所属模块：Battle/Visual
// 对应交互：阶段三 #156-#160
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Battle.Tower;
using Logger = AetheraSurvivors.Framework.Logger;

namespace AetheraSurvivors.Battle.Visual

{
    // ====================================================================
    // 粒子特效类型
    // ====================================================================

    /// <summary>粒子特效类型</summary>
    public enum VFXType
    {
        // 爆炸类
        Explosion,          // 通用爆炸
        CannonExplosion,    // 炮塔爆炸（大范围）

        // 冰冻类
        IceHit,             // 冰冻命中
        IceAura,            // 冰塔范围光环
        FreezeShatter,      // 冰冻粉碎

        // 火焰类
        FireHit,            // 火焰命中
        FireBreath,         // 火焰吐息（Boss龙）
        FireBurst,          // 火焰爆发

        // 毒雾类
        PoisonCloud,        // 毒雾区域
        PoisonHit,          // 毒伤命中
        PoisonSpread,       // 毒素扩散

        // 雷电类
        LightningStrike,    // 闪电打击
        ChainLightning,     // 连锁闪电

        // 治疗类
        HealPulse,          // 治疗脉冲
        ShieldBubble,       // 护盾气泡

        // 通用
        SpawnPortal,        // 怪物出生传送门
        DeathPoof,          // 通用死亡烟雾
        LevelUp,            // 塔升级特效
        GoldSparkle         // 金矿产出闪光
    }

    // ====================================================================
    // 粒子特效数据
    // ====================================================================

    /// <summary>粒子特效配置</summary>
    public class VFXConfig
    {
        public VFXType Type;
        public Color StartColor;
        public Color EndColor;
        public float Duration;
        public float StartSize;
        public float EndSize;
        public int ParticleCount;
        public float StartSpeed;
        public float Gravity;
        public ParticleSystemShapeType ShapeType;
        public float ShapeRadius;
        public bool Loop;
    }

    // ====================================================================
    // ParticleVFXSystem 核心类
    // ====================================================================

    /// <summary>
    /// 粒子特效系统 — 统一管理所有战斗粒子效果
    /// 
    /// 设计：
    /// 1. 运行时动态创建ParticleSystem（无需外部预制体）
    /// 2. 对象池管理，避免频繁创建/销毁
    /// 3. 按类型预配置参数
    /// </summary>
    public class ParticleVFXSystem : MonoSingleton<ParticleVFXSystem>
    {
        // ========== 对象池 ==========

        /// <summary>特效对象池（按VFXType分池）</summary>
        private readonly Dictionary<VFXType, Queue<ParticleSystem>> _pools
            = new Dictionary<VFXType, Queue<ParticleSystem>>();

        /// <summary>活跃的特效（等待回收）</summary>
        private readonly List<(ParticleSystem ps, float endTime, VFXType type)> _activeEffects
            = new List<(ParticleSystem, float, VFXType)>(64);

        /// <summary>特效配置缓存</summary>
        private readonly Dictionary<VFXType, VFXConfig> _configs = new Dictionary<VFXType, VFXConfig>();

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            InitConfigs();
            Logger.I("ParticleVFX", "粒子特效系统初始化，配置={0}种", _configs.Count);
        }

        protected override void OnDispose()
        {
            ClearAll();
        }

        private void Update()
        {
            // 回收已结束的特效
            float time = Time.time;
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                var (ps, endTime, type) = _activeEffects[i];
                if (ps == null || time >= endTime)
                {
                    if (ps != null)
                    {
                        ReturnToPool(type, ps);
                    }
                    _activeEffects.RemoveAt(i);
                }
            }
        }

        // ========== 核心方法 ==========

        /// <summary>
        /// 在指定位置播放粒子特效
        /// </summary>
        /// <param name="type">特效类型</param>
        /// <param name="position">世界坐标</param>
        /// <param name="scale">缩放倍率</param>
        /// <param name="colorOverride">颜色覆盖（null使用默认配色）</param>
        /// <returns>ParticleSystem（可用于后续控制）</returns>
        public ParticleSystem Play(VFXType type, Vector3 position, float scale = 1f, Color? colorOverride = null)
        {
            if (!_configs.TryGetValue(type, out var config))
            {
                Logger.W("ParticleVFX", "未找到特效配置: {0}", type);
                return null;
            }

            var ps = GetFromPool(type, config);
            if (ps == null) return null;

            ps.transform.position = position;
            ps.transform.localScale = Vector3.one * scale;

            // 应用颜色覆盖
            if (colorOverride.HasValue)
            {
                var main = ps.main;
                main.startColor = new ParticleSystem.MinMaxGradient(colorOverride.Value);
            }

            ps.gameObject.SetActive(true);
            ps.Play(true);

            float duration = config.Loop ? config.Duration : config.Duration + 0.5f;
            _activeEffects.Add((ps, Time.time + duration, type));

            return ps;
        }

        /// <summary>
        /// 在指定位置播放并立即设置范围
        /// </summary>
        public ParticleSystem PlayWithRadius(VFXType type, Vector3 position, float radius)
        {
            return Play(type, position, radius / 1f);
        }

        /// <summary>停止指定特效</summary>
        public void Stop(ParticleSystem ps)
        {
            if (ps != null)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        /// <summary>清理所有特效</summary>
        public void ClearAll()
        {
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                var (ps, _, type) = _activeEffects[i];
                if (ps != null)
                {
                    ps.Stop(true);
                    ps.gameObject.SetActive(false);
                }
            }
            _activeEffects.Clear();

            // 清空对象池
            foreach (var pair in _pools)
            {
                while (pair.Value.Count > 0)
                {
                    var ps = pair.Value.Dequeue();
                    if (ps != null) Destroy(ps.gameObject);
                }
            }
            _pools.Clear();
        }

        // ========== 便捷方法 ==========

        /// <summary>播放爆炸特效</summary>
        public void PlayExplosion(Vector3 pos, float radius = 1f)
        {
            Play(VFXType.Explosion, pos, radius);
        }

        /// <summary>播放炮塔爆炸</summary>
        public void PlayCannonExplosion(Vector3 pos, float radius = 1.5f)
        {
            Play(VFXType.CannonExplosion, pos, radius);
        }

        /// <summary>播放冰冻命中</summary>
        public void PlayIceHit(Vector3 pos)
        {
            Play(VFXType.IceHit, pos);
        }

        /// <summary>播放冰塔光环</summary>
        public ParticleSystem PlayIceAura(Vector3 pos, float radius)
        {
            return Play(VFXType.IceAura, pos, radius);
        }

        /// <summary>播放火焰命中</summary>
        public void PlayFireHit(Vector3 pos)
        {
            Play(VFXType.FireHit, pos);
        }

        /// <summary>播放火焰吐息</summary>
        public ParticleSystem PlayFireBreath(Vector3 pos, float scale = 1f)
        {
            return Play(VFXType.FireBreath, pos, scale);
        }

        /// <summary>播放毒雾</summary>
        public ParticleSystem PlayPoisonCloud(Vector3 pos, float radius)
        {
            return Play(VFXType.PoisonCloud, pos, radius);
        }

        /// <summary>播放毒伤</summary>
        public void PlayPoisonHit(Vector3 pos)
        {
            Play(VFXType.PoisonHit, pos, 0.5f);
        }

        /// <summary>播放治疗</summary>
        public void PlayHealPulse(Vector3 pos, float radius = 1f)
        {
            Play(VFXType.HealPulse, pos, radius);
        }

        /// <summary>播放护盾</summary>
        public ParticleSystem PlayShieldBubble(Vector3 pos)
        {
            return Play(VFXType.ShieldBubble, pos);
        }

        /// <summary>播放出生传送门</summary>
        public void PlaySpawnPortal(Vector3 pos)
        {
            Play(VFXType.SpawnPortal, pos);
        }

        /// <summary>播放塔升级特效</summary>
        public void PlayLevelUp(Vector3 pos)
        {
            Play(VFXType.LevelUp, pos);
        }

        /// <summary>播放金矿闪光</summary>
        public void PlayGoldSparkle(Vector3 pos)
        {
            Play(VFXType.GoldSparkle, pos, 0.8f);
        }

        // ========== 对象池管理 ==========

        private ParticleSystem GetFromPool(VFXType type, VFXConfig config)
        {
            if (!_pools.TryGetValue(type, out var queue))
            {
                queue = new Queue<ParticleSystem>(8);
                _pools[type] = queue;
            }

            ParticleSystem ps = null;
            while (queue.Count > 0)
            {
                ps = queue.Dequeue();
                if (ps != null) break;
            }

            if (ps == null)
            {
                ps = CreateParticleSystem(type, config);
            }

            return ps;
        }

        private void ReturnToPool(VFXType type, ParticleSystem ps)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.gameObject.SetActive(false);

            if (!_pools.TryGetValue(type, out var queue))
            {
                queue = new Queue<ParticleSystem>(8);
                _pools[type] = queue;
            }

            if (queue.Count < 20) // 每种最多缓存20个
            {
                queue.Enqueue(ps);
            }
            else
            {
                Destroy(ps.gameObject);
            }
        }

        // ========== ParticleSystem创建 ==========

        private ParticleSystem CreateParticleSystem(VFXType type, VFXConfig config)
        {
            var obj = new GameObject($"VFX_{type}");
            obj.transform.SetParent(transform);
            obj.SetActive(false);

            var ps = obj.AddComponent<ParticleSystem>();
            var renderer = obj.GetComponent<ParticleSystemRenderer>();

            // 设置渲染器
            renderer.material = GetParticleMaterial();
            renderer.sortingOrder = 15;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            // 主模块
            var main = ps.main;
            main.duration = config.Duration;
            main.loop = config.Loop;
            main.startLifetime = new ParticleSystem.MinMaxCurve(config.Duration * 0.5f, config.Duration);
            main.startSpeed = new ParticleSystem.MinMaxCurve(config.StartSpeed * 0.5f, config.StartSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(config.StartSize * 0.5f, config.StartSize);
            main.startColor = new ParticleSystem.MinMaxGradient(config.StartColor, config.EndColor);
            main.gravityModifier = config.Gravity;
            main.maxParticles = config.ParticleCount * 2;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.None;

            // 发射模块
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, (short)config.ParticleCount)
            });

            // 形状模块
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = config.ShapeType;
            shape.radius = config.ShapeRadius;

            // 颜色随生命周期
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 0.5f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.1f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            // 大小随生命周期
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                AnimationCurve.EaseInOut(0f, config.StartSize, 1f, config.EndSize));

            return ps;
        }

        // ========== 材质 ==========

        private static Material _particleMaterial;
        private Material GetParticleMaterial()
        {
            if (_particleMaterial != null) return _particleMaterial;

            // 使用Unity内置粒子Shader
            var shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            _particleMaterial = new Material(shader);
            _particleMaterial.SetFloat("_Mode", 1); // Additive
            _particleMaterial.SetTexture("_MainTex", CreateParticleTexture());

            return _particleMaterial;
        }

        /// <summary>创建一个软圆形粒子纹理</summary>
        private Texture2D CreateParticleTexture()
        {
            int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float alpha = Mathf.Clamp01(1f - dist / center);
                    alpha = alpha * alpha; // 边缘更平滑
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        // ========== 配置初始化 ==========

        private void InitConfigs()
        {
            // ---- 爆炸类 ----
            _configs[VFXType.Explosion] = new VFXConfig
            {
                Type = VFXType.Explosion, StartColor = new Color(1f, 0.8f, 0.3f),
                EndColor = new Color(1f, 0.3f, 0f), Duration = 0.5f,
                StartSize = 0.3f, EndSize = 0.05f, ParticleCount = 15,
                StartSpeed = 3f, Gravity = 0.5f,
                ShapeType = ParticleSystemShapeType.Sphere, ShapeRadius = 0.2f
            };

            _configs[VFXType.CannonExplosion] = new VFXConfig
            {
                Type = VFXType.CannonExplosion, StartColor = new Color(1f, 0.6f, 0.1f),
                EndColor = new Color(0.5f, 0.2f, 0f), Duration = 0.8f,
                StartSize = 0.5f, EndSize = 0.1f, ParticleCount = 25,
                StartSpeed = 4f, Gravity = 0.3f,
                ShapeType = ParticleSystemShapeType.Sphere, ShapeRadius = 0.5f
            };

            // ---- 冰冻类 ----
            _configs[VFXType.IceHit] = new VFXConfig
            {
                Type = VFXType.IceHit, StartColor = new Color(0.7f, 0.9f, 1f),
                EndColor = new Color(0.4f, 0.7f, 1f), Duration = 0.4f,
                StartSize = 0.2f, EndSize = 0.05f, ParticleCount = 10,
                StartSpeed = 2f, Gravity = -0.5f,
                ShapeType = ParticleSystemShapeType.Sphere, ShapeRadius = 0.15f
            };

            _configs[VFXType.IceAura] = new VFXConfig
            {
                Type = VFXType.IceAura, StartColor = new Color(0.5f, 0.8f, 1f, 0.3f),
                EndColor = new Color(0.3f, 0.6f, 1f, 0.1f), Duration = 3f,
                StartSize = 0.15f, EndSize = 0.05f, ParticleCount = 8,
                StartSpeed = 0.5f, Gravity = -0.2f,
                ShapeType = ParticleSystemShapeType.Circle, ShapeRadius = 1f, Loop = true
            };

            _configs[VFXType.FreezeShatter] = new VFXConfig
            {
                Type = VFXType.FreezeShatter, StartColor = new Color(0.8f, 0.95f, 1f),
                EndColor = Color.white, Duration = 0.5f,
                StartSize = 0.15f, EndSize = 0.02f, ParticleCount = 12,
                StartSpeed = 4f, Gravity = 2f,
                ShapeType = ParticleSystemShapeType.Sphere, ShapeRadius = 0.1f
            };

            // ---- 火焰类 ----
            _configs[VFXType.FireHit] = new VFXConfig
            {
                Type = VFXType.FireHit, StartColor = new Color(1f, 0.9f, 0.3f),
                EndColor = new Color(1f, 0.2f, 0f), Duration = 0.4f,
                StartSize = 0.25f, EndSize = 0.05f, ParticleCount = 12,
                StartSpeed = 2.5f, Gravity = -1f,
                ShapeType = ParticleSystemShapeType.Cone, ShapeRadius = 0.15f
            };

            _configs[VFXType.FireBreath] = new VFXConfig
            {
                Type = VFXType.FireBreath, StartColor = new Color(1f, 0.9f, 0.2f),
                EndColor = new Color(1f, 0.1f, 0f), Duration = 1.5f,
                StartSize = 0.4f, EndSize = 0.1f, ParticleCount = 20,
                StartSpeed = 5f, Gravity = -0.5f,
                ShapeType = ParticleSystemShapeType.Cone, ShapeRadius = 0.3f, Loop = true
            };

            _configs[VFXType.FireBurst] = new VFXConfig
            {
                Type = VFXType.FireBurst, StartColor = new Color(1f, 0.7f, 0f),
                EndColor = new Color(1f, 0.1f, 0f), Duration = 0.6f,
                StartSize = 0.3f, EndSize = 0.05f, ParticleCount = 18,
                StartSpeed = 3.5f, Gravity = -0.8f,
                ShapeType = ParticleSystemShapeType.Sphere, ShapeRadius = 0.3f
            };

            // ---- 毒雾类 ----
            _configs[VFXType.PoisonCloud] = new VFXConfig
            {
                Type = VFXType.PoisonCloud, StartColor = new Color(0.3f, 0.8f, 0.2f, 0.3f),
                EndColor = new Color(0.2f, 0.5f, 0.1f, 0.1f), Duration = 3f,
                StartSize = 0.4f, EndSize = 0.6f, ParticleCount = 6,
                StartSpeed = 0.3f, Gravity = -0.1f,
                ShapeType = ParticleSystemShapeType.Circle, ShapeRadius = 0.8f, Loop = true
            };

            _configs[VFXType.PoisonHit] = new VFXConfig
            {
                Type = VFXType.PoisonHit, StartColor = new Color(0.4f, 1f, 0.2f),
                EndColor = new Color(0.2f, 0.6f, 0.1f), Duration = 0.3f,
                StartSize = 0.15f, EndSize = 0.03f, ParticleCount = 8,
                StartSpeed = 1.5f, Gravity = -0.3f,
                ShapeType = ParticleSystemShapeType.Sphere, ShapeRadius = 0.1f
            };

            _configs[VFXType.PoisonSpread] = new VFXConfig
            {
                Type = VFXType.PoisonSpread, StartColor = new Color(0.3f, 0.9f, 0.15f, 0.5f),
                EndColor = new Color(0.15f, 0.5f, 0.1f, 0.1f), Duration = 0.8f,
                StartSize = 0.2f, EndSize = 0.4f, ParticleCount = 10,
                StartSpeed = 2f, Gravity = -0.1f,
                ShapeType = ParticleSystemShapeType.Sphere, ShapeRadius = 0.5f
            };

            // ---- 雷电类 ----
            _configs[VFXType.LightningStrike] = new VFXConfig
            {
                Type = VFXType.LightningStrike, StartColor = new Color(0.7f, 0.8f, 1f),
                EndColor = new Color(0.5f, 0.6f, 1f), Duration = 0.2f,
                StartSize = 0.1f, EndSize = 0.02f, ParticleCount = 15,
                StartSpeed = 8f, Gravity = 0f,
                ShapeType = ParticleSystemShapeType.Sphere, ShapeRadius = 0.05f
            };

            _configs[VFXType.ChainLightning] = new VFXConfig
            {
                Type = VFXType.ChainLightning, StartColor = new Color(0.6f, 0.7f, 1f),
                EndColor = Color.white, Duration = 0.3f,
                StartSize = 0.08f, EndSize = 0.02f, ParticleCount = 20,
                StartSpeed = 10f, Gravity = 0f,
                ShapeType = ParticleSystemShapeType.Sphere, ShapeRadius = 0.03f
            };

            // ---- 治疗类 ----
            _configs[VFXType.HealPulse] = new VFXConfig
            {
                Type = VFXType.HealPulse, StartColor = new Color(0.3f, 1f, 0.5f, 0.5f),
                EndColor = new Color(0.5f, 1f, 0.7f, 0.1f), Duration = 0.8f,
                StartSize = 0.1f, EndSize = 0.03f, ParticleCount = 12,
                StartSpeed = 1f, Gravity = -1.5f,
                ShapeType = ParticleSystemShapeType.Circle, ShapeRadius = 0.5f
            };

            _configs[VFXType.ShieldBubble] = new VFXConfig
            {
                Type = VFXType.ShieldBubble, StartColor = new Color(0.5f, 0.7f, 1f, 0.3f),
                EndColor = new Color(0.3f, 0.5f, 1f, 0.1f), Duration = 5f,
                StartSize = 0.08f, EndSize = 0.04f, ParticleCount = 6,
                StartSpeed = 0.3f, Gravity = -0.2f,
                ShapeType = ParticleSystemShapeType.Circle, ShapeRadius = 0.4f, Loop = true
            };

            // ---- 通用类 ----
            _configs[VFXType.SpawnPortal] = new VFXConfig
            {
                Type = VFXType.SpawnPortal, StartColor = new Color(0.6f, 0.2f, 1f),
                EndColor = new Color(0.3f, 0.1f, 0.5f), Duration = 1f,
                StartSize = 0.2f, EndSize = 0.3f, ParticleCount = 15,
                StartSpeed = 1.5f, Gravity = -0.5f,
                ShapeType = ParticleSystemShapeType.Circle, ShapeRadius = 0.3f
            };

            _configs[VFXType.DeathPoof] = new VFXConfig
            {
                Type = VFXType.DeathPoof, StartColor = new Color(0.7f, 0.7f, 0.7f, 0.5f),
                EndColor = new Color(0.4f, 0.4f, 0.4f, 0f), Duration = 0.5f,
                StartSize = 0.2f, EndSize = 0.4f, ParticleCount = 8,
                StartSpeed = 1f, Gravity = -0.3f,
                ShapeType = ParticleSystemShapeType.Sphere, ShapeRadius = 0.2f
            };

            _configs[VFXType.LevelUp] = new VFXConfig
            {
                Type = VFXType.LevelUp, StartColor = new Color(1f, 0.9f, 0.3f),
                EndColor = new Color(1f, 1f, 0.6f), Duration = 0.8f,
                StartSize = 0.1f, EndSize = 0.03f, ParticleCount = 20,
                StartSpeed = 2.5f, Gravity = -2f,
                ShapeType = ParticleSystemShapeType.Circle, ShapeRadius = 0.3f
            };

            _configs[VFXType.GoldSparkle] = new VFXConfig
            {
                Type = VFXType.GoldSparkle, StartColor = new Color(1f, 0.85f, 0.2f),
                EndColor = new Color(1f, 0.95f, 0.5f), Duration = 0.6f,
                StartSize = 0.08f, EndSize = 0.02f, ParticleCount = 8,
                StartSpeed = 1.5f, Gravity = -1f,
                ShapeType = ParticleSystemShapeType.Sphere, ShapeRadius = 0.2f
            };
        }

        // ========== 调试信息 ==========

        /// <summary>获取调试信息</summary>
        public string GetDebugInfo()
        {
            int pooledCount = 0;
            foreach (var pair in _pools)
            {
                pooledCount += pair.Value.Count;
            }

            return $"活跃特效:{_activeEffects.Count} 池中:{pooledCount} 配置:{_configs.Count}种";
        }
    }
}
