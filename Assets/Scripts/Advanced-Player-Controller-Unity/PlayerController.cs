using System;
using ImprovedTimers;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityUtils;
using UnityUtils.StateMachine;
using Vector3 = UnityEngine.Vector3;

namespace AdvancedController
{
    // 确保该脚本挂载的 GameObject 上必须有 PlayerMover 组件
    [RequireComponent(typeof(PlayerMover))]
    public class PlayerController : MonoBehaviour
    {
        #region Fields (字段与变量)

        // --- 序列化字段 (Inspector 面板可配置) ---
        [SerializeField, Required] private InputReader input; // 输入组件 (Odin Inspector 的 Required 标签确保不为空)
        [SerializeField] private Transform cameraTransform; // 摄像机引用，用于计算摄像机相对方向

        private bool jumpInputIsLocked, jumpKeyWasPressed, jumpKeyWasLetGo, jumpKeyIsPressed;

        // --- 移动参数 ---
        public float movementSpeed = 7f; // 地面移动速度
        public float airControlSpeed = 2f; // 空中控制系数
        public float jumpSpeed = 10f; // 跳跃初速度
        public float jumpDuration = 0.2f; // 跳跃持续时间 (用于控制跳跃手感)
        public float airFriction = 0.5f; // 空中摩擦力
        public float groundFriction = 100f; // 地面摩擦力
        public float gravity = 30f; // 重力
        public float slideGravity = 5f; // 滑行时的额外重力
        public float slopeLimit = 30f; // 坡度限制 (超过此角度视为太陡，触发滑行)
        public bool useLocalMomentum; // 是否使用局部坐标系的动量

        // --- 组件与辅助对象 ---
        private Transform tr; // 缓存 Transform 组件
        private PlayerMover mover; // 角色移动逻辑组件
        private CeilingDetector ceilingDetector; // 天花板检测组件
        private StateMachine stateMachine; // 状态机实例
        private CountdownTimer jumpTimer; // 跳跃计时器 (来自 ImprovedTimers)

        // --- 动量与速度 ---
        private Vector3 momentum, savedVelocity, savedMovementVelocity;

        // --- 事件 ---
        public event Action<Vector3> OnJump = delegate { }; // 跳跃事件
        public event Action<Vector3> OnLand = delegate { }; // 着陆事件

        #endregion

        private void Awake()
        {
            // 初始化组件引用
            tr = transform;
            mover = GetComponent<PlayerMover>();
            ceilingDetector = GetComponent<CeilingDetector>();

            // 初始化跳跃计时器
            jumpTimer = new CountdownTimer(jumpDuration);

            // 设置状态机逻辑
            SetupStateMachine();
        }

        private void Start()
        {
            // 启用输入动作
            input.EnablePlayerActions();
            // 绑定跳跃输入回调
            input.Jump += HandleJumpKeyInput;
        }

        /// <summary>
        /// 玩家对象销毁（如 HP 归零被 Destroy）时禁用输入：
        /// InputReader 是 ScriptableObject，InputSystem 的回调不会随玩家对象自动注销，
        /// 若不显式禁用，后续输入（Fire/Move 等）会访问已销毁的组件（如 Camera.main）报错。
        /// </summary>
        private void OnDestroy()
        {
            if (input != null)
            {
                input.DisablePlayerActions();
            }
        }

        /// <summary>
        /// 处理跳跃按键输入的状态机逻辑。
        /// 用于检测按键按下 (Pressed)、松开 (LetGo) 和长按 (Held)。
        /// </summary>
        private void HandleJumpKeyInput(bool isButtonPressed)
        {
            if (!jumpKeyIsPressed && isButtonPressed)
            {
                jumpKeyWasPressed = true; // 标记为刚按下
            }

            if (jumpKeyIsPressed && !isButtonPressed)
            {
                jumpKeyWasLetGo = true; // 标记为刚松开
                jumpInputIsLocked = false; // 解锁输入锁
            }

            jumpKeyIsPressed = isButtonPressed; // 更新当前按键状态
        }

