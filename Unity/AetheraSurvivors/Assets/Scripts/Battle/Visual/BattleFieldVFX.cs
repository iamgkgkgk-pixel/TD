// ============================================================
// 文件名：BattleFieldVFX.cs
// 功能描述：战场环境视觉效果
//          波次预告动画、Boss警告、战场氛围（波次进展指示器）、
//          击杀连击提示、金币飘出动画
// 创建时间：2026-03-25
// 所属模块：Battle/Visual
// 对应交互：阶段三 #153-#155
// ============================================================

using System.Collections;
using UnityEngine;
using AetheraSurvivors.Framework;
using AetheraSurvivors.Battle.Enemy;
using AetheraSurvivors.Battle.Wave;
using Logger = AetheraSurvivors.Framework.Logger;

namespace AetheraSurvivors.Battle.Visual

{
    /// <summary>
    /// 战场环境视觉效果管理器
    /// 
    /// 功能：
    /// 1. 波次开始/完成的UI动画提示
    /// 2. Boss登场警告
    /// 3. 击杀连击计数器
    /// 4. 金币飘出效果
    /// 5. 全屏闪光（危险警告/通关庆祝）
    /// </summary>
    public class BattleFieldVFX : MonoSingleton<BattleFieldVFX>
    {
        // ========== 运行时数据 ==========

        /// <summary>连击计数器</summary>
        private int _killCombo = 0;
        private float _comboTimer = 0f;
        private const float ComboResetTime = 2f; // 2秒内无击杀则重置连击

        /// <summary>波次提示文字对象</summary>
        private GameObject _waveTextObj;
        private TextMesh _waveTextMesh;

        /// <summary>连击文字对象</summary>
        private GameObject _comboTextObj;
        private TextMesh _comboTextMesh;

        // ========== 生命周期 ==========

        protected override void OnInit()
        {
            CreateUIElements();

            EventBus.Instance.Subscribe<WaveStartEvent>(OnWaveStart);
            EventBus.Instance.Subscribe<WaveCompleteEvent>(OnWaveComplete);
            EventBus.Instance.Subscribe<AllWavesClearedEvent>(OnAllWavesCleared);
            EventBus.Instance.Subscribe<EnemyDeathEvent>(OnEnemyDeath);
            EventBus.Instance.Subscribe<BaseHealthChangedEvent>(OnBaseHealthChanged);

            Logger.I("BattleFieldVFX", "战场视觉效果管理器初始化");
        }

        protected override void OnDispose()
        {
            EventBus.Instance.Unsubscribe<WaveStartEvent>(OnWaveStart);
            EventBus.Instance.Unsubscribe<WaveCompleteEvent>(OnWaveComplete);
            EventBus.Instance.Unsubscribe<AllWavesClearedEvent>(OnAllWavesCleared);
            EventBus.Instance.Unsubscribe<EnemyDeathEvent>(OnEnemyDeath);
            EventBus.Instance.Unsubscribe<BaseHealthChangedEvent>(OnBaseHealthChanged);
        }

        private void Update()
        {
            // 连击计时器
            if (_killCombo > 0)
            {
                _comboTimer -= Time.deltaTime;
                if (_comboTimer <= 0f)
                {
                    _killCombo = 0;
                    HideComboText();
                }
            }
        }

        // ========== 事件处理 ==========

        /// <summary>波次开始</summary>
        private void OnWaveStart(WaveStartEvent evt)
        {
            _killCombo = 0;

            if (evt.IsBoss)
            {
                StartCoroutine(ShowBossWarning(evt.WaveIndex));
            }
            else if (evt.IsElite)
            {
StartCoroutine(ShowWaveText($"! 精英波 #{evt.WaveIndex}", new Color(0.8f, 0.4f, 1f), 2f));

            }
            else
            {
                StartCoroutine(ShowWaveText($"第 {evt.WaveIndex}/{evt.TotalWaves} 波", Color.white, 1.5f));
            }
        }

