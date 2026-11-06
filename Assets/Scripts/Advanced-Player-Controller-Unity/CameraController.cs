using System;
using Sirenix.OdinInspector; // Odin Inspector: 提供 [Required] 等编辑器增强属性
using Unity.Mathematics; // DOTS 数学库: 高性能数学运算（当前未直接使用，但为后续扩展预留）
using UnityEngine;

namespace AdvancedController
{
    /// <summary>
    /// 轨道相机旋转控制器
    /// 
    /// 架构定位：
    ///   InputReader → CameraController → Transform.localRotation
    ///       ↓
    ///   (障碍物检测/规避逻辑将在 LateUpdate 或独立组件中实现)
    /// 
    /// 核心设计：
    /// 使用 float 累积角度而非直接操作 Quaternion，
    /// 避免万向节锁、 Gimbal Lock 和 Slerp 过冲问题。
    /// 每帧从累积值重新合成旋转，保证数值稳定性。
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        #region Fields

        // === 累积角度状态 ===
        // ⚠️ 关键设计：不使用 transform.rotation 反推角度
        // 从 Quaternion 反推 Euler 角会因万向节锁产生跳变，
        // 独立维护 float 累积值是轨道相机的标准做法
        private float currentXAngle; // 俯仰角（Pitch）：绕本地 X 轴
        private float currentYAngle; // 偏航角（Yaw）：绕本地 Y 轴

        // === 垂直限制 ===
        // Range 属性在 Inspector 中生成滑块，防止运行时设置非法值
        [Range(0f, 90f)] public float upperVerticalLimit = 35f; // 向上看最大角度
        [Range(0f, 90f)] public float lowerVerticalLimit = 35f; // 向下看最大角度

        // === 旋转参数 ===
        public float cameraSpeed = 50f; // 基础灵敏度
        public bool smoothCameraRotation; // 是否启用输入平滑
        [Range(1f, 50f)] public float CameraSmoothingFactor = 25f; // 平滑系数

        // === 组件引用 ===
        private Transform tr; // 缓存 Transform，避免每帧 GetComponent 开销
        private Camera cam; // 子相机引用（供障碍物检测等外部系统使用）

        // [Required]: Odin Inspector 属性，编辑时若未赋值则显示红色警告
        // 比 [SerializeField] 更安全，在开发阶段即可发现遗漏
        [SerializeField, Required] private InputReader input;

        /// <summary>
        /// 视角锁定开关：为 true 时跳过输入旋转（供瞄准系统在施法瞄准期间冻结视角，
        /// 让鼠标移动用于指示器/范围定位而非转动相机）。
        /// </summary>
        public bool lockLook;

        #endregion

        // === 公共查询接口 ===
        // 供角色控制器、IK 系统等读取相机朝向
        // 使用 Transform 而非存储的方向向量，保证始终返回最新值
        public Vector3 GetUpDirection() => tr.up;
        public Vector3 GetFacingDirection() => tr.forward;

        private void Awake()
        {
            // 缓存组件引用，消除运行时查找开销
            tr = transform;
            cam = GetComponentInChildren<Camera>();

            // 从初始旋转同步累积角度
            // ⚠️ 仅在 Awake 执行一次，之后完全由累积值驱动
            // 若场景保存了非零旋转，此处确保平滑过渡而非跳变
            currentXAngle = tr.localRotation.eulerAngles.x;
            currentYAngle = tr.localRotation.eulerAngles.y;

            // ⚠️ 潜在问题：EulerAngle.x 范围是 [0, 360]
            // 若初始俯仰为 350°（即 -10°），Clamp 后会跳到 0°
            // 建议添加规范化处理：if (currentXAngle > 180f) currentXAngle -= 360f;
        }

        private void Update()
        {
            // 瞄准锁定：lockLook 为 true 时冻结视角，鼠标移动让位于瞄准指示器
            if (lockLook) return;

            // Y 轴取反：鼠标/摇杆上推 = 正值，但相机俯仰上抬需要负 X 旋转
            RotateCamera(input.LookDirection.x, -input.LookDirection.y);
        }

        /// <summary>
        /// 核心旋转逻辑：输入处理 → 角度累积 → 约束 → 旋转合成
        /// </summary>
        private void RotateCamera(float horizontalInput, float verticalInput)
        {
            // === 输入平滑 ===
            if (smoothCameraRotation)
            {
                // Lerp(0, input, factor) 本质是指数衰减滤波
                // ⚠️ 帧率依赖警告：此写法在不同帧率下平滑效果不一致
                // 生产环境应改为：Mathf.Lerp(current, target, 1 - Mathf.Exp(-factor * dt))
                horizontalInput = Mathf.Lerp(0, horizontalInput, Time.deltaTime * CameraSmoothingFactor);
                verticalInput = Mathf.Lerp(0, verticalInput, Time.deltaTime * CameraSmoothingFactor);
            }

            // === 角度累积 ===
            // 乘以 Time.deltaTime 使旋转速度与帧率无关
            currentXAngle += verticalInput * cameraSpeed * Time.deltaTime;
            currentYAngle += horizontalInput * cameraSpeed * Time.deltaTime;

            // === 垂直约束 ===
            // Clamp 在累积后、合成前执行，保证旋转永远合法
            // Yaw 不约束：允许无限水平旋转（float 溢出精度损失可忽略）
            currentXAngle = Mathf.Clamp(currentXAngle, -upperVerticalLimit, lowerVerticalLimit);

            // === 旋转合成 ===
            // 从安全的标量角度重建 Quaternion，彻底避免万向节锁
            // Z=0 保证相机不会发生 Roll 倾斜
            tr.localRotation = Quaternion.Euler(currentXAngle, currentYAngle, 0);
        }
    }
}