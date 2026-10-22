using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace KitchenChaos.EditorTools
{
    /// <summary>
    /// 将工程从已弃用的内置渲染管线（Built-in RP）一键迁移到 URP。
    /// - 创建 ForwardRendererData + UniversalRenderPipelineAsset
    /// - 指定给 GraphicsSettings.defaultRenderPipeline 与当前质量等级
    /// 首次打开工程（导入 URP 包后）会自动执行一次，也可通过菜单 Tools/Kitchen Chaos/Migrate to URP 手动运行。
    /// 材质均为运行时 VisualFactory 生成（优先 URP/Lit），无需材质升级。
    /// </summary>
    [InitializeOnLoad]
    public static class UrpMigrator
    {
        private const string Folder = "Assets/Settings";
        private const string RendererPath = Folder + "/KitchenForwardRenderer.asset";
        private const string PipelinePath = Folder + "/KitchenURP.asset";
        private const string FlagKey = "KitchenChaos.URPMigrationDone";

        static UrpMigrator()
        {
            // 延迟到编辑器就绪后执行，避免包还在导入时类型不可用
            EditorApplication.delayCall += AutoRun;
        }

        private static void AutoRun()
        {
            if (SessionState.GetBool(FlagKey, false)) return;
            SessionState.SetBool(FlagKey, true);

            if (HasUrpAsset())
            {
                EnsureAssigned();
                return;
            }

            // 找不到 URP 程序集说明包尚未导入完成，本次跳过（下次会话再试）
            if (!UrpAvailable()) return;

            try
            {
                Migrate();
                Debug.Log("[KitchenChaos] 已自动迁移到 URP（通用渲染管线），内置管线弃用提示已消除。");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[KitchenChaos] URP 自动迁移未完成，可稍后用菜单 Tools/Kitchen Chaos/Migrate to URP 重试：" + e.Message);
            }
        }

        [MenuItem("Tools/Kitchen Chaos/Migrate to URP")]
        public static void MigrateMenu()
        {
            if (!UrpAvailable())
            {
                EditorUtility.DisplayDialog("迁移到 URP",
                    "尚未找到 URP 包，请确认 com.unity.render-pipelines.universal 已导入完成后再试。", "好");
                return;
            }
            Migrate();
            EditorUtility.DisplayDialog("迁移到 URP", "完成！已创建并启用 URP 渲染管线资产。", "好");
        }

        /// <summary>命令行入口：-executeMethod KitchenChaos.EditorTools.UrpMigrator.RunFromCli</summary>
        public static void RunFromCli()
        {
            if (!UrpAvailable())
                throw new System.InvalidOperationException("URP runtime assembly not available yet.");
            // CLI 入口始终强制重建，确保用官方工厂生成带完整默认通道的正确渲染器资产
            Migrate();
            Debug.Log("[KitchenChaos] CLI：URP 迁移完成。");
            AssetDatabase.SaveAssets();
        }

        public static void Migrate()
        {
            Directory.CreateDirectory(Folder);

            // URP 类型通过反射获取，避免在包缺失时编译失败
            Assembly urp = Assembly.Load("Unity.RenderPipelines.Universal.Runtime");
            System.Type pipelineType = urp.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset");

            // 关键：必须用 URP 官方工厂生成渲染器，它会写入完整的默认渲染通道。
            // 之前反射 new ForwardRendererData 会得到空通道的已弃用资产，升级后导致 RenderCameraStack 空引用、整帧黑屏。
            // 先删除任何可能损坏的旧资产，确保重建出正确配置。
            DeleteIfExists(RendererPath);
            DeleteIfExists(PipelinePath);
            AssetDatabase.Refresh();

            // 1) 用官方工厂创建配置正确的 UniversalRendererData
            MethodInfo createRenderer = pipelineType.GetMethod(
                "CreateRendererAsset",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                null,
                new[] { typeof(string), urp.GetType("UnityEngine.Rendering.Universal.RendererType"), typeof(bool), typeof(string) },
                null);
            if (createRenderer == null)
                throw new System.InvalidOperationException("未找到 UniversalRenderPipelineAsset.CreateRendererAsset，URP 版本可能不兼容。");

            object rendererTypeEnum = System.Enum.Parse(urp.GetType("UnityEngine.Rendering.Universal.RendererType"), "UniversalRenderer");
            // relativePath=false：直接使用给定完整路径，不自动加后缀
            ScriptableObject rendererAsset =
                (ScriptableObject)createRenderer.Invoke(null, new object[] { RendererPath, rendererTypeEnum, false, "Renderer" });
            if (rendererAsset == null)
                throw new System.InvalidOperationException("CreateRendererAsset 返回空。");

            // 2) 用该渲染器创建管线资产（官方 Create 会把渲染器挂进 m_RendererDataList[0]）
            MethodInfo createPipeline = pipelineType.GetMethod(
                "Create",
                BindingFlags.Static | BindingFlags.Public,
                null, new[] { urp.GetType("UnityEngine.Rendering.Universal.ScriptableRendererData") }, null);
            ScriptableObject pipelineAsset;
            if (createPipeline != null)
            {
                pipelineAsset = (ScriptableObject)createPipeline.Invoke(null, new object[] { rendererAsset });
            }
            else
            {
                // 兜底：无参 Create + 手动挂渲染器
                pipelineAsset = (ScriptableObject)ScriptableObject.CreateInstance(pipelineType);
                FieldInfo rf = pipelineType.GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
                System.Array arr = System.Array.CreateInstance(urp.GetType("UnityEngine.Rendering.Universal.ScriptableRendererData"), 1);
                arr.SetValue(rendererAsset, 0);
                rf.SetValue(pipelineAsset, arr);
            }
            AssetDatabase.CreateAsset(pipelineAsset, PipelinePath);

            EditorUtility.SetDirty(rendererAsset);
            EditorUtility.SetDirty(pipelineAsset);
            AssetDatabase.SaveAssets();

            AssignPipeline(pipelineAsset as RenderPipelineAsset);
        }

        private static void DeleteIfExists(string assetPath)
        {
            if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                AssetDatabase.DeleteAsset(assetPath);
        }

        private static void AssignPipeline(RenderPipelineAsset asset)
        {
            if (asset == null) throw new System.InvalidOperationException("无法分配空的渲染管线资产。");
            GraphicsSettings.defaultRenderPipeline = asset;
            // 强制覆盖当前质量等级，避免旧设置覆盖图形设置
            QualitySettings.renderPipeline = asset;
            AssetDatabase.SaveAssets();
        }

        private static bool HasUrpAsset()
        {
            return GraphicsSettings.defaultRenderPipeline != null
                   || AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(PipelinePath) != null;
        }

        private static void EnsureAssigned()
        {
            RenderPipelineAsset asset = GraphicsSettings.defaultRenderPipeline;
            if (asset == null)
            {
                asset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(PipelinePath);
                if (asset != null)
                {
                    GraphicsSettings.defaultRenderPipeline = asset;
                    if (QualitySettings.renderPipeline == null)
                        QualitySettings.renderPipeline = asset;
                }
            }
        }

        private static bool UrpAvailable()
        {
            foreach (Assembly asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name == "Unity.RenderPipelines.Universal.Runtime")
                {
                    return asm.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset") != null;
                }
            }
            return false;
        }
    }
}
