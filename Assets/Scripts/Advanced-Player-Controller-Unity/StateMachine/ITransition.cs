namespace UnityUtils.StateMachine
{
    /// <summary>
    /// 定义状态机中的状态转换接口。
    /// 封装了状态转移的目标状态以及触发该转移所需满足的条件。
    /// </summary>
    public interface ITransition
    {
        /// <summary>
        /// 获取状态转换的目标状态。
        /// 当转换条件满足时，状态机将转移到此状态。
        /// </summary>
        IState To { get; }

        /// <summary>
        /// 获取触发状态转换的条件判断逻辑。
        /// 状态机在评估时若该条件返回 true，则执行此转换。
        /// </summary>
        IPredicate Condition { get; }
    }
}