using UnityEngine;

namespace UnityUtils.StateMachine
{
    /// <summary>
    /// 状态机实体的抽象基类，继承自 MonoBehaviour。
    /// 提供状态机的初始化、更新以及状态和转换的声明方法。
    /// 继承此类以实现具有状态机行为的实体。
    /// </summary>
    public abstract class StatefulEntity : MonoBehaviour
    {
        /// <summary>
        /// 状态机实例，用于管理实体的当前状态及状态转换。
        /// </summary>
        protected StateMachine stateMachine;

        /// <summary>
        /// Awake or Start can be used to declare all states and transitions.
        /// </summary>
        /// <example>
        /// <code>
        /// protected override void Awake() {
        ///     base.Awake();
        /// 
        ///     var state = new State1(this);
        ///     var anotherState = new State2(this);
        ///
        ///     At(state, anotherState, () => true);
        ///     At(state, anotherState, myFunc);
        ///     At(state, anotherState, myPredicate);
        /// 
        ///     Any(anotherState, () => true);
        ///
        ///     stateMachine.SetState(state);
        /// </code> 
        /// </example>
        protected virtual void Awake()
        {
            stateMachine = new StateMachine();
        }

        /// <summary>
        /// 在每一帧调用，用于驱动状态机的常规更新逻辑。
        /// </summary>
        protected virtual void Update() => stateMachine.Update();

        /// <summary>
        /// 在固定的物理时间步长调用，用于驱动状态机的物理相关更新逻辑。
        /// </summary>
        protected virtual void FixedUpdate() => stateMachine.FixedUpdate();

        /// <summary>
        /// 添加一个从指定起始状态到目标状态的条件转换。
        /// </summary>
        /// <typeparam name="T">转换条件的类型，通常为 Func&lt;bool&gt; 或 Predicate 等委托。</typeparam>
        /// <param name="from">转换的起始状态。</param>
        /// <param name="to">转换的目标状态。</param>
        /// <param name="condition">评估是否触发转换的条件。</param>
        protected void At<T>(IState from, IState to, T condition) => stateMachine.AddTransition(from, to, condition);

        /// <summary>
        /// 添加一个从任意当前状态到目标状态的全局条件转换。
        /// </summary>
        /// <typeparam name="T">转换条件的类型，通常为 Func&lt;bool&gt; 或 Predicate 等委托。</typeparam>
        /// <param name="to">转换的目标状态。</param>
        /// <param name="condition">评估是否触发转换的条件。</param>
        protected void Any<T>(IState to, T condition) => stateMachine.AddAnyTransition(to, condition);
    }
}