
// ============================================================
// 文件名：BasePanel.cs
// 功能描述：UI面板基类 — 所有UI面板必须继承此类
//          提供统一的生命周期回调和UI层级管理
// 创建时间：2026-03-25
// 所属模块：Framework
// 对应交互：阶段二 #48
// ============================================================

using UnityEngine;

namespace AetheraSurvivors.Framework
{
    /// <summary>
    /// UI层级枚举
    /// </summary>
    public enum UILayer
    {
        /// <summary>底层：主界面/战斗HUD等常驻面板</summary>
        Bottom = 0,

        /// <summary>普通层：大部分功能面板</summary>
        Normal = 100,

        /// <summary>弹窗层：确认框/提示框</summary>
        Popup = 200,

        /// <summary>顶层：Loading/Toast/引导遮罩</summary>
        Top = 300,

        /// <summary>系统层：系统级弹窗（断网提示/强更新）</summary>
        System = 400
    }

    /// <summary>
    /// UI面板基类 — 所有UI面板必须继承此类
    /// 
    /// 使用流程：
    /// 1. 创建面板预制体，挂载继承自BasePanel的脚本
    /// 2. 将预制体放到 Resources/Prefabs/UI/ 目录
    /// 3. 通过 UIManager.Instance.Open&lt;MyPanel&gt;() 打开
    /// 
    /// 生命周期：
    ///   OnOpen(param) → OnShow() → [用户操作] → OnHide() → OnClose()
    ///   
    ///   OnOpen:  首次打开时调用（初始化）
    ///   OnShow:  每次显示时调用（刷新数据）
    ///   OnHide:  每次隐藏时调用（暂停逻辑）
    ///   OnClose: 被销毁时调用（清理资源）
    /// </summary>
    public abstract class BasePanel : MonoBehaviour
    {
        // ========== 公共属性 ==========

        /// <summary>面板所属UI层级（子类可重写）</summary>
        public virtual UILayer Layer => UILayer.Normal;

        /// <summary>是否缓存面板（关闭后不销毁，下次打开直接显示）</summary>
        public virtual bool IsCached => true;

        /// <summary>是否显示半透明遮罩背景</summary>
        public virtual bool HasMask => false;

        /// <summary>点击遮罩是否关闭面板</summary>
        public virtual bool CloseOnMaskClick => false;

        /// <summary>面板是否正在显示</summary>
        public bool IsShowing { get; private set; }

        /// <summary>面板打开时传入的参数</summary>
        protected object OpenParam { get; private set; }

        // ========== 私有字段 ==========

        private CanvasGroup _canvasGroup;

        // ========== 公共方法（UIManager调用） ==========

        /// <summary>
        /// 打开面板（UIManager内部调用）
        /// </summary>
        /// <param name="param">传入参数</param>
        internal void InternalOpen(object param)
        {
            OpenParam = param;
            gameObject.SetActive(true);
            IsShowing = true;

            OnOpen(param);
            OnShow();
        }

        /// <summary>
        /// 显示面板（从缓存中恢复显示）
        /// </summary>
        internal void InternalShow()
        {
            gameObject.SetActive(true);
            IsShowing = true;
            OnShow();
        }

        /// <summary>
        /// 隐藏面板（被其他面板覆盖时）
        /// </summary>
        internal void InternalHide()
        {
            IsShowing = false;
            OnHide();
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 关闭面板（销毁或放入缓存）
        /// </summary>
        internal void InternalClose()
        {
            IsShowing = false;
            OnHide();
            OnClose();
        }

        // ========== 子类重写的生命周期方法 ==========

        /// <summary>
        /// 面板首次打开时调用
        /// 用于：查找UI组件引用、绑定按钮事件
        /// </summary>
        /// <param name="param">传入的参数对象</param>
        protected virtual void OnOpen(object param)
        {
        }

        /// <summary>
        /// 面板每次显示时调用
        /// 用于：刷新数据、播放进入动画
        /// </summary>
        protected virtual void OnShow()
        {
        }

        /// <summary>
        /// 面板每次隐藏时调用
        /// 用于：暂停逻辑、停止动画
        /// </summary>
        protected virtual void OnHide()
        {
        }

        /// <summary>
        /// 面板被销毁前调用
        /// 用于：清理资源、取消事件订阅
        /// </summary>
        protected virtual void OnClose()
        {
        }

        // ========== 便捷方法 ==========

        /// <summary>
        /// 关闭自身面板
        /// </summary>
        protected void CloseSelf()
        {
            if (UIManager.HasInstance)
            {
                UIManager.Instance.Close(GetType());
            }
        }

        /// <summary>
        /// 设置面板透明度（通过CanvasGroup）
        /// </summary>
        protected void SetAlpha(float alpha)
        {
            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                {
                    _canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }
            _canvasGroup.alpha = alpha;
        }

        /// <summary>
        /// 设置面板是否可交互
        /// </summary>
        protected void SetInteractable(bool interactable)
        {
            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                {
                    _canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }
            _canvasGroup.interactable = interactable;
            _canvasGroup.blocksRaycasts = interactable;
        }
    }
}