        /// <summary>波次完成</summary>
        private void OnWaveComplete(WaveCompleteEvent evt)
        {
            StartCoroutine(ShowWaveText($"第 {evt.WaveIndex} 波完成!", new Color(0.3f, 1f, 0.5f), 1.5f));
        }

        /// <summary>全部波次完成</summary>
        private void OnAllWavesCleared(AllWavesClearedEvent evt)
        {
            StartCoroutine(ShowVictoryAnimation());
        }

        /// <summary>怪物死亡 → 连击</summary>
        private void OnEnemyDeath(EnemyDeathEvent evt)
        {
            _killCombo++;
            _comboTimer = ComboResetTime;

            if (_killCombo >= 3)
            {
                ShowComboText(_killCombo);
            }

            // 金币飘出效果
            if (evt.GoldDrop > 0)
            {
                SpawnGoldEffect(evt.Position, evt.GoldDrop);
            }
        }

        /// <summary>基地受伤 → 屏幕闪红</summary>
        private void OnBaseHealthChanged(BaseHealthChangedEvent evt)
        {
            if (evt.Damage > 0)
            {
                StartCoroutine(ScreenFlashRed());
            }
        }

        // ========== 波次文字动画 ==========

        private IEnumerator ShowWaveText(string text, Color color, float duration)
        {
            if (_waveTextMesh == null) yield break;

            _waveTextObj.SetActive(true);
            _waveTextMesh.text = text;
            _waveTextMesh.color = new Color(color.r, color.g, color.b, 0f);

            // 位置跟随摄像机中心
            UpdateWaveTextPosition();

            float elapsed = 0f;
            float fadeInTime = 0.3f;
            float holdTime = duration - 0.6f;
            float fadeOutTime = 0.3f;

            // 淡入
            while (elapsed < fadeInTime)
            {
                float t = elapsed / fadeInTime;
                _waveTextMesh.color = new Color(color.r, color.g, color.b, t);
                transform.localScale = Vector3.one * Mathf.Lerp(1.5f, 1f, t);
                UpdateWaveTextPosition();
                elapsed += Time.deltaTime;
                yield return null;
            }

            // 保持
            _waveTextMesh.color = color;
            elapsed = 0f;
            while (elapsed < holdTime)
            {
                UpdateWaveTextPosition();
                elapsed += Time.deltaTime;
                yield return null;
            }

            // 淡出
            elapsed = 0f;
            while (elapsed < fadeOutTime)
            {
                float t = elapsed / fadeOutTime;
                _waveTextMesh.color = new Color(color.r, color.g, color.b, 1f - t);
                UpdateWaveTextPosition();
                elapsed += Time.deltaTime;
                yield return null;
            }

            _waveTextObj.SetActive(false);
        }

        /// <summary>Boss警告动画</summary>
        private IEnumerator ShowBossWarning(int waveIndex)
        {
            // 第一阶段：大字闪烁 "!! WARNING"
            yield return ShowWaveText("!! WARNING", new Color(1f, 0.2f, 0.1f), 1.5f);

            yield return new WaitForSeconds(0.2f);

            // 第二阶段：显示Boss名
            yield return ShowWaveText($"BOSS 来袭!", new Color(1f, 0.5f, 0f), 2f);
        }

        /// <summary>胜利动画</summary>
        private IEnumerator ShowVictoryAnimation()
        {
            yield return ShowWaveText("✦ VICTORY ✦", new Color(1f, 0.9f, 0.3f), 3f);
        }

        // ========== 连击文字 ==========

        private void ShowComboText(int combo)
        {
            if (_comboTextMesh == null) return;

            _comboTextObj.SetActive(true);

            string comboText;
            Color comboColor;

            if (combo >= 20)
            {
comboText = $"* {combo} COMBO! *";

                comboColor = new Color(1f, 0.3f, 0f);
            }
            else if (combo >= 10)
            {
comboText = $"! {combo} COMBO!";

                comboColor = new Color(1f, 0.6f, 0f);
            }
            else if (combo >= 5)
            {
                comboText = $"{combo} Combo!";
                comboColor = new Color(1f, 0.85f, 0.3f);
            }
            else
            {
                comboText = $"{combo} Combo";
                comboColor = new Color(0.9f, 0.9f, 0.9f);
            }

            _comboTextMesh.text = comboText;
            _comboTextMesh.color = comboColor;

            // 缩放弹跳效果
            float scale = 1f + Mathf.Min(combo * 0.02f, 0.5f);
            _comboTextObj.transform.localScale = Vector3.one * scale * 0.12f;

            UpdateComboTextPosition();
        }

