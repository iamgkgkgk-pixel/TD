
// ============================================================
// 文件名：DataBinding.cs
// 功能描述：响应式数据绑定系统 — 数据变化时自动通知UI刷新
// 创建时间：2026-03-25
// 所属模块：Data
// 对应交互：阶段二 #57
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace AetheraSurvivors.Data
{
    /// <summary>
    /// 可观察属性 — 值变化时自动通知所有监听者
    /// 
    /// 使用示例：
    ///   // 定义可观察属性
    ///   public ObservableProperty&lt;int&gt; Gold = new ObservableProperty&lt;int&gt;(0);
    ///   
    ///   // 订阅变化
    ///   Gold.OnValueChanged += (oldVal, newVal) => {
    ///       goldText.text = newVal.ToString();
    ///   };
    ///   
    ///   // 修改值（会自动触发回调）
    ///   Gold.Value = 100;
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    public class ObservableProperty<T>
    {
        /// <summary>值变化事件（旧值, 新值）</summary>
        public event Action<T, T> OnValueChanged;

        private T _value;

        /// <summary>
        /// 当前值（设置时如果值变化会触发OnValueChanged）
        /// </summary>
        public T Value
        {
            get => _value;
            set
            {
                if (EqualityComparer<T>.Default.Equals(_value, value)) return;

                T oldValue = _value;
                _value = value;
                OnValueChanged?.Invoke(oldValue, _value);
            }
        }

        /// <summary>
        /// 强制设置值（即使值相同也触发通知）
        /// </summary>
        public void SetValueForce(T value)
        {
            T oldValue = _value;
            _value = value;
            OnValueChanged?.Invoke(oldValue, _value);
        }

        /// <summary>
        /// 静默设置值（不触发通知）
        /// </summary>
        public void SetValueSilent(T value)
        {
            _value = value;
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public ObservableProperty(T initialValue = default)
        {
            _value = initialValue;
        }

        /// <summary>
        /// 清除所有监听者
        /// </summary>
        public void ClearListeners()
        {
            OnValueChanged = null;
        }

        /// <summary>
        /// 隐式转换为值类型
        /// </summary>
        public static implicit operator T(ObservableProperty<T> property)
        {
            return property._value;
        }

        public override string ToString()
        {
            return _value?.ToString() ?? "null";
        }
    }

    /// <summary>
    /// 可观察列表 — 列表变化时通知
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    public class ObservableList<T>
    {
        /// <summary>列表变化事件</summary>
        public event Action OnListChanged;

        /// <summary>元素添加事件</summary>
        public event Action<T> OnItemAdded;

        /// <summary>元素移除事件</summary>
        public event Action<T> OnItemRemoved;

        private readonly List<T> _list;

        /// <summary>元素数量</summary>
        public int Count => _list.Count;

        /// <summary>索引访问器</summary>
        public T this[int index]
        {
            get => _list[index];
            set
            {
                _list[index] = value;
                OnListChanged?.Invoke();
            }
        }

        public ObservableList(int capacity = 8)
        {
            _list = new List<T>(capacity);
        }

        public void Add(T item)
        {
            _list.Add(item);
            OnItemAdded?.Invoke(item);
            OnListChanged?.Invoke();
        }

        public bool Remove(T item)
        {
            bool removed = _list.Remove(item);
            if (removed)
            {
                OnItemRemoved?.Invoke(item);
                OnListChanged?.Invoke();
            }
            return removed;
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= _list.Count) return;
            T item = _list[index];
            _list.RemoveAt(index);
            OnItemRemoved?.Invoke(item);
            OnListChanged?.Invoke();
        }

        public void Clear()
        {
            _list.Clear();
            OnListChanged?.Invoke();
        }

        public bool Contains(T item) => _list.Contains(item);

        public int IndexOf(T item) => _list.IndexOf(item);

        public List<T> ToList() => new List<T>(_list);

        public void ClearListeners()
        {
            OnListChanged = null;
            OnItemAdded = null;
            OnItemRemoved = null;
        }
    }

    /// <summary>
    /// 数据绑定辅助类 — 简化UI绑定
    /// 
    /// 使用示例：
    ///   // 绑定Text到金币数据
    ///   DataBinder.BindText(goldProp, goldText, v => $"金币: {v}");
    ///   
    ///   // 绑定Slider到血量
    ///   DataBinder.BindSlider(hpProp, hpSlider, maxHp);
    /// </summary>
    public static class DataBinder
    {
        /// <summary>
        /// 绑定可观察属性到UnityEngine.UI.Text
        /// </summary>
        /// <typeparam name="T">值类型</typeparam>
        /// <param name="property">可观察属性</param>
        /// <param name="text">UI Text组件</param>
        /// <param name="formatter">格式化函数</param>
        public static void BindText<T>(ObservableProperty<T> property, UnityEngine.UI.Text text,
                                        Func<T, string> formatter = null)
        {
            if (property == null || text == null) return;

            // 立即刷新一次
            text.text = formatter != null ? formatter(property.Value) : property.Value?.ToString() ?? string.Empty;

            // 订阅变化
            property.OnValueChanged += (oldVal, newVal) =>
            {
                if (text != null) // UI组件可能已被销毁
                {
                    text.text = formatter != null ? formatter(newVal) : newVal?.ToString() ?? string.Empty;
                }
            };
        }

        /// <summary>
        /// 绑定可观察int属性到Slider（按比例）
        /// </summary>
        public static void BindSlider(ObservableProperty<int> property, UnityEngine.UI.Slider slider,
                                       int maxValue)
        {
            if (property == null || slider == null || maxValue <= 0) return;

            slider.value = (float)property.Value / maxValue;

            property.OnValueChanged += (oldVal, newVal) =>
            {
                if (slider != null)
                {
                    slider.value = (float)newVal / maxValue;
                }
            };
        }

        /// <summary>
        /// 绑定可观察float属性到Slider
        /// </summary>
        public static void BindSlider(ObservableProperty<float> property, UnityEngine.UI.Slider slider,
                                       float maxValue)
        {
            if (property == null || slider == null || maxValue <= 0) return;

            slider.value = property.Value / maxValue;

            property.OnValueChanged += (oldVal, newVal) =>
            {
                if (slider != null)
                {
                    slider.value = newVal / maxValue;
                }
            };
        }

        /// <summary>
        /// 绑定可观察bool属性到GameObject的active状态
        /// </summary>
        public static void BindActive(ObservableProperty<bool> property, GameObject go)
        {
            if (property == null || go == null) return;

            go.SetActive(property.Value);

            property.OnValueChanged += (oldVal, newVal) =>
            {
                if (go != null)
                {
                    go.SetActive(newVal);
                }
            };
        }
    }
}
