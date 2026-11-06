using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 墙面镂空控制器：挂载于主相机上，每帧检测相机与目标之间的墙体，
/// 动态替换材质并传递裁剪参数给 Shader Graph，实现非 Stencil 的透视效果。
/// </summary>
public class CutoutObject : MonoBehaviour
{
    [SerializeField] private Transform targetObject;   // 被追踪的目标（如玩家）
    [SerializeField] private LayerMask wallMask;       // 哪些层被视为"可镂空墙体"
    [SerializeField] private Shader cutoutShader;      // 备用裁剪着色器，用于替换不支持裁剪的原始材质

    private Camera mainCamera;

    // 缓存已创建的裁剪材质实例，避免每帧 new Material（GC 灾难）
    // Key: 原始 Renderer, Value: 对应的裁剪材质数组
    private readonly Dictionary<Renderer, Material[]> wallMaterials = new();

    // 保存每个 Renderer 的原始 sharedMaterials，用于恢复
    private readonly Dictionary<Renderer, Material[]> originalMaterials = new();

    // 本帧命中的墙体渲染器集合，每帧 Clear 后重新填充
    private readonly HashSet<Renderer> hitRenderers = new();

    private void Awake()
    {
        // ⚠️ 此脚本必须挂载在相机上，因为需要 WorldToViewportPoint 和射线起点
        mainCamera = GetComponent<Camera>();
    }

    private void Update()
    {
        // ===== 第一步：重置所有墙体的裁剪状态 =====
        // 先将所有缓存材质的 _CutoutSize 设为 0（不裁剪），再恢复原始材质
        // 这确保了上一帧被裁剪但本帧不再遮挡的墙体能正确还原
        CloseHoles();
        hitRenderers.Clear();

        if (mainCamera == null || targetObject == null) return;

        // ===== 第二步：计算目标的屏幕空间位置 =====
        // Shader Graph 中使用 Viewport 坐标（0~1范围），而非世界坐标
        // 这样裁剪区域始终跟随目标在屏幕上的投影，不受相机远近影响
        Vector2 cutoutPos = mainCamera.WorldToViewportPoint(targetObject.position);

        // ===== 第三步：从相机向目标发射射线，收集所有遮挡墙体 =====
        Vector3 offset = targetObject.position - transform.position;
        RaycastHit[] hitObjects = Physics.RaycastAll(
            transform.position, offset, offset.magnitude, wallMask);

        for (int i = 0; i < hitObjects.Length; ++i)
        {
            FindWallRenderers(hitObjects[i].collider.transform);
        }

        // ===== 第四步：对本帧命中的墙体应用裁剪参数 =====
        foreach (Renderer renderer in hitRenderers)
        {
            Material[] materials = GetWallMaterials(renderer);
            foreach (Material material in materials)
            {
                if (material == null || !material.HasProperty("_CutoutSize")) continue;

                // 传入屏幕空间裁剪中心点
                material.SetVector("_CutoutPos", cutoutPos);
                // 裁剪半径（Viewport 单位，0.1 ≈ 屏幕宽度的10%）
                material.SetFloat("_CutoutSize", 0.1f);
                // 边缘柔化宽度，配合 Dithering/Noise 实现渐变过渡而非硬边
                material.SetFloat("_FalloffSize", 0.05f);
            }
        }
    }

    /// <summary>
    /// 从碰撞体所在 Transform 向上查找墙体根节点，再向下收集所有墙体 Renderer
    /// </summary>
    private void FindWallRenderers(Transform hit)
    {
        // 模块化墙体预制体结构：Collider_* 与可视 Mesh 是兄弟节点
        // 先向上找到 Walls 层的边界父节点，再从其下收集所有 Renderer
        // 这样即使射线只击中了碰撞体子物体，也能找到同级的可视网格
        for (Transform root = hit; root != null && IsWall(root.gameObject); root = root.parent)
        {
            bool found = false;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
            {
                // 跳过非墙体层物体和零尺寸渲染器（如空占位符）
                if (!IsWall(renderer.gameObject) || renderer.bounds.size.sqrMagnitude == 0) continue;
                if (renderer.sharedMaterials.Length == 0) continue;

                hitRenderers.Add(renderer); // HashSet 自动去重
                found = true;
            }
            // 找到当前层级的墙体 Renderer 后立即停止向上搜索
            // 防止越界到非墙体父级（如整个关卡根节点）
            if (found) return;
        }
    }