        /// <summary>
        /// 初始化状态机及其转换逻辑。
        /// 定义了角色在 Grounded(地面), Falling(下落), Sliding(滑行), 
        /// Rising(上升), Jumping(跳跃) 之间的切换规则。
        /// </summary>
        private void SetupStateMachine()
        {
            stateMachine = new StateMachine();

            // 创建所有状态实例
            var grounded = new GroundedState(this);
            var falling = new FallingState(this);
            var sliding = new SlidingState(this);
            var rising = new RisingState(this);
            var jumping = new JumpingState(this);

            // --- 定义状态转换规则 (At: 从A状态到B状态) ---
            // 地面状态的转换
            At(grounded, rising, () => IsRising()); // 地面 -> 上升：当动量向上时
            At(grounded, sliding, () => mover.IsGrounded() && IsGroundTooSteep()); // 地面 -> 滑行：在地面且坡度太陡
            At(grounded, falling, () => !mover.IsGrounded()); // 地面 -> 下落：离开地面
            At(grounded, jumping, () => (jumpKeyWasPressed || jumpKeyWasPressed) && !jumpInputIsLocked); // 地面 -> 跳跃

            // 下落状态的转换
            At(falling, rising, () => IsRising());
            At(falling, grounded, () => mover.IsGrounded() && !IsGroundTooSteep());
            At(falling, sliding, () => mover.IsGrounded() && IsGroundTooSteep());

            // 滑行状态的转换
            At(sliding, rising, () => IsRising());
            At(sliding, falling, () => !mover.IsGrounded());
            At(sliding, grounded, () => mover.IsGrounded() && !IsGroundTooSteep());

            // 上升状态的转换
            At(rising, grounded, () => mover.IsGrounded() && !IsGroundTooSteep());
            At(rising, sliding, () => mover.IsGrounded() && IsGroundTooSteep());
            At(rising, falling, () => IsFalling());
            // 上升 -> 下落：当检测到天花板时
            At(rising, falling, () => ceilingDetector != null && ceilingDetector.HitCeiling());

            // 跳跃状态的转换
            // 跳跃 -> 上升：当跳跃计时器结束 或 松开跳跃键时
            At(jumping, rising, () => jumpTimer.IsFinished || jumpKeyWasLetGo);
            // 跳跃 -> 下落：当检测到天花板时
            At(jumping, falling, (() => ceilingDetector != null && ceilingDetector.HitCeiling()));

            // 设置初始状态为 "下落" (Falling)，确保角色一开始在空中也能正确处理
            stateMachine.SetState(falling);
        }

        // 辅助方法：添加状态转换
        private void At(IState from, IState to, Func<bool> condition)
            => stateMachine.AddTransition(from, to, condition);

        // 辅助方法：添加任意状态转换 (Any)
        private void Any<T>(IState to, Func<bool> condition)
            => stateMachine.AddAnyTransition(to, condition);

        // --- 辅助判断函数 ---
        private bool IsRising() => VectorMath.GetDotProduct(GetMomentum(), tr.up) > 0f;
        private bool IsFalling() => VectorMath.GetDotProduct(GetMomentum(), tr.up) < 0f;

        // 判断地面坡度是否太陡 (超过 slopeLimit 度)
        private bool IsGroundTooSteep() => Vector3.Angle(mover.GetGroundNormal(), tr.up) > slopeLimit;

        // 获取动量 (考虑局部/世界坐标系)
        public Vector3 GetMomentum() => useLocalMomentum ? tr.localToWorldMatrix * momentum : momentum;

        // --- 更新循环 ---
        private void Update()
        {
            stateMachine.Update(); // 更新状态机逻辑
        }

