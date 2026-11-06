using UnityEngine;

namespace AdvancedController
{
    /// <summary>
    /// 天花板检测器组件，用于检测角色或物体是否接触到天花板
    /// </summary>
    public class CeilingDetector : MonoBehaviour
    {
        /// <summary>
        /// 天花板角度限制，当接触面法线与角色上方向的夹角小于此值时认为碰到天花板
        /// </summary>
        public float ceilingAngleLimit = 10f;

        /// <summary>
        /// 是否处于调试模式，启用时会绘制调试射线
        /// </summary>
        public bool isInDebugMode;

        /// <summary>
        /// 调试绘制持续时间
        /// </summary>
        float debugDrawDuration = 2.0f;

        /// <summary>
        /// 记录是否击中天花板的状态标志
        /// </summary>
        bool ceilingWasHit;

        void OnCollisionEnter(Collision collision) => CheckForContact(collision);
        void OnCollisionStay(Collision collision) => CheckForContact(collision);

        /// <summary>
        /// 检查碰撞接触并判断是否碰到天花板
        /// </summary>
        /// <param name="collision">碰撞信息</param>
        void CheckForContact(Collision collision)
        {
            // 检查是否有接触点
            if (collision.contacts.Length == 0) return;

            // 计算接触面法线与角色上方向的夹角
            float angle = Vector3.Angle(-transform.up, collision.contacts[0].normal);

            // 如果夹角小于限制值，则标记碰到天花板
            if (angle < ceilingAngleLimit)
            {
                ceilingWasHit = true;
            }

            // 调试模式下绘制法线方向的射线
            if (isInDebugMode)
            {
                Debug.DrawRay(collision.contacts[0].point, collision.contacts[0].normal, Color.red, debugDrawDuration);
            }
        }

        /// <summary>
        /// 获取是否击中天花板的状态
        /// </summary>
        /// <returns>如果击中天花板返回true，否则返回false</returns>
        public bool HitCeiling() => ceilingWasHit;

        /// <summary>
        /// 重置击中天花板状态
        /// </summary>
        public void Reset() => ceilingWasHit = false;
    }
}