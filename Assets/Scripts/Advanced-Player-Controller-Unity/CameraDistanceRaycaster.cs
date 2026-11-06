using System;
using Sirenix.OdinInspector; // Odin Inspector: 提供 [Required] 编辑器验证属性
using UnityEngine;

namespace AdvancedController
{
    /// <summary>
    /// 相机距离射线投射器（障碍物规避）
    /// 
    /// 架构定位：
    ///   CameraController (Update) → 设置理想旋转
    ///   CameraDistanceRaycaster (LateUpdate) → 根据碰撞调整实际距离
    ///   
    /// 核心原理：
    ///   Pivot ──── SphereCast ────→ IdealPosition
    ///              ↓ hit
    ///   Pivot ── AdjustedPosition (拉近相机)
    /// </summary>
    public class CameraDistanceRaycaster : MonoBehaviour
    {
        // === 配置字段 ===

        // [Required]: Odin 属性，编辑时若未赋值显示红色错误提示
        // 比运行时 NullReferenceException 更早发现问题
        [SerializeField, Required] private Transform cameraTransform; // 实际相机 Transform（被本组件移动）
        [SerializeField, Required] private Transform cameraTargetTransform; // 理想相机位置（由 CameraController 驱动）

        // 碰撞检测层掩码，默认检测所有层
        public LayerMask layerMask = Physics.AllLayers;

        // 相机与障碍物表面的最小安全距离
        // 防止相机近裁剪面穿透墙壁导致看到墙体内部
        public float minimumDistanceFromObstacles = 0.1f;

        // 距离插值平滑系数
        // ⚠️ 帧率依赖警告：与前一脚本相同的 Lerp 问题，见下方改进建议
        public float smoothingFactor = 25;

        // === 运行时状态 ===
        private Transform tr; // 缓存自身 Transform（作为射线起点/Pivot）
        private float currentDistance; // 平滑后的当前距离（独立于目标距离的状态变量）

        private void Awake()
        {
            tr = transform;

            // 从检测层中排除 "Ignore Raycast" 层
            // &= ~ 是位运算清除特定位的标准写法
            // 确保触发器区域、UI 碰撞体等不会误拦截相机射线
            layerMask &= ~(1 << LayerMask.NameToLayer("Ignore Raycast"));

            // 初始化平滑距离为当前实际距离，避免首帧跳变
            currentDistance = (cameraTargetTransform.position - tr.position).magnitude;
        }

        /// <summary>
        /// LateUpdate 执行时机保证：
        /// 1. CameraController.Update 已设置理想旋转/位置
        /// 2. 角色动画/IK 已完成
        /// 3. 本组件最后修正相机距离
        /// 4. 渲染使用修正后的最终位置
        /// </summary>
        private void LateUpdate()
        {
            // 从 Pivot 指向理想相机位置的方向向量
            Vector3 castDirection = cameraTargetTransform.position - tr.position;

            // 物理探测获取无障碍距离
            float distance = GetCameraDistance(castDirection);

            // 平滑过渡到新距离
            // ⚠️ 帧率依赖：应改为 1 - Exp(-k * dt) 形式
            currentDistance = Mathf.Lerp(currentDistance, distance, Time.deltaTime * smoothingFactor);

            // 将相机放置在 Pivot + 方向 × 平滑距离处
            cameraTransform.position = tr.position + castDirection.normalized * currentDistance;
        }

        /// <summary>
        /// 物理探测：返回相机到 Pivot 的安全距离
        /// </summary>
        private float GetCameraDistance(Vector3 castDirection)
        {
            // 最大探测距离 = 理想距离 + 安全余量
            // 加余量确保 SphereCast 的球体不会在理想位置之外提前命中
            float distance = castDirection.magnitude + minimumDistanceFromObstacles;

            // ❌ 已注释的 Raycast 方案（保留用于对比教学）
            // Raycast 是无限细的线，容易穿过薄墙、栅栏、铁丝网等几何体
            // 导致相机在下一帧突然穿墙或剧烈抖动
            // if (Physics.Raycast(new Ray(tr.position, castDirection), out RaycastHit hit, distance, layerMask,
            //         QueryTriggerInteraction.Ignore))
            // {
            //     return Mathf.Max(0f, hit.distance - minimumDistanceFromObstacles);
            // }

            // ✅ SphereCast 方案（当前激活）
            // 用半径 0.5f 的球体沿射线扫过，模拟相机的物理体积
            // 优势：不会穿过薄几何体，碰撞结果更稳定，减少抖动
            // ⚠️ 硬编码警告：sphereRadius 应提取为 [SerializeField] 配置字段
            float sphereRadius = 0.5f;
            if (Physics.SphereCast(new Ray(tr.position, castDirection), sphereRadius, out RaycastHit hit, distance,
                    layerMask, QueryTriggerInteraction.Ignore))
            {
                // hit.distance 是球心到碰撞点的距离
                // 减去安全余量得到相机实际可放置的距离
                // Max(0, ...) 防止负距离导致相机翻转到 Pivot 另一侧
                return Mathf.Max(0f, hit.distance - minimumDistanceFromObstacles);
            }

            // 无碰撞：返回理想距离
            return castDirection.magnitude;
        }
    }
}