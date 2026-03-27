// ============================================================
// 文件名：GMPanel.cs
// 功能描述：Unity Editor扩展 — GM调试面板
//          运行时修改数值、跳关卡、加货币、解锁全部
// 创建时间：2026-03-25
// 所属模块：Editor/GMPanel
// 对应交互：阶段二 #70
// ============================================================

#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using AetheraSurvivors.Platform;

namespace AetheraSurvivors.Editor
{

    /// <summary>
    /// GM调试面板 — Unity Editor扩展
    /// 
    /// 功能：
    /// 1. 运行时修改玩家资源（金币/钻石/体力）
    /// 2. 跳关卡（跳到指定章节关卡）
    /// 3. 解锁全部内容（所有英雄/关卡/成就）
    /// 4. 修改战斗参数（伤害倍率/无敌模式/秒杀模式）
    /// 5. 重置存档（清除本地数据）
    /// 6. 查看运行时状态（Manager状态、内存使用等）
    /// 
    /// 打开方式：
    ///   Unity菜单 → AetheraSurvivors → GM Panel
    /// 
    /// ⚠️ 注意：
    ///   此面板仅在编辑器中可用，不会打包到发布版本
    /// </summary>
    public class GMPanel : EditorWindow
    {
        // ========== 状态 ==========

        /// <summary>滚动位置</summary>
        private Vector2 _scrollPos;

        /// <summary>展开折叠状态</summary>
        private bool _foldResource = true;
        private bool _foldBattle = true;
        private bool _foldLevel = true;
        private bool _foldSystem = true;
        private bool _foldDebug = true;

        /// <summary>GM输入值</summary>
        private int _addGold = 10000;
        private int _addDiamond = 1000;
        private int _addStamina = 100;
        private int _targetChapter = 1;
        private int _targetLevel = 1;
        private float _damageMultiplier = 1f;
        private float _speedMultiplier = 1f;
        private bool _godMode = false;
        private bool _oneHitKill = false;

        // ========== 菜单入口 ==========

        [MenuItem("AetheraSurvivors/GM调试面板 (GM Panel)")]
        public static void ShowWindow()
        {
            var window = GetWindow<GMPanel>("GM调试面板");
            window.minSize = new Vector2(350, 500);
            window.Show();
        }

        // ========== GUI ==========

        private void OnGUI()
        {
            // 标题
            EditorGUILayout.LabelField("═══ GM 调试面板 ═══", EditorStyles.boldLabel);

            // 运行状态提示
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("⚠️ 大部分功能需要在Play Mode下使用", MessageType.Warning);
            }

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            // ====== 资源修改 ======
            _foldResource = EditorGUILayout.Foldout(_foldResource, "💰 资源修改", true, EditorStyles.foldoutHeader);
            if (_foldResource)
            {
                EditorGUI.indentLevel++;
                DrawResourceSection();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);

            // ====== 战斗调试 ======
            _foldBattle = EditorGUILayout.Foldout(_foldBattle, "⚔️ 战斗调试", true, EditorStyles.foldoutHeader);
            if (_foldBattle)
            {
                EditorGUI.indentLevel++;
                DrawBattleSection();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);

            // ====== 关卡跳转 ======
            _foldLevel = EditorGUILayout.Foldout(_foldLevel, "🗺️ 关卡跳转", true, EditorStyles.foldoutHeader);
            if (_foldLevel)
            {
                EditorGUI.indentLevel++;
                DrawLevelSection();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);

            // ====== 系统操作 ======
            _foldSystem = EditorGUILayout.Foldout(_foldSystem, "⚙️ 系统操作", true, EditorStyles.foldoutHeader);
            if (_foldSystem)
            {
                EditorGUI.indentLevel++;
                DrawSystemSection();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);

