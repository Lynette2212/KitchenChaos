using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace KitchenChaos.EditorTools
{
    public static class ChefModelImportTools
    {
        private const string ModelPath = "Assets/Resources/Art/Chef/Chef.fbx";
        private const string TexturePath = "Assets/Resources/Art/Chef/Chef_BaseColor.jpg";

        [MenuItem("Kitchen Chaos/Inspect Chef Model")]
        public static void Inspect()
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                Debug.LogError("CHEF MODEL: missing " + ModelPath);
                return;
            }
            var sb = new StringBuilder();
            sb.AppendLine("CHEF MODEL REPORT");
            sb.AppendLine("name=" + model.name);
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Bounds bounds = renderer.bounds;
                sb.AppendLine("renderer=" + renderer.name + " type=" + renderer.GetType().Name
                    + " bounds=" + bounds.size + " center=" + bounds.center
                    + " materials=" + renderer.sharedMaterials.Length);
                foreach (Material material in renderer.sharedMaterials)
                    sb.AppendLine("  material=" + (material != null ? material.name + " shader=" + material.shader.name : "null"));
            }
            foreach (SkinnedMeshRenderer skinned in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                sb.AppendLine("skinned=" + skinned.name + " bones=" + skinned.bones.Length + " rootBone=" + (skinned.rootBone != null ? skinned.rootBone.name : "none"));
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
                sb.AppendLine("node=" + t.name + " depth=" + Depth(t, model.transform));
            int animatorCount = model.GetComponentsInChildren<Animator>(true).Length;
            sb.AppendLine("animators=" + animatorCount);
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
            {
                if (asset is AnimationClip clip) sb.AppendLine("clip=" + clip.name + " length=" + clip.length);
                else if (asset is Avatar avatar) sb.AppendLine("avatar=" + avatar.name + " human=" + avatar.isHuman);
            }
            Debug.Log(sb.ToString());
        }

        [MenuItem("Kitchen Chaos/Configure Chef Model")]
        public static void Configure()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            if (importer == null)
            {
                Debug.LogError("CHEF MODEL: importer missing");
                return;
            }
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "ChefSkin" };
            if (texture != null) material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Smoothness", 0.25f);
            string materialPath = "Assets/Resources/Art/Chef/ChefSkin.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(materialPath) != null)
                AssetDatabase.DeleteAsset(materialPath);
            AssetDatabase.CreateAsset(material, materialPath);
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model != null)
            {
                foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] mats = new Material[renderer.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++) mats[i] = material;
                    renderer.sharedMaterials = mats;
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("CHEF MODEL: configured texture=" + (texture != null) + " material=" + materialPath);
        }

        private static int Depth(Transform t, Transform root)
        {
            int depth = 0;
            while (t != null && t != root) { t = t.parent; depth++; }
            return depth;
        }
    }
}
