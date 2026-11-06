using UnityEngine;
using UnityUtils.StateMachine;

namespace AdvancedController
{
    /// <summary>
    /// 表示角色处于地面接触状态的状态类。
    /// 当角色进入此状态时，会通知控制器重新获得地面接触。
    /// </summary>
    public class GroundedState : IState
    {
        readonly PlayerController controller;

        /// <summary>
        /// 初始化 GroundedState 类的新实例。
        /// </summary>
        /// <param name="controller">用于管理角色行为和状态的玩家控制器实例。</param>
        public GroundedState(PlayerController controller)
        {
            this.controller = controller;
        }

        /// <summary>
        /// 当状态机进入此状态时调用。
        /// 通知玩家控制器角色已重新接触地面。
        /// </summary>
        public void OnEnter()
        {
            controller.OnGroundContactRegained();
        }
    }

    /// <summary>
    /// 表示角色处于自由下落状态的状态类。
    /// 当角色进入此状态时，会通知控制器下落过程开始。
    /// </summary>
    public class FallingState : IState
    {
        readonly PlayerController controller;

        /// <summary>
        /// 初始化 FallingState 类的新实例。
        /// </summary>
        /// <param name="controller">用于管理角色行为和状态的玩家控制器实例。</param>
        public FallingState(PlayerController controller)
        {
            this.controller = controller;
        }

        /// <summary>
        /// 当状态机进入此状态时调用。
        /// 通知玩家控制器角色开始下落。
        /// </summary>
        public void OnEnter()
        {
            controller.OnFallStart();
        }
    }

    /// <summary>
    /// 表示角色处于滑行状态的状态类。
    /// 当角色进入此状态时，会通知控制器失去地面接触。
    /// </summary>
    public class SlidingState : IState
    {
        readonly PlayerController controller;

        /// <summary>
        /// 初始化 SlidingState 类的新实例。
        /// </summary>
        /// <param name="controller">用于管理角色行为和状态的玩家控制器实例。</param>
        public SlidingState(PlayerController controller)
        {
            this.controller = controller;
        }

        /// <summary>
        /// 当状态机进入此状态时调用。
        /// 通知玩家控制器角色已失去地面接触。
        /// </summary>
        public void OnEnter()
        {
            controller.OnGroundContactLost();
        }
    }

    /// <summary>
    /// 表示角色处于上升状态（非主动跳跃引起）的状态类。
    /// 当角色进入此状态时，会通知控制器失去地面接触。
    /// </summary>
    public class RisingState : IState
    {
        readonly PlayerController controller;

        /// <summary>
        /// 初始化 RisingState 类的新实例。
        /// </summary>
        /// <param name="controller">用于管理角色行为和状态的玩家控制器实例。</param>
        public RisingState(PlayerController controller)
        {
            this.controller = controller;
        }

        /// <summary>
        /// 当状态机进入此状态时调用。
        /// 通知玩家控制器角色已失去地面接触。
        /// </summary>
        public void OnEnter()
        {
            controller.OnGroundContactLost();
        }
    }

    /// <summary>
    /// 表示角色处于主动跳跃状态的状态类。
    /// 当角色进入此状态时，会通知控制器失去地面接触并触发跳跃开始逻辑。
    /// </summary>
    public class JumpingState : IState
    {
        readonly PlayerController controller;

        /// <summary>
        /// 初始化 JumpingState 类的新实例。
        /// </summary>
        /// <param name="controller">用于管理角色行为和状态的玩家控制器实例。</param>
        public JumpingState(PlayerController controller)
        {
            this.controller = controller;
        }

        /// <summary>
        /// 当状态机进入此状态时调用。
        /// 通知玩家控制器角色已失去地面接触，并触发跳跃开始的回调。
        /// </summary>
        public void OnEnter()
        {
            // 角色起跳时，首先标记失去地面接触，随后执行跳跃初始化的逻辑
            controller.OnGroundContactLost();
            controller.OnJumpStart();
        }
    }
}