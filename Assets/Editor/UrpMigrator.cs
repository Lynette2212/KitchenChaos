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
            if (HasUrpAsset())
            {
                EnsureAssigned();
                Debug.Log("[KitchenChaos] URP 资产已存在并已启用，跳过迁移。");
                return;
            }
            if (!UrpAvailable())
                throw new System.InvalidOperationException("URP runtime assembly not available yet.");
            Migrate();
            Debug.Log("[KitchenChaos] CLI：URP 迁移完成。");
            AssetDatabase.SaveAssets();
        }

        public static void Migrate()
        {
            Directory.CreateDirectory(Folder);

            // URP 类型通过反射获取，避免在包缺失时编译失败
            Assembly urp = Assembly.Load("Unity.RenderPipelines.Universal.Runtime");
            System.Type rendererDataType = urp.GetType("UnityEngine.Rendering.Universal.ForwardRendererData");
            System.Type pipelineType = urp.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset");

            // 1) Forward Renderer
            Object rendererAsset = LoadOrCreate(RendererPath, rendererDataType);

            // 2) Pipeline Asset
            Object pipelineAsset = LoadOrCreate(PipelinePath, pipelineType);

            // 把 ForwardRenderer 挂进管线资产的 renderer 列表（元素须为强类型数组）
            FieldInfo renderersField = pipelineType.GetField("m_RendererDataList",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (renderersField != null)
            {
                System.Type elemType = renderersField.FieldType.GetElementType();
                System.Array list = System.Array.CreateInstance(elemType, 1);
                list.SetValue(rendererAsset, 0);
                renderersField.SetValue(pipelineAsset, list);
            }
            FieldInfo defaultRendererIndex = pipelineType.GetField("m_DefaultRendererIndex",
                BindingFlags.NonPublic | BindingFlags.Instance);
            defaultRendererIndex?.SetValue(pipelineAsset, 0);

            EditorUtility.SetDirty(rendererAsset);
            EditorUtility.SetDirty(pipelineAsset);
            AssetDatabase.SaveAssets();

            AssignPipeline(pipelineAsset);
        }

        private static Object LoadOrCreate(string path, System.Type type)
        {
            Object existing = AssetDatabase.LoadAssetAtPath(path, typeof(ScriptableObject));
            if (existing != null) return existing;

            Object asset = ScriptableObject.CreateInstance(type);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void AssignPipeline(Object pipelineAsset)
        {
            RenderPipelineAsset asset = pipelineAsset as RenderPipelineAsset;
            GraphicsSettings.defaultRenderPipeline = asset;

            // 当前质量等级也指定一份，否则质量设置可能覆盖图形设置
            if (QualitySettings.renderPipeline == null)
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