            // ====== 调试信息 ======
            _foldDebug = EditorGUILayout.Foldout(_foldDebug, "📊 运行时信息", true, EditorStyles.foldoutHeader);
            if (_foldDebug)
            {
                EditorGUI.indentLevel++;
                DrawDebugSection();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>绘制资源修改区域</summary>
        private void DrawResourceSection()
        {
            EditorGUILayout.BeginHorizontal();
            _addGold = EditorGUILayout.IntField("金币数量", _addGold);
            GUI.enabled = Application.isPlaying;
            if (GUILayout.Button("+金币", GUILayout.Width(60)))
            {
                GMAddGold(_addGold);
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            _addDiamond = EditorGUILayout.IntField("钻石数量", _addDiamond);
            GUI.enabled = Application.isPlaying;
            if (GUILayout.Button("+钻石", GUILayout.Width(60)))
            {
                GMAddDiamond(_addDiamond);
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            _addStamina = EditorGUILayout.IntField("体力数量", _addStamina);
            GUI.enabled = Application.isPlaying;
            if (GUILayout.Button("+体力", GUILayout.Width(60)))
            {
                GMAddStamina(_addStamina);
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);
            GUI.enabled = Application.isPlaying;
            if (GUILayout.Button("💎 资源全满"))
            {
                GMAddGold(999999);
                GMAddDiamond(99999);
                GMAddStamina(999);
            }
            GUI.enabled = true;
        }

        /// <summary>绘制战斗调试区域</summary>
        private void DrawBattleSection()
        {
            _damageMultiplier = EditorGUILayout.Slider("伤害倍率", _damageMultiplier, 0.1f, 100f);
            _speedMultiplier = EditorGUILayout.Slider("游戏速度", _speedMultiplier, 0.1f, 10f);

            EditorGUILayout.Space(3);
            _godMode = EditorGUILayout.Toggle("🛡️ 无敌模式", _godMode);
            _oneHitKill = EditorGUILayout.Toggle("⚡ 秒杀模式", _oneHitKill);

            EditorGUILayout.Space(3);

            GUI.enabled = Application.isPlaying;

            if (GUILayout.Button("应用战斗参数"))
            {
                GMApplyBattleParams();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("杀死所有怪物"))
            {
                GMKillAllEnemies();
            }
            if (GUILayout.Button("跳过当前波次"))
            {
                GMSkipWave();
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("直接通关"))
            {
                GMWinBattle();
            }

            GUI.enabled = true;
        }

        /// <summary>绘制关卡跳转区域</summary>
        private void DrawLevelSection()
        {
            _targetChapter = EditorGUILayout.IntSlider("目标章节", _targetChapter, 1, 30);
            _targetLevel = EditorGUILayout.IntSlider("目标关卡", _targetLevel, 1, 5);

            GUI.enabled = Application.isPlaying;

            if (GUILayout.Button($"🚀 跳转到 Ch{_targetChapter}-Lv{_targetLevel}"))
            {
                GMJumpToLevel(_targetChapter, _targetLevel);
            }

            EditorGUILayout.Space(3);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("解锁全部关卡"))
            {
                GMUnlockAllLevels();
            }
            if (GUILayout.Button("解锁全部英雄"))
            {
                GMUnlockAllHeroes();
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("🔓 解锁全部内容"))
            {
                GMUnlockAll();
            }

            GUI.enabled = true;
        }

        /// <summary>绘制系统操作区域</summary>
        private void DrawSystemSection()
        {
            GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
            if (GUILayout.Button("⚠️ 清除本地存档"))
            {
                if (EditorUtility.DisplayDialog("⚠️ 危险操作",
                    "这将清除所有本地存档数据！\n确定要继续吗？", "确定清除", "取消"))
                {
                    GMClearSave();
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(3);

            GUI.enabled = Application.isPlaying;
            if (GUILayout.Button("📊 强制上报所有埋点"))
            {
                GMFlushAnalytics();
            }
            if (GUILayout.Button("💾 强制保存所有数据"))
            {
                GMForceSave();
            }
            if (GUILayout.Button("🔄 重新同步服务器时间"))
            {
                GMSyncTime();
            }
            GUI.enabled = true;
        }

        /// <summary>绘制调试信息区域</summary>
        private void DrawDebugSection()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.LabelField("（需要在Play Mode下查看）");
                return;
            }

            // 系统信息
            EditorGUILayout.LabelField($"FPS: {(1f / Time.unscaledDeltaTime):F0}");
            EditorGUILayout.LabelField($"内存: {SystemInfo.systemMemorySize} MB (系统)");
            EditorGUILayout.LabelField($"GC内存: {(System.GC.GetTotalMemory(false) / 1024f / 1024f):F1} MB");
            EditorGUILayout.LabelField($"DrawCalls: {UnityStats.drawCalls}");
            EditorGUILayout.LabelField($"三角面: {UnityStats.triangles}");

            EditorGUILayout.Space(3);

            // Manager状态
            EditorGUILayout.LabelField("Manager状态:", EditorStyles.boldLabel);
            DrawManagerStatus("GameManager", Framework.GameManager.HasInstance);
            DrawManagerStatus("EventBus", Framework.EventBus.HasInstance);
            DrawManagerStatus("SaveManager", Framework.SaveManager.HasInstance);
            DrawManagerStatus("AudioManager", Framework.AudioManager.HasInstance);
            DrawManagerStatus("UIManager", Framework.UIManager.HasInstance);
            DrawManagerStatus("TimerManager", Framework.TimerManager.HasInstance);
            DrawManagerStatus("ResourceManager", Framework.ResourceManager.HasInstance);
            DrawManagerStatus("HttpClient", HttpClient.HasInstance);
            DrawManagerStatus("WXBridgeExtended", WXBridgeExtended.HasInstance);
            DrawManagerStatus("WXLogin", WXLogin.HasInstance);
            DrawManagerStatus("AnalyticsManager", AnalyticsManager.HasInstance);
            DrawManagerStatus("CrashReporter", CrashReporter.HasInstance);

            if (WXLogin.HasInstance)
            {
                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField("登录状态:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"  状态: {WXLogin.Instance.State}");
                EditorGUILayout.LabelField($"  用户ID: {WXLogin.Instance.UserId ?? "N/A"}");
            }
        }

        /// <summary>绘制Manager状态行</summary>
        private void DrawManagerStatus(string name, bool isActive)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"  {(isActive ? "✅" : "❌")} {name}");
            EditorGUILayout.EndHorizontal();
        }

        // ========== GM命令实现 ==========

        private void GMAddGold(int amount)
        {
            // 后续阶段四实现PlayerData后接入
            Debug.Log($"[GM] 添加金币: +{amount} （待PlayerData接入）");
        }

        private void GMAddDiamond(int amount)
        {
            Debug.Log($"[GM] 添加钻石: +{amount} （待PlayerData接入）");
        }

        private void GMAddStamina(int amount)
        {
            Debug.Log($"[GM] 添加体力: +{amount} （待PlayerData接入）");
        }

        private void GMApplyBattleParams()
        {
            Time.timeScale = _speedMultiplier;
            Debug.Log($"[GM] 应用战斗参数: 伤害={_damageMultiplier}x, 速度={_speedMultiplier}x, " +
                $"无敌={_godMode}, 秒杀={_oneHitKill}");
        }

        private void GMKillAllEnemies()
        {
            Debug.Log("[GM] 杀死所有怪物 （待BattleManager接入）");
        }

        private void GMSkipWave()
        {
            Debug.Log("[GM] 跳过当前波次 （待WaveManager接入）");
        }

        private void GMWinBattle()
        {
            Debug.Log("[GM] 直接通关 （待BattleManager接入）");
        }

        private void GMJumpToLevel(int chapter, int level)
        {
            Debug.Log($"[GM] 跳转到关卡: Ch{chapter}-Lv{level} （待LevelManager接入）");
        }

        private void GMUnlockAllLevels()
        {
            Debug.Log("[GM] 解锁全部关卡 （待PlayerData接入）");
        }

        private void GMUnlockAllHeroes()
        {
            Debug.Log("[GM] 解锁全部英雄 （待PlayerData接入）");
        }

        private void GMUnlockAll()
        {
            GMUnlockAllLevels();
            GMUnlockAllHeroes();
            Debug.Log("[GM] 解锁全部内容");
        }

        private void GMClearSave()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();

            if (Application.isPlaying && Framework.SaveManager.HasInstance)
            {
                Framework.SaveManager.Instance.ClearAll();
            }

            Debug.Log("[GM] ⚠️ 已清除全部本地存档");
        }

        private void GMFlushAnalytics()
        {
            if (AnalyticsManager.HasInstance)
            {
                AnalyticsManager.Instance.Flush();
                Debug.Log("[GM] 已强制上报所有埋点");
            }
        }

        private void GMForceSave()
        {
            if (Framework.SaveManager.HasInstance)
            {
                Framework.SaveManager.Instance.FlushAll();
                Debug.Log("[GM] 已强制保存所有数据");
            }
        }

        private void GMSyncTime()
        {
            if (TimeSync.HasInstance)
            {
                TimeSync.Instance.Sync((success) =>
                {
                    Debug.Log($"[GM] 时间同步: {(success ? "成功" : "失败")}");
                });
            }
        }
    }
}

#endif
