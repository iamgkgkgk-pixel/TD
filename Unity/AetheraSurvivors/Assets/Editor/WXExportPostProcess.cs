using UnityEngine;
using System.IO;
using System.Text;

namespace WeChatWASM
{
    /// <summary>
    /// 微信小游戏导出后处理：自动修复 game.json 的分包配置
    /// 解决每次 Unity 导出后 subpackages 配置丢失导致包体超 4MB 的问题
    /// </summary>
    public class WXExportPostProcess : LifeCycleBase
    {
        public override void afterBuildTemplate()
        {
            FixGameJson();
        }

        public override void exportDone()
        {
            FixGameJson();
        }

        private void FixGameJson()
        {
            // 获取导出目录
            string dstDir = BuildTemplateHelper.DstMinigameDir;
            if (string.IsNullOrEmpty(dstDir))
            {
                Debug.LogWarning("[WXExportPostProcess] 无法获取导出目录，跳过 game.json 修复");
                return;
            }

            string gameJsonPath = Path.Combine(dstDir, "game.json");
            if (!File.Exists(gameJsonPath))
            {
                Debug.LogWarning("[WXExportPostProcess] game.json 不存在: " + gameJsonPath);
                return;
            }

            try
            {
                string content = File.ReadAllText(gameJsonPath, Encoding.UTF8);
                var gameJson = LitJson.JsonMapper.ToObject(content);

                bool modified = false;

                // 检查并添加 subpackages 配置
                if (!gameJson.ContainsKey("subpackages"))
                {
                    Debug.Log("[WXExportPostProcess] game.json 缺少 subpackages 配置，正在添加...");

                    var subpackages = new LitJson.JsonData();
                    subpackages.SetJsonType(LitJson.JsonType.Array);

                    var wasmcode = new LitJson.JsonData();
                    wasmcode["name"] = "wasmcode";
                    wasmcode["root"] = "wasmcode/";
                    subpackages.Add(wasmcode);

                    var dataPackage = new LitJson.JsonData();
                    dataPackage["name"] = "data-package";
                    dataPackage["root"] = "data-package/";
                    subpackages.Add(dataPackage);

                    gameJson["subpackages"] = subpackages;
                    modified = true;
                }

                // 检查并添加 parallelPreloadSubpackages 配置
                if (!gameJson.ContainsKey("parallelPreloadSubpackages"))
                {
                    Debug.Log("[WXExportPostProcess] game.json 缺少 parallelPreloadSubpackages 配置，正在添加...");

                    var parallel = new LitJson.JsonData();
                    parallel.SetJsonType(LitJson.JsonType.Array);

                    var wasmPreload = new LitJson.JsonData();
                    wasmPreload["name"] = "wasmcode";
                    parallel.Add(wasmPreload);

                    var dataPreload = new LitJson.JsonData();
                    dataPreload["name"] = "data-package";
                    parallel.Add(dataPreload);

                    gameJson["parallelPreloadSubpackages"] = parallel;
                    modified = true;
                }

                if (modified)
                {
                    var writer = new LitJson.JsonWriter();
                    writer.IndentValue = 2;
                    writer.PrettyPrint = true;
                    gameJson.ToJson(writer);
                    File.WriteAllText(gameJsonPath, writer.TextWriter.ToString(), Encoding.UTF8);
                    Debug.Log("[WXExportPostProcess] ✅ game.json 分包配置已自动修复: " + gameJsonPath);
                }
                else
                {
                    Debug.Log("[WXExportPostProcess] ✅ game.json 分包配置正常，无需修复");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[WXExportPostProcess] 修复 game.json 失败: " + e.Message);
            }
        }
    }
}