        private void FixedUpdate()
        {
            stateMachine.FixedUpdate(); // 状态机的物理更新
            mover.CheckForGround(); // 检测地面碰撞
            HandleMomentum(); // 处理动量逻辑 (重力、摩擦力等)

            // 计算最终速度
            // 如果是地面状态，应用移动速度；否则仅应用动量
            Vector3 velocity = stateMachine.CurrentState is GroundedState ? CalculateMovementVelocity() : Vector3.zero;
            velocity += useLocalMomentum ? tr.localToWorldMatrix * momentum : momentum;

            // 设置移动组件的参数
            mover.SetExtendSensorRange(IsGrounded()); // 根据是否在地面调整传感器范围
            mover.SetVelocity(velocity); // 应用最终速度
            savedVelocity = velocity;
            savedMovementVelocity = CalculateMovementVelocity();

            ResetJumpKeys(); // 重置输入按键状态

            // 重置天花板检测器
            if (ceilingDetector != null) ceilingDetector.Reset();
        }

        // --- 移动计算 ---
        private Vector3 CalculateMovementVelocity() => CalculateMovementDirection() * movementSpeed;

        private Vector3 CalculateMovementDirection()
        {
            // 计算基于摄像机视角的移动方向
            Vector3 direction = cameraTransform == null
                ? tr.right * input.Direction.x + tr.forward * input.Direction.y
                : Vector3.ProjectOnPlane(cameraTransform.right, tr.up).normalized * input.Direction.x +
                  Vector3.ProjectOnPlane(cameraTransform.forward, tr.up).normalized * input.Direction.y;

            // 限制向量长度
            return direction.magnitude > 1f ? direction.normalized : direction;
        }

        /// <summary>
        /// 核心动量处理逻辑。
        /// 分离水平和垂直动量，应用重力、摩擦力和滑行逻辑。
        /// </summary>
        private void HandleMomentum()
        {
            if (useLocalMomentum) momentum = tr.localToWorldMatrix * momentum;

            // 分离垂直和水平动量
            Vector3 verticalMomentum = VectorMath.ExtractDotVector(momentum, tr.up);
            Vector3 horizontalMomentum = momentum - verticalMomentum;

            // 应用重力
            verticalMomentum -= tr.up * (gravity * Time.deltaTime);

            // 地面状态：垂直动量归零
            if (stateMachine.CurrentState is GroundedState && VectorMath.GetDotProduct(verticalMomentum, tr.up) < 0f)
            {
                verticalMomentum = Vector3.zero;
            }

            // 空中状态：处理空中控制
            if (!IsGrounded())
            {
                AdjustHorizontalMomentum(ref horizontalMomentum, CalculateMovementVelocity());
            }

            // 滑行状态：特殊处理
            if (stateMachine.CurrentState is SlidingState)
            {
                HandleSliding(ref horizontalMomentum);
            }

            // 应用摩擦力 (地面/空气)
            float friction = stateMachine.CurrentState is GroundedState ? groundFriction : airFriction;
            horizontalMomentum = Vector3.MoveTowards(horizontalMomentum, Vector3.zero, friction * Time.deltaTime);

            // 重新组合动量
            momentum = horizontalMomentum + verticalMomentum;

            // 跳跃状态：应用跳跃速度
            if (stateMachine.CurrentState is JumpingState)
            {
                HandleJumping();
            }

            // 滑行状态：应用滑行重力和坡度修正
            if (stateMachine.CurrentState is SlidingState)
            {
                momentum = Vector3.ProjectOnPlane(momentum, mover.GetGroundNormal());
                if (VectorMath.GetDotProduct(momentum, tr.up) > 0f)
                {
                    momentum = VectorMath.RemoveDotVector(momentum, tr.up);
                }

                Vector3 slideDirection = Vector3.ProjectOnPlane(-tr.up, mover.GetGroundNormal()).normalized;
                momentum += slideDirection * (slideGravity * Time.deltaTime);
            }

            if (useLocalMomentum) momentum = tr.worldToLocalMatrix * momentum;
        }

        // 跳跃处理
        private void HandleJumping()
        {
            momentum = VectorMath.RemoveDotVector(momentum, tr.up);
            momentum += tr.up * jumpSpeed;
        }