        private void HideComboText()
        {
            if (_comboTextObj != null)
            {
                _comboTextObj.SetActive(false);
            }
        }

        // ========== 金币飘出效果 ==========

        private void SpawnGoldEffect(Vector3 position, int goldAmount)
        {
            // 生成金币飘字
            if (DamagePopupManager.HasInstance)
            {
                DamagePopupManager.Instance.ShowPopup($"+{goldAmount}", position + Vector3.up * 0.3f, PopupType.Gold);
            }
        }

        // ========== 屏幕闪红 ==========

        private IEnumerator ScreenFlashRed()
        {
            // 简单实现：创建一个覆盖屏幕的半透明红色对象
            var flashObj = new GameObject("ScreenFlash");
            var sr = flashObj.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 200;

            // 创建大方块Sprite
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1);

            // 覆盖屏幕
            if (Camera.main != null)
            {
                flashObj.transform.position = new Vector3(
                    Camera.main.transform.position.x,
                    Camera.main.transform.position.y,
                    0f
                );
                float camHeight = Camera.main.orthographicSize * 2f;
                float camWidth = camHeight * Camera.main.aspect;
                flashObj.transform.localScale = new Vector3(camWidth, camHeight, 1f);
            }

            // 闪烁淡出
            float duration = 0.3f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float alpha = Mathf.Lerp(0.3f, 0f, elapsed / duration);
                sr.color = new Color(1f, 0f, 0f, alpha);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Destroy(flashObj);
        }

        // ========== UI元素创建 ==========

        private void CreateUIElements()
        {
            // 波次提示文字
            _waveTextObj = new GameObject("WaveText");
            _waveTextObj.transform.SetParent(transform);
            _waveTextMesh = _waveTextObj.AddComponent<TextMesh>();
            _waveTextMesh.alignment = TextAlignment.Center;
            _waveTextMesh.anchor = TextAnchor.MiddleCenter;
            _waveTextMesh.fontSize = 64;
            _waveTextMesh.characterSize = 0.12f;
            _waveTextMesh.fontStyle = FontStyle.Bold;
            var waveMR = _waveTextObj.GetComponent<MeshRenderer>();
            if (waveMR != null) waveMR.sortingOrder = 150;
            _waveTextObj.SetActive(false);

            // 连击文字
            _comboTextObj = new GameObject("ComboText");
            _comboTextObj.transform.SetParent(transform);
            _comboTextMesh = _comboTextObj.AddComponent<TextMesh>();
            _comboTextMesh.alignment = TextAlignment.Center;
            _comboTextMesh.anchor = TextAnchor.MiddleCenter;
            _comboTextMesh.fontSize = 48;
            _comboTextMesh.characterSize = 0.12f;
            _comboTextMesh.fontStyle = FontStyle.Bold;
            var comboMR = _comboTextObj.GetComponent<MeshRenderer>();
            if (comboMR != null) comboMR.sortingOrder = 145;
            _comboTextObj.SetActive(false);
        }

        private void UpdateWaveTextPosition()
        {
            if (_waveTextObj == null || Camera.main == null) return;
            _waveTextObj.transform.position = new Vector3(
                Camera.main.transform.position.x,
                Camera.main.transform.position.y + Camera.main.orthographicSize * 0.3f,
                0f
            );
        }

        private void UpdateComboTextPosition()
        {
            if (_comboTextObj == null || Camera.main == null) return;
            _comboTextObj.transform.position = new Vector3(
                Camera.main.transform.position.x + Camera.main.orthographicSize * Camera.main.aspect * 0.6f,
                Camera.main.transform.position.y + Camera.main.orthographicSize * 0.6f,
                0f
            );
        }
    }
}
