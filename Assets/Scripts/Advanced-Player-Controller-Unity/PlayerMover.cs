using System;
using UnityEngine;

namespace AdvancedController
{
    /// <summary>
    /// 玩家移动器。
    /// 负责处理角色的底层物理移动和地面检测。
    /// 它通过射线检测（Raycast Sensor）来模拟胶囊体碰撞，从而实现比 Unity 原生物理更平滑的移动和台阶跨越效果。
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public class PlayerMover : MonoBehaviour
    {
        #region Fields

        /// <summary>
        /// 台阶高度比例。
        /// 用于计算胶囊体的分段比例，以及角色能自动跨过的最大台阶高度。
        /// </summary>
        [Header("Collider Settings:")] [Range(0f, 1f)] [SerializeField]
        private float stepHeightRatio = 0.1f;

        [SerializeField] private float colliderHeight = 2f; // 胶囊体总高度
        [SerializeField] private float colliderThickness = 1f; // 胶囊体直径（厚度）
        [SerializeField] private Vector3 colliderOffset = Vector3.zero; // 胶囊体中心点的偏移量

        private Rigidbody rb;
        private Transform tr;
        private CapsuleCollider col;
        private RaycastSensor sensor; // 射线传感器，用于代替物理碰撞检测地面

        private bool isGrounded; // 是否接触地面
        private float baseSensorRange; // 传感器的基础探测距离

        /// <summary>
        /// 地面修正速度。
        /// 用于在检测到地面但距离过近时，将角色“吸附”到理想的地面高度，防止抖动。
        /// </summary>
        private Vector3 currentGroundAdjustmentVelocity;

        private int currentLayer; // 缓存当前层级，用于检测层级变化

        [Header("Sensor Settings:")] [SerializeField]
        private bool isInDebugMode;

        /// <summary>
        /// 是否使用扩展探测范围。
        /// 在空中时开启此选项可延长射线，防止角色在下落过程中过早判定为“接地”。
        /// </summary>
        private bool isUsingExtendedSensorRange = true;

        #endregion

        private void Awake()
        {
            Setup();
            RecalculateColliderDimensions();
        }

        // 当在 Inspector 中修改数值时立即更新碰撞体，方便调试
        private void OnValidate()
        {
            if (gameObject.activeInHierarchy)
            {
                RecalculateColliderDimensions();
            }
        }

        /// <summary>
        /// 检测地面。
        /// 这是每一帧物理更新前必须调用的核心方法。
        /// 它发射射线检测地面，并计算是否需要垂直修正位置。
        /// </summary>
        public void CheckForGround()
        {
            // 如果层级发生变化，重新计算射线遮罩
            if (currentLayer != gameObject.layer)
            {
                RecalculateSensorLayerMask();
            }

            currentGroundAdjustmentVelocity = Vector3.zero;

            // 动态调整射线长度：如果在空中（扩展模式），则增加探测距离
            sensor.castLength = isUsingExtendedSensorRange
                ? baseSensorRange + colliderHeight * tr.localScale.x * stepHeightRatio
                : baseSensorRange;

            sensor.Cast(); // 发射射线

            isGrounded = sensor.HasDetectedHit();
            if (!isGrounded) return;

            // --- 地面吸附逻辑 ---
            // 计算角色底部与理想地面位置的距离，生成修正速度
            float distance = sensor.GetDistance();
            float upperLimit = colliderHeight * tr.localScale.x * (1f - stepHeightRatio) * 0.5f;
            float middle = upperLimit + colliderHeight * tr.localScale.x * stepHeightRatio;
            float distanceToGo = middle - distance;

            // 计算这一帧需要移动多少距离才能完美贴合地面
            currentGroundAdjustmentVelocity = tr.up * (distanceToGo / Time.fixedDeltaTime);
        }

        public bool IsGrounded() => isGrounded;
        public Vector3 GetGroundNormal() => sensor.GetNormal();

        /// <summary>
        /// 设置刚体速度。
        /// 注意：这里不仅应用了外部传入的速度，还叠加了地面修正速度。
        /// </summary>
        public void SetVelocity(Vector3 velocity) => rb.linearVelocity = velocity + currentGroundAdjustmentVelocity;

        public void SetExtendSensorRange(bool isExtended) => isUsingExtendedSensorRange = isExtended;

        private void Setup()
        {
            tr = transform;
            rb = GetComponent<Rigidbody>();
            col = GetComponent<CapsuleCollider>();

            // 禁用物理引擎的自动旋转和重力，完全由代码控制
            rb.freezeRotation = true;
            rb.useGravity = false;
        }

        /// <summary>
        /// 重新计算射线传感器的层级遮罩。
        /// 确保射线不会穿透应该发生碰撞的物体，也不会检测到被忽略的层级。
        /// </summary>
        private void RecalculateSensorLayerMask()
        {
            int objectLayer = gameObject.layer;
            int layerMask = Physics.AllLayers;

            for (int i = 0; i < 32; i++)
            {
                if (Physics.GetIgnoreLayerCollision(objectLayer, i))
                {
                    layerMask &= ~(1 << i);
                }
            }

            int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
            layerMask &= ~(1 << ignoreRaycastLayer);

            sensor.layermask = layerMask;
            currentLayer = objectLayer;
        }

        /// <summary>
        /// 校准传感器参数。
        /// 根据胶囊体的大小设置射线的起点、方向和长度。
        /// </summary>
        private void RecalibrateSensor()
        {
            sensor ??= new RaycastSensor(tr);

            sensor.SetCastOrigin(col.bounds.center);
            sensor.SetCastDirection(RaycastSensor.CastDirection.Down);
            RecalculateSensorLayerMask();

            const float safetyDistanceFactor = 0.001f; // 安全系数，防止浮点数误差导致检测失效

            // 计算传感器长度：包含胶囊体主体部分 + 台阶高度部分
            float length = colliderHeight * (1f - stepHeightRatio) * 0.5f + colliderHeight * stepHeightRatio;
            baseSensorRange = length * (1f + safetyDistanceFactor) * tr.localScale.x;
            sensor.castLength = length * tr.localScale.x;
        }

        /// <summary>
        /// 重新计算碰撞体尺寸。
        /// 根据 Inspector 中的配置动态调整胶囊体 Collider 的形状。
        /// </summary>
        private void RecalculateColliderDimensions()
        {
            if (col == null)
            {
                Setup();
            }

            // 胶囊体高度 = 总高度 * (1 - 台阶比例)
            // 这样胶囊体主体就不会包含底部的“台阶空间”
            col.height = colliderHeight * (1f - stepHeightRatio);
            col.radius = colliderThickness / 2f;

            // 计算中心点偏移，确保胶囊体底部留出台阶高度的空间
            col.center = colliderOffset * colliderHeight + new Vector3(0f, stepHeightRatio * col.height / 2f, 0f);

            // 防止胶囊体半径大于高度的一半（这会导致胶囊体变形或报错）
            if (col.height / 2f < col.radius)
            {
                col.radius = col.height / 2f;
            }

            RecalibrateSensor();
        }
    }
}