        // 输入状态重置
        private void ResetJumpKeys()
        {
            jumpKeyWasLetGo = false;
            jumpKeyWasPressed = false;
        }

        // --- 状态回调方法 (通常在状态类中被调用) ---

        /// <summary>
        /// 跳跃开始时调用。
        /// </summary>
        public void OnJumpStart()
        {
            if (useLocalMomentum) momentum = tr.localToWorldMatrix * momentum;
            momentum += tr.up * jumpSpeed;
            jumpTimer.Start(); // 启动计时器
            jumpInputIsLocked = true;
            OnJump.Invoke(momentum);
            if (useLocalMomentum) momentum = tr.worldToLocalMatrix * momentum;
        }

        /// <summary>
        /// 失去地面接触时调用 (开始下落)。
        /// 处理离开地面时的动量继承逻辑。
        /// </summary>
        public void OnGroundContactLost()
        {
            if (useLocalMomentum) momentum = tr.localToWorldMatrix * momentum;
            Vector3 velocity = GetMovementVelocity();
            // ... (复杂的动量投影逻辑，防止穿模或异常速度)
            momentum += velocity;
            if (useLocalMomentum) momentum = tr.worldToLocalMatrix * momentum;
        }

        public Vector3 GetMovementVelocity() => savedMovementVelocity;

        /// <summary>
        /// 重新接触地面时调用。
        /// </summary>
        public void OnGroundContactRegained()
        {
            Vector3 collisionVelocity = useLocalMomentum ? tr.localToWorldMatrix * momentum : momentum;
            OnLand.Invoke(collisionVelocity);
        }

        /// <summary>
        /// 处理开始下落时的动量转换逻辑。
        /// 将当前动量中的垂直向上分量反转为向下方向，同时保留水平方向的动量。
        /// </summary>
        public void OnFallStart()
        {
            // 提取当前动量在物体向上方向的投影向量
            var currentUpdMomentum = VectorMath.ExtractDotVector(momentum, tr.up);
            // 从总动量中移除向上方向的分量，从而保留水平动量
            momentum = VectorMath.RemoveDotVector(momentum, tr.up);
            // 将原有的向上动量大小反转为向下方向，并应用到总动量中
            momentum -= tr.up * currentUpdMomentum.magnitude;
        }

        // --- 辅助方法 ---

        // 滑行时的水平动量调整
        private void HandleSliding(ref Vector3 horizontalMomentum)
        {
            Vector3 pointDownVector = Vector3.ProjectOnPlane(mover.GetGroundNormal(), tr.up).normalized;
            Vector3 movementVelocity = CalculateMovementVelocity();
            movementVelocity = VectorMath.RemoveDotVector(movementVelocity, pointDownVector);
            horizontalMomentum += movementVelocity * Time.fixedDeltaTime;
        }

        // 空中水平动量调整 (空气控制)
        private void AdjustHorizontalMomentum(ref Vector3 horizontalMomentum, Vector3 movementVelocity)
        {
            // 根据当前速度与目标速度的关系，调整加速度
            if (horizontalMomentum.magnitude > movementSpeed)
            {
                // 减速逻辑
                if (VectorMath.GetDotProduct(movementVelocity, horizontalMomentum.normalized) > 0f)
                {
                    movementVelocity = VectorMath.RemoveDotVector(movementVelocity, horizontalMomentum.normalized);
                }

                horizontalMomentum += movementVelocity * (Time.deltaTime * airControlSpeed * 0.25f);
            }
            else
            {
                // 加速逻辑
                horizontalMomentum += movementVelocity * (Time.deltaTime * airControlSpeed);
                horizontalMomentum = Vector3.ClampMagnitude(horizontalMomentum, movementSpeed);
            }
        }

        // 检查是否在地面 (包含滑行状态)
        private bool IsGrounded() => stateMachine.CurrentState is GroundedState or SlidingState;
    }
}