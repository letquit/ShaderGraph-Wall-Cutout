using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityUtils.StateMachine
{
    /// <summary>
    /// 状态机核心类，负责管理状态节点、状态转换以及状态的生命周期调用。
    /// </summary>
    public class StateMachine
    {
        // 当前激活的状态节点
        StateNode currentNode;

        // 存储所有已注册状态节点的字典，以状态类型为键
        readonly Dictionary<Type, StateNode> nodes = new();

        // 存储全局转换（Any Transitions）的集合，在任何状态下都会被评估
        readonly HashSet<Transition> anyTransitions = new();

        /// <summary>
        /// 获取当前状态机正在运行的状态实例。
        /// </summary>
        public IState CurrentState => currentNode.State;

        /// <summary>
        /// 状态机的每帧更新方法。评估状态转换条件，执行状态切换，并调用当前状态的 Update 方法。
        /// </summary>
        public void Update()
        {
            // 获取当前满足条件的转换
            var transition = GetTransition();

            if (transition != null)
            {
                ChangeState(transition.To);

                // 重置所有节点和全局转换中 ActionPredicate 条件的标志位，防止条件状态残留
                foreach (var node in nodes.Values)
                {
                    ResetActionPredicateFlags(node.Transitions);
                }

                ResetActionPredicateFlags(anyTransitions);
            }

            currentNode.State?.Update();
        }

        /// <summary>
        /// 重置指定转换集合中所有 ActionPredicate 类型条件的标志位。
        /// </summary>
        /// <param name="transitions">需要重置标志位的转换集合。</param>
        static void ResetActionPredicateFlags(IEnumerable<Transition> transitions)
        {
            // 遍历并重置 ActionPredicate 条件的 flag 属性
            foreach (var transition in transitions.OfType<Transition<ActionPredicate>>())
            {
                transition.condition.flag = false;
            }
        }

        /// <summary>
        /// 状态机的物理帧更新方法，调用当前状态的 FixedUpdate 方法。
        /// </summary>
        public void FixedUpdate()
        {
            currentNode.State?.FixedUpdate();
        }

        /// <summary>
        /// 强制设置当前状态。不经过转换条件评估，直接切换并触发新状态的 OnEnter。
        /// </summary>
        /// <param name="state">要设置的目标状态实例。</param>
        public void SetState(IState state)
        {
            // 直接更新当前节点并触发进入逻辑
            currentNode = nodes[state.GetType()];
            currentNode.State?.OnEnter();
        }

        /// <summary>
        /// 内部状态切换逻辑。处理旧状态的退出和新状态的进入，并更新当前节点。
        /// </summary>
        /// <param name="state">要切换到的目标状态实例。</param>
        void ChangeState(IState state)
        {
            if (state == currentNode.State)
                return;

            var previousState = currentNode.State;
            var nextState = nodes[state.GetType()].State;

            // 依次触发旧状态的退出和新状态的进入回调
            previousState?.OnExit();
            nextState.OnEnter();
            currentNode = nodes[state.GetType()];
        }

        /// <summary>
        /// 添加两个状态之间的转换规则。
        /// </summary>
        /// <typeparam name="T">转换条件的类型。</typeparam>
        /// <param name="from">起始状态实例。</param>
        /// <param name="to">目标状态实例。</param>
        /// <param name="condition">触发转换的条件实例。</param>
        public void AddTransition<T>(IState from, IState to, T condition)
        {
            GetOrAddNode(from).AddTransition(GetOrAddNode(to).State, condition);
        }

        /// <summary>
        /// 添加全局转换规则（Any Transition），在任何状态下只要满足条件即可触发转换。
        /// </summary>
        /// <typeparam name="T">转换条件的类型。</typeparam>
        /// <param name="to">目标状态实例。</param>
        /// <param name="condition">触发转换的条件实例。</param>
        public void AddAnyTransition<T>(IState to, T condition)
        {
            anyTransitions.Add(new Transition<T>(GetOrAddNode(to).State, condition));
        }

        /// <summary>
        /// 评估并获取当前可触发的转换。优先评估全局转换，其次评估当前状态的局部转换。
        /// </summary>
        /// <returns>满足条件的转换对象；如果没有满足条件的转换，则返回 null。</returns>
        Transition GetTransition()
        {
            // 优先检查全局转换
            foreach (var transition in anyTransitions)
                if (transition.Evaluate())
                    return transition;

            // 检查当前状态的局部转换
            foreach (var transition in currentNode.Transitions)
            {
                if (transition.Evaluate())
                    return transition;
            }

            return null;
        }

        /// <summary>
        /// 获取指定状态对应的状态节点。如果节点不存在，则创建并添加到字典中。
        /// </summary>
        /// <param name="state">目标状态实例。</param>
        /// <returns>对应的状态节点对象。</returns>
        StateNode GetOrAddNode(IState state)
        {
            var node = nodes.GetValueOrDefault(state.GetType());
            if (node == null)
            {
                // 节点不存在时进行初始化并注册到字典
                node = new StateNode(state);
                nodes[state.GetType()] = node;
            }

            return node;
        }

        /// <summary>
        /// 状态节点内部类，用于封装状态实例及其关联的出边（转换规则）。
        /// </summary>
        class StateNode
        {
            /// <summary>
            /// 获取该节点封装的状态实例。
            /// </summary>
            public IState State { get; }

            /// <summary>
            /// 获取该节点所有的出边转换规则集合。
            /// </summary>
            public HashSet<Transition> Transitions { get; }

            /// <summary>
            /// 初始化状态节点实例。
            /// </summary>
            /// <param name="state">要封装的状态实例。</param>
            public StateNode(IState state)
            {
                State = state;
                Transitions = new HashSet<Transition>();
            }

            /// <summary>
            /// 为当前节点添加一条指向目标状态的转换规则。
            /// </summary>
            /// <typeparam name="T">转换条件的类型。</typeparam>
            /// <param name="to">目标状态实例。</param>
            /// <param name="predicate">触发转换的条件实例。</param>
            public void AddTransition<T>(IState to, T predicate)
            {
                Transitions.Add(new Transition<T>(to, predicate));
            }
        }
    }
}