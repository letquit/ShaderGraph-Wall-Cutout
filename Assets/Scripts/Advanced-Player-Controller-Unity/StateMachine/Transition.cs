using System;

namespace UnityUtils.StateMachine
{
    /// <summary>
    /// 状态机中的状态转换抽象基类。
    /// </summary>
    public abstract class Transition
    {
        /// <summary>
        /// 获取或设置转换的目标状态。
        /// </summary>
        public IState To { get; protected set; }

        /// <summary>
        /// 评估是否满足状态转换的条件。
        /// </summary>
        /// <returns>如果满足转换条件则返回 true；否则返回 false。</returns>
        public abstract bool Evaluate();
    }

    /// <summary>
    /// 带有泛型条件的状态转换实现类。
    /// </summary>
    /// <typeparam name="T">转换条件的类型。</typeparam>
    public class Transition<T> : Transition
    {
        /// <summary>
        /// 状态转换的条件对象。
        /// </summary>
        public readonly T condition;

        /// <summary>
        /// 初始化 <see cref="Transition{T}"/> 类的新实例。
        /// </summary>
        /// <param name="to">转换的目标状态。</param>
        /// <param name="condition">触发转换的条件。</param>
        public Transition(IState to, T condition)
        {
            To = to;
            this.condition = condition;
        }

        /// <summary>
        /// 评估当前条件是否满足状态转换要求。
        /// 依次尝试将条件转换为委托或谓词接口并执行评估。
        /// </summary>
        /// <returns>如果条件评估为 true 则返回 true；如果条件类型不匹配或评估为 false 则返回 false。</returns>
        public override bool Evaluate()
        {
            // 尝试将条件变量作为 Func<bool> 委托进行调用
            // Check if the condition variable is a Func<bool> and call the Invoke method if it is not null
            var result = (condition as Func<bool>)?.Invoke();
            if (result.HasValue)
            {
                return result.Value;
            }

            // 尝试将条件变量作为 ActionPredicate 进行调用
            // Check if the condition variable is an ActionPredicate and call the Evaluate method if it is not null
            result = (condition as ActionPredicate)?.Evaluate();
            if (result.HasValue)
            {
                return result.Value;
            }

            // 尝试将条件变量作为 IPredicate 接口进行调用
            // Check if the condition variable is an IPredicate and call the Evaluate method if it is not null
            result = (condition as IPredicate)?.Evaluate();
            if (result.HasValue)
            {
                return result.Value;
            }

            // 如果条件变量不是受支持的类型，则默认返回 false
            // If the condition variable is not a Func<bool>, an ActionPredicate, or an IPredicate, return false
            return false;
        }
    }

    /// <summary>
    /// Represents a predicate that uses a Func delegate to evaluate a condition.
    /// </summary>
    /// <remarks>
    /// 表示使用 Func 委托来评估条件的谓词。
    /// </remarks>
    public class FuncPredicate : IPredicate
    {
        /// <summary>
        /// 用于评估条件的委托。
        /// </summary>
        readonly Func<bool> func;

        /// <summary>
        /// 初始化 <see cref="FuncPredicate"/> 类的新实例。
        /// </summary>
        /// <param name="func">用于条件评估的返回布尔值的委托。</param>
        public FuncPredicate(Func<bool> func)
        {
            this.func = func;
        }

        /// <summary>
        /// 评估条件。
        /// </summary>
        /// <returns>调用委托得到的布尔结果。</returns>
        public bool Evaluate() => func.Invoke();
    }

    /// <summary>
    /// Represents a predicate that encapsulates an action and evaluates to true once the action has been invoked.
    /// </summary>
    /// <remarks>
    /// 表示封装了一个动作的谓词，当该动作被调用后，评估结果将返回 true。
    /// </remarks>
    public class ActionPredicate : IPredicate
    {
        /// <summary>
        /// 标记动作是否已被触发的状态标志。
        /// </summary>
        public bool flag;

        /// <summary>
        /// 初始化 <see cref="ActionPredicate"/> 类的新实例，并将触发逻辑订阅到指定的事件反应中。
        /// </summary>
        /// <param name="eventReaction">要订阅的事件动作引用。</param>
        public ActionPredicate(ref Action eventReaction) => eventReaction += () => { flag = true; };

        /// <summary>
        /// 评估动作是否已被触发，并在评估后重置标志。
        /// </summary>
        /// <returns>如果动作已被触发则返回 true，否则返回 false。</returns>
        public bool Evaluate()
        {
            // 获取当前标志状态并将其重置为 false，以实现单次触发评估
            bool result = flag;
            flag = false;
            return result;
        }
    }
}