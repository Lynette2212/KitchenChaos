using System.Collections.Generic;
using UnityEngine;

namespace KitchenChaos.Gameplay
{
    /// <summary>
    /// 挂在可交互柜台上：玩家靠近并选中时整体微微提亮，离开后恢复。
    /// 用材质自发光实现，兼容 URP / 内置管线（存在 _EmissionColor 属性时生效）。
    /// </summary>
    public class InteractableHighlighter : MonoBehaviour
    {
        public Color highlightColor = new Color(1f, 0.85f, 0.4f);
        public float highlightIntensity = 0.35f;

        private readonly List<Material> materials = new List<Material>();
        private bool highlighted;

        private void Awake()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                // material（非 shared）保证只影响本实例
                foreach (Material m in r.materials)
                {
                    if (m != null) materials.Add(m);
                }
            }
        }

        public void SetHighlight(bool on)
        {
            if (on == highlighted) return;
            highlighted = on;

            foreach (Material m in materials)
            {
                if (m == null) continue;
                if (m.HasProperty("_EmissionColor"))
                {
                    if (on)
                    {
                        m.EnableKeyword("_EMISSION");
                        m.SetColor("_EmissionColor", highlightColor * highlightIntensity);
                    }
                    else
                    {
                        m.SetColor("_EmissionColor", Color.black);
                    }
                }
            }
        }
    }
}
