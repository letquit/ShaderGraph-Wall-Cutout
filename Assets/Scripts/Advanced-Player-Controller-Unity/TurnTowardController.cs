using System;
using Sirenix.OdinInspector; // Odin Inspector: 提供 [Required] 编辑器验证
using UnityEngine;
using UnityUtils; // 自定义工具库: 包含 VectorMath.GetAngle 等扩展方法

namespace AdvancedController
{
    /// <summary>
    /// 角色朝向控制器（仅处理 Y 轴水平旋转）
    /// 
    /// 架构定位：
    ///   PlayerController → 输出世界空间速度向量
    ///   TurnTowardController (LateUpdate) → 读取速度，平滑旋转角色 Transform
    ///   
    /// 核心设计原则：
    ///   1. 视觉与物理分离：不修改 Rigidbody/CharacterController，仅驱动 Transform
    ///   2. 累积欧拉角：避免从 Quaternion 反推角度的万向节锁问题
    ///   3. 非线性转向：小角度慢转（自然），大角度快转（响应性）
    ///   4. 过冲保护：单帧旋转量不超过剩余角度差
    /// </summary>
    public class TurnTowardController : MonoBehaviour
    {
        // === 配置 ===

        // [Required]: Odin 属性，编辑时未赋值则显示红色错误
        // 比运行时 NullRef 更早暴露问题
        [SerializeField, Required] private PlayerController controller;

        // 基础转向速度（度/秒），在 fallOffAngle 处达到最大值
        public float turnSpeed = 50f;

        // === 运行时状态 ===
        private Transform tr;

        // ⚠️ 关键设计：独立维护 Y 轴累积角度
        // 不使用 tr.localRotation.y，因为从 Quaternion 提取欧拉角
        // 在俯仰非零时会产生万向节锁导致的跳变
        private float currentYRotation;

        // 转向响应的角度阈值
        // 当角度差 ≥ 90° 时转向速度达到 turnSpeed 最大值
        // 当角度差 < 90° 时速度按比例衰减，实现自然的"减速对齐"
        private const float fallOffAngle = 90f;

        private void Start()
        {
            tr = transform;
            // 同步初始旋转，避免首帧跳变
            // ⚠️ 同 CameraController 的潜在问题：localEulerAngles.y ∈ [0,360]
            // 若初始值为 350°，后续计算可能产生 350→0 的长路径旋转
            // 建议规范化：if (currentYRotation > 180f) currentYRotation -= 360f;
            currentYRotation = tr.localEulerAngles.y;
        }

        /// <summary>
        /// LateUpdate 保证：
        /// 1. PlayerController.Update/FixedUpdate 已产出最新速度
        /// 2. 动画系统已完成骨骼更新
        /// 3. 本组件最后修正朝向
        /// 4. 渲染使用最终朝向
        /// </summary>
        private void LateUpdate()
        {
            // === 1. 获取投影到父级平面上的移动速度 ===
            // ProjectOnPlane 移除垂直分量，确保只响应水平移动
            // 使用 tr.parent.up 而非 Vector3.up，支持在斜坡/移动平台上正确转向
            Vector3 velocity = Vector3.ProjectOnPlane(controller.GetMovementVelocity(), tr.parent.up);

            // 死区检查：速度极小时不转向，防止静止时因浮点噪声抖动
            if (velocity.magnitude < 0.001f) return;

            // === 2. 计算带符号的角度差 ===
            // GetAngle(from, to, axis) 返回 [-180, 180] 范围的有符号角度
            // 正值 = 需顺时针转，负值 = 需逆时针转
            // ⚠️ 这比 Vector3.Angle 更优：后者返回 [0,180] 无符号值，丢失转向方向
            float angleDifference = VectorMath.GetAngle(tr.forward, velocity.normalized, tr.parent.up);

            // === 3. 计算本帧旋转步长（核心算法）===
            // Mathf.Sign(angleDifference): 保留转向方向 (+1 / -1)
            // InverseLerp(0, 90, |diff|): 将角度差映射到 [0,1] 归一化因子
            //   → 0° 差 = 0 倍速（自然停止）
            //   → 90°+ 差 = 1 倍速（最大响应）
            //   → 中间值线性过渡（平滑减速对齐）
            // Time.deltaTime * turnSpeed: 帧率无关的基础角速度
            float step = Mathf.Sign(angleDifference)
                         * Mathf.InverseLerp(0f, fallOffAngle, Mathf.Abs(angleDifference))
                         * Time.deltaTime * turnSpeed;

            // === 4. 过冲保护 + 角度累积 ===
            // 若本帧步长已超过剩余角度差，直接吸附到目标角度
            // 防止在小角度时因 dt 波动导致来回震荡
            float absStep = Mathf.Abs(step);
            float absDiff = Mathf.Abs(angleDifference);
            currentYRotation += absStep > absDiff ? angleDifference : step;

            // === 5. 从累积标量重建旋转 ===
            // X=0, Z=0 保证角色不会意外倾斜
            // 每帧从 float 重建，彻底避免 Quaternion 数值漂移
            tr.localRotation = Quaternion.Euler(0f, currentYRotation, 0f);
        }
    }
}