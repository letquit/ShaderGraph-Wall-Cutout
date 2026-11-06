using Sirenix.OdinInspector;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityUtils
{
    /// <summary>
    /// 定义条件谓词接口，用于评估特定条件是否满足。
    /// </summary>
    public interface IPredicate
    {
        /// <summary>
        /// 评估当前条件。
        /// </summary>
        /// <returns>如果条件满足返回 true，否则返回 false。</returns>
        bool Evaluate();
    }

    /// <summary>
    /// 逻辑与（AND）条件谓词，当且仅当所有子条件都满足时，整体条件才满足。
    /// </summary>
    public class And : IPredicate
    {
        /// <summary>
        /// 子条件规则列表，用于进行逻辑与评估。
        /// </summary>
        [SerializeField] List<IPredicate> rules = new List<IPredicate>();

        /// <summary>
        /// 评估所有子条件，只有当所有子条件都返回 true 时才返回 true。
        /// </summary>
        /// <returns>如果所有子条件均满足返回 true，否则返回 false。</returns>
        public bool Evaluate() => rules.All(r => r.Evaluate());
    }

    /// <summary>
    /// 逻辑或（OR）条件谓词，只要有一个子条件满足，整体条件即满足。
    /// </summary>
    public class Or : IPredicate
    {
        /// <summary>
        /// 子条件规则列表，用于进行逻辑或评估。
        /// </summary>
        [SerializeField] List<IPredicate> rules = new List<IPredicate>();

        /// <summary>
        /// 评估所有子条件，只要有一个子条件返回 true 即返回 true。
        /// </summary>
        /// <returns>如果至少有一个子条件满足返回 true，否则返回 false。</returns>
        public bool Evaluate() => rules.Any(r => r.Evaluate());
    }

    /// <summary>
    /// 逻辑非（NOT）条件谓词，对单个子条件的结果进行取反。
    /// </summary>
    public class Not : IPredicate
    {
        /// <summary>
        /// 需要被取反的单个子条件规则。
        /// </summary>
        [SerializeField, LabelWidth(80)] IPredicate rule;

        /// <summary>
        /// 评估子条件并返回其相反的结果。
        /// </summary>
        /// <returns>如果子条件不满足返回 true，满足则返回 false。</returns>
        public bool Evaluate() => !rule.Evaluate();
    }
}