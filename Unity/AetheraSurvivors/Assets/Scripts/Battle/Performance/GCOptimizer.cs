// ============================================================
// 文件名：GCOptimizer.cs
// 功能描述：GC优化 — 零GC分配工具集、缓冲区复用、
//          字符串构建优化、委托缓存
// 创建时间：2026-03-25
// 所属模块：Battle/Performance
// 对应交互：阶段三 #167-#168
// ============================================================

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AetheraSurvivors.Battle.Performance
{
    /// <summary>
    /// GC优化工具集 — 提供各种零GC/低GC的工具方法
    /// 
    /// 微信小游戏WebGL平台GC非常敏感：
    /// 1. 每次GC都会导致明显卡顿（尤其是低端机）
    /// 2. 频繁的小分配会加快GC触发频率
    /// 
    /// 核心原则：
    /// - 避免在Update/FixedUpdate中产生GC分配
    /// - 复用临时数组/列表/字符串
    /// - 缓存委托和闭包
    /// - 使用struct代替class做临时数据
    /// </summary>
    public static class GCOptimizer
    {
        // ========== 共享StringBuilder ==========

        /// <summary>共享StringBuilder（避免字符串拼接产生GC）</summary>
        [ThreadStatic]
        private static StringBuilder _sharedSB;

        /// <summary>
        /// 获取共享StringBuilder
        /// 用法：var sb = GCOptimizer.GetStringBuilder(); sb.Append(...); var result = sb.ToString();
        /// ⚠️ 非线程安全，仅限主线程使用
        /// </summary>
        public static StringBuilder GetStringBuilder()
        {
            if (_sharedSB == null)
            {
                _sharedSB = new StringBuilder(256);
            }
            _sharedSB.Clear();
            return _sharedSB;
        }

        // ========== 共享临时列表 ==========

        /// <summary>共享的Transform列表（避免每次FindTarget创建新List）</summary>
        private static readonly List<Transform> _sharedTransformList = new List<Transform>(64);

        /// <summary>获取共享Transform列表（使用前会自动Clear）</summary>
        public static List<Transform> GetSharedTransformList()
        {
            _sharedTransformList.Clear();
            return _sharedTransformList;
        }

        /// <summary>共享的Vector3列表</summary>
        private static readonly List<Vector3> _sharedVector3List = new List<Vector3>(64);

        /// <summary>获取共享Vector3列表</summary>
        public static List<Vector3> GetSharedVector3List()
        {
            _sharedVector3List.Clear();
            return _sharedVector3List;
        }

        /// <summary>共享的int列表</summary>
        private static readonly List<int> _sharedIntList = new List<int>(64);

        /// <summary>获取共享int列表</summary>
        public static List<int> GetSharedIntList()
        {
            _sharedIntList.Clear();
            return _sharedIntList;
        }

        // ========== 共享临时数组 ==========

        /// <summary>共享的Collider2D数组（Physics2D.OverlapCircleNonAlloc使用）</summary>
        private static readonly Collider2D[] _sharedColliders = new Collider2D[64];

        /// <summary>获取共享Collider2D数组</summary>
        public static Collider2D[] GetSharedColliderArray() => _sharedColliders;

        /// <summary>共享的RaycastHit2D数组</summary>
        private static readonly RaycastHit2D[] _sharedRaycastHits = new RaycastHit2D[16];

        /// <summary>获取共享RaycastHit2D数组</summary>
        public static RaycastHit2D[] GetSharedRaycastArray() => _sharedRaycastHits;

        // ========== 零GC的数字转字符串 ==========

        /// <summary>
        /// 整数缓存（0-999的字符串预缓存，避免int.ToString()产生GC）
        /// </summary>
        private static readonly string[] _intStringCache = new string[1000];

        static GCOptimizer()
        {
            // 预缓存0-999的字符串
            for (int i = 0; i < 1000; i++)
            {
                _intStringCache[i] = i.ToString();
            }
        }

        /// <summary>
        /// 零GC的整数转字符串（0-999直接返回缓存，超出范围才分配）
        /// </summary>
        public static string IntToStringCached(int value)
        {
            if (value >= 0 && value < 1000)
            {
                return _intStringCache[value];
            }
            return value.ToString(); // 超出范围，不可避免的GC
        }

        // ========== 零GC的浮点格式化 ==========

        /// <summary>
        /// 低GC的浮点格式化（将浮点数格式化到共享StringBuilder）
        /// </summary>
        public static string FormatFloat(float value, int decimalPlaces = 1)
        {
            var sb = GetStringBuilder();

            if (value < 0)
            {
                sb.Append('-');
                value = -value;
            }

            int intPart = (int)value;
            sb.Append(intPart);

            if (decimalPlaces > 0)
            {
                sb.Append('.');
                float fracPart = value - intPart;
                for (int i = 0; i < decimalPlaces; i++)
                {
                    fracPart *= 10;
                    int digit = (int)fracPart;
                    sb.Append((char)('0' + digit));
                    fracPart -= digit;
                }
            }

            return sb.ToString();
        }

        // ========== 委托缓存 ==========

        /// <summary>
        /// 缓存的WaitForSeconds实例（避免每次new WaitForSeconds产生GC）
        /// </summary>
        private static readonly Dictionary<float, WaitForSeconds> _waitCache = new Dictionary<float, WaitForSeconds>(16);

        /// <summary>
        /// 获取缓存的WaitForSeconds
        /// </summary>
        public static WaitForSeconds GetWaitForSeconds(float seconds)
        {
            // 四舍五入到0.1精度，增加缓存命中率
            float key = Mathf.Round(seconds * 10f) / 10f;

            if (!_waitCache.TryGetValue(key, out var wait))
            {
                wait = new WaitForSeconds(key);
                _waitCache[key] = wait;
            }

            return wait;
        }

        /// <summary>WaitForEndOfFrame的缓存实例</summary>
        public static readonly WaitForEndOfFrame WaitEndOfFrame = new WaitForEndOfFrame();

        /// <summary>WaitForFixedUpdate的缓存实例</summary>
        public static readonly WaitForFixedUpdate WaitFixedUpdate = new WaitForFixedUpdate();

        // ========== 颜色缓存 ==========

        /// <summary>常用颜色预缓存（避免new Color产生的装箱）</summary>
        public static readonly Color White = Color.white;
        public static readonly Color Red = Color.red;
        public static readonly Color Green = Color.green;
        public static readonly Color Blue = Color.blue;
        public static readonly Color Yellow = Color.yellow;
        public static readonly Color Clear = Color.clear;
        public static readonly Color HalfWhite = new Color(1f, 1f, 1f, 0.5f);
        public static readonly Color HalfRed = new Color(1f, 0f, 0f, 0.5f);

        // ========== HashSet/Dictionary 工具 ==========

        /// <summary>
        /// 安全地遍历并移除字典中的元素（避免"Collection was modified"异常）
        /// ⚠️ 会产生一次List分配，但避免了异常
        /// </summary>
        public static void RemoveWhere<TKey, TValue>(Dictionary<TKey, TValue> dict, Func<TKey, TValue, bool> predicate)
        {
            var keysToRemove = new List<TKey>(4);
            foreach (var pair in dict)
            {
                if (predicate(pair.Key, pair.Value))
                {
                    keysToRemove.Add(pair.Key);
                }
            }
            for (int i = 0; i < keysToRemove.Count; i++)
            {
                dict.Remove(keysToRemove[i]);
            }
        }
    }

    // ====================================================================
    // 零GC事件参数池
    // ====================================================================

    /// <summary>
    /// 事件参数对象池 — 避免频繁的事件参数struct装箱
    /// 使用struct事件+泛型已避免了大部分装箱，此处作为补充优化
    /// </summary>
    public static class EventArgPool<T> where T : struct
    {
        private static readonly Stack<T[]> _pool = new Stack<T[]>(8);

        /// <summary>获取一个单元素数组（避免参数传递时的装箱）</summary>
        public static T[] Rent()
        {
            return _pool.Count > 0 ? _pool.Pop() : new T[1];
        }

        /// <summary>归还</summary>
        public static void Return(T[] array)
        {
            if (array != null && array.Length == 1 && _pool.Count < 32)
            {
                _pool.Push(array);
            }
        }
    }
}
