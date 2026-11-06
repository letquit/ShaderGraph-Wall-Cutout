namespace UnityUtils.StateMachine
{
    /// <summary>
    /// 定义状态机中状态对象的基本行为和生命周期回调接口。
    /// </summary>
    public interface IState
    {
        /// <summary>
        /// 在每一帧调用，用于处理状态的常规逻辑更新。
        /// </summary>
        void Update()
        {
        }

        /// <summary>
        /// 在固定的时间步长调用，用于处理状态的物理相关逻辑更新。
        /// </summary>
        void FixedUpdate()
        {
        }

        /// <summary>
        /// 在进入该状态时调用，用于执行状态初始化或进入时的准备工作。
        /// </summary>
        void OnEnter()
        {
        }

        /// <summary>
        /// 在退出该状态时调用，用于执行状态清理或退出时的收尾工作。
        /// </summary>
        void OnExit()
        {
        }
    }
}