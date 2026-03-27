
// ============================================================
// 文件名：SceneController.cs
// 功能描述：场景管理器 — 异步加载、进度回调、场景切换
// 创建时间：2026-03-25
// 所属模块：Framework
// 对应交互：阶段二 #51
// ============================================================

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AetheraSurvivors.Framework
{
    /// <summary>
    /// 场景管理器 — 全局单例
    /// 
    /// 职责：
    /// 1. 异步场景加载（带进度回调）
    /// 2. 加载完成回调
    /// 3. 支持Loading过渡（最小加载时间，避免闪屏）
    /// 4. 场景切换前后事件通知
    /// 
    /// 使用示例：
    ///   SceneController.Instance.LoadScene("BattleScene", 
    ///       onProgress: p => loadingBar.value = p,
    ///       onComplete: () => Debug.Log("加载完成")
    ///   );
    /// </summary>
    public class SceneController : MonoSingleton<SceneController>
    {
        // ========== 常量 ==========

        /// <summary>最小加载时间（秒），避免Loading界面一闪而过</summary>
        private const float MinLoadTime = 0.5f;

        // ========== 私有字段 ==========

        /// <summary>是否正在加载场景</summary>
        private bool _isLoading;

        /// <summary>当前场景名</summary>
        private string _currentSceneName;

        // ========== 公共属性 ==========

        /// <summary>是否正在加载中</summary>
        public bool IsLoading => _isLoading;

        /// <summary>当前场景名</summary>
        public string CurrentSceneName => _currentSceneName;

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            _currentSceneName = SceneManager.GetActiveScene().name;
        }

        // ========== 公共方法 ==========

        /// <summary>
        /// 异步加载场景
        /// </summary>
        /// <param name="sceneName">场景名称</param>
        /// <param name="onProgress">进度回调（0-1）</param>
        /// <param name="onComplete">加载完成回调</param>
        /// <param name="loadMode">加载模式（默认Single替换当前场景）</param>
        public void LoadScene(string sceneName, Action<float> onProgress = null, Action onComplete = null,
                              LoadSceneMode loadMode = LoadSceneMode.Single)
        {
            if (_isLoading)
            {
                Debug.LogWarning($"[SceneController] 正在加载中，忽略新请求: {sceneName}");
                return;
            }

            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("[SceneController] 场景名不能为空");
                return;
            }

            StartCoroutine(LoadSceneAsync(sceneName, onProgress, onComplete, loadMode));
        }

        /// <summary>
        /// 重新加载当前场景
        /// </summary>
        public void ReloadCurrentScene(Action<float> onProgress = null, Action onComplete = null)
        {
            LoadScene(_currentSceneName, onProgress, onComplete);
        }

        /// <summary>
        /// 卸载附加场景（仅Additive模式加载的场景）
        /// </summary>
        /// <param name="sceneName">要卸载的场景名</param>
        /// <param name="onComplete">卸载完成回调</param>
        public void UnloadScene(string sceneName, Action onComplete = null)
        {
            if (string.IsNullOrEmpty(sceneName)) return;

            StartCoroutine(UnloadSceneAsync(sceneName, onComplete));
        }

        // ========== 私有方法 ==========

        /// <summary>异步加载场景协程</summary>
        private IEnumerator LoadSceneAsync(string sceneName, Action<float> onProgress,
                                            Action onComplete, LoadSceneMode loadMode)
        {
            _isLoading = true;
            float startTime = Time.realtimeSinceStartup;

            // 发布场景即将卸载事件
            if (EventBus.HasInstance)
            {
                EventBus.Instance.Publish(new SceneLoadedEvent { SceneName = _currentSceneName });
            }

            // 开始异步加载
            var operation = SceneManager.LoadSceneAsync(sceneName, loadMode);
            operation.allowSceneActivation = false;

            // 等待加载（Unity异步加载到90%时会暂停，等待allowSceneActivation=true）
            while (operation.progress < 0.9f)
            {
                // 将Unity的0-0.9映射到0-0.9
                float progress = Mathf.Clamp01(operation.progress / 0.9f) * 0.9f;
                onProgress?.Invoke(progress);
                yield return null;
            }

            onProgress?.Invoke(0.9f);

            // 确保最小加载时间（避免Loading界面一闪而过）
            float elapsed = Time.realtimeSinceStartup - startTime;
            if (elapsed < MinLoadTime)
            {
                float remaining = MinLoadTime - elapsed;
                float timer = 0f;
                while (timer < remaining)
                {
                    timer += Time.deltaTime;
                    float fakeProgress = 0.9f + 0.1f * (timer / remaining);
                    onProgress?.Invoke(fakeProgress);
                    yield return null;
                }
            }

            // 允许场景激活
            operation.allowSceneActivation = true;

            // 等待场景完全加载
            while (!operation.isDone)
            {
                yield return null;
            }

            // 更新当前场景名
            if (loadMode == LoadSceneMode.Single)
            {
                _currentSceneName = sceneName;
            }

            onProgress?.Invoke(1f);
            _isLoading = false;

            // 发布场景加载完成事件
            if (EventBus.HasInstance)
            {
                EventBus.Instance.Publish(new SceneLoadedEvent { SceneName = sceneName });
            }

            onComplete?.Invoke();
        }

        /// <summary>异步卸载场景协程</summary>
        private IEnumerator UnloadSceneAsync(string sceneName, Action onComplete)
        {
            var operation = SceneManager.UnloadSceneAsync(sceneName);

            if (operation == null)
            {
                Debug.LogWarning($"[SceneController] 无法卸载场景: {sceneName}");
                onComplete?.Invoke();
                yield break;
            }

            while (!operation.isDone)
            {
                yield return null;
            }

            onComplete?.Invoke();
        }
    }
}