    /// <summary>
    /// 位运算检查 GameObject 是否在 wallMask 中
    /// </summary>
    private bool IsWall(GameObject item)
    {
        return (wallMask.value & (1 << item.layer)) != 0;
    }

    /// <summary>
    /// 获取或创建裁剪材质实例，首次调用时克隆/替换材质并缓存
    /// </summary>
    private Material[] GetWallMaterials(Renderer renderer)
    {
        // 命中缓存：直接返回并重新赋值 sharedMaterials
        // （CloseHoles 中已恢复为原始材质，此处需再次切换回裁剪材质）
        if (wallMaterials.TryGetValue(renderer, out Material[] cached))
        {
            renderer.sharedMaterials = cached;
            return cached;
        }

        // 首次遇到此 Renderer：创建裁剪材质实例
        Material[] originals = renderer.sharedMaterials;
        Material[] instances = new Material[originals.Length];

        for (int i = 0; i < originals.Length; ++i)
        {
            Material source = originals[i];
            if (source == null) continue;

            // 判断原始材质是否已支持裁剪属性
            bool convert = !source.HasProperty("_CutoutSize") && cutoutShader != null;

            // 不支持裁剪 → 用 cutoutShader 创建全新材质
            // 已支持裁剪 → 克隆原始材质（保留原有参数）
            Material instance = convert ? new Material(cutoutShader) : new Material(source);

            if (convert)
            {
                // 将原始材质的纹理信息迁移到裁剪材质
                // 兼容 URP (_BaseMap) 和 Built-in (_MainTex) 命名
                string textureProperty = source.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
                if (source.HasProperty(textureProperty))
                {
                    instance.SetTexture("_MainTexture", source.GetTexture(textureProperty));
                    instance.SetVector("_Tiling", source.GetTextureScale(textureProperty));
                    instance.SetVector("_Offset", source.GetTextureOffset(textureProperty));
                }
            }
            instances[i] = instance;
        }

        // 双向缓存：原始材质用于恢复，裁剪材质用于后续帧复用
        originalMaterials.Add(renderer, originals);
        wallMaterials.Add(renderer, instances);
        renderer.sharedMaterials = instances;
        return instances;
    }

    /// <summary>
    /// 关闭所有镂空：将裁剪尺寸归零，并恢复所有 Renderer 的原始材质
    /// </summary>
    private void CloseHoles()
    {
        // 归零裁剪尺寸（防御性操作，确保即使材质未恢复也不会残留镂空）
        foreach (Material[] materials in wallMaterials.Values)
            foreach (Material material in materials)
                if (material != null && material.HasProperty("_CutoutSize"))
                    material.SetFloat("_CutoutSize", 0f);

        // 恢复原始 sharedMaterials，断开裁剪材质与 Renderer 的关联
        foreach (var entry in originalMaterials)
            if (entry.Key != null) entry.Key.sharedMaterials = entry.Value;
    }

    // 禁用时立即恢复，防止编辑器暂停/切场景时残留镂空材质
    private void OnDisable() => CloseHoles();

    // 销毁时恢复原始材质 + 释放所有运行时创建的材质实例，防止内存泄漏
    private void OnDestroy()
    {
        foreach (var entry in originalMaterials)
            if (entry.Key != null) entry.Key.sharedMaterials = entry.Value;

        foreach (Material[] materials in wallMaterials.Values)
            foreach (Material material in materials)
                if (material != null) Destroy(material);

        wallMaterials.Clear();
        originalMaterials.Clear();
    }
}