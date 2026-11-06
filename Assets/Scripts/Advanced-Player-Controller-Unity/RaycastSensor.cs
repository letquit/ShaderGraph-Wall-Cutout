using UnityEngine;

namespace AdvancedController
{
    /// <summary>
    /// 射线传感器类，用于在指定方向发射射线检测碰撞
    /// </summary>
    public class RaycastSensor
    {
        /// <summary>
        /// 射线长度，默认为1米
        /// </summary>
        public float castLength = 1f;

        /// <summary>
        /// 射线检测的层掩码，默认为255（所有层）
        /// </summary>
        public LayerMask layermask = 255;

        /// <summary>
        /// 射线起点相对于变换的本地坐标
        /// </summary>
        private Vector3 origin = Vector3.zero;

        /// <summary>
        /// 关联的变换对象
        /// </summary>
        private Transform tr;

        /// <summary>
        /// 射线投射方向枚举
        /// </summary>
        public enum CastDirection
        {
            Forward, // 前方
            Right, // 右方
            Up, // 上方
            Backward, // 后方
            Left, // 左方
            Down // 下方
        }

        /// <summary>
        /// 当前射线投射方向
        /// </summary>
        private CastDirection castDirection;

        /// <summary>
        /// 射线检测结果信息
        /// </summary>
        private RaycastHit hitInfo;

        /// <summary>
        /// 初始化射线传感器
        /// </summary>
        /// <param name="playerTransform">用于射线检测的变换对象</param>
        public RaycastSensor(Transform playerTransform)
        {
            tr = playerTransform;
        }

        /// <summary>
        /// 执行射线检测
        /// </summary>
        public void Cast()
        {
            Vector3 worldOrigin = tr.TransformPoint(origin);
            Vector3 worldDirection = GetCastDirection();

            Physics.Raycast(worldOrigin, worldDirection, out hitInfo, castLength, layermask,
                QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// 检查是否检测到碰撞
        /// </summary>
        /// <returns>如果检测到碰撞返回true，否则返回false</returns>
        public bool HasDetectedHit() => hitInfo.collider != null;

        /// <summary>
        /// 获取碰撞点距离
        /// </summary>
        /// <returns>从射线起点到碰撞点的距离</returns>
        public float GetDistance() => hitInfo.distance;

        /// <summary>
        /// 获取碰撞表面法线
        /// </summary>
        /// <returns>碰撞表面的法线向量</returns>
        public Vector3 GetNormal() => hitInfo.normal;

        /// <summary>
        /// 获取碰撞点位置
        /// </summary>
        /// <returns>世界空间中的碰撞点坐标</returns>
        public Vector3 GetPosition() => hitInfo.point;

        /// <summary>
        /// 获取碰撞体对象
        /// </summary>
        /// <returns>检测到的碰撞体对象</returns>
        public Collider GetCollider() => hitInfo.collider;

        /// <summary>
        /// 获取碰撞物体的变换组件
        /// </summary>
        /// <returns>检测到的碰撞物体的变换组件</returns>
        public Transform GetTransform() => hitInfo.transform;

        /// <summary>
        /// 设置射线投射方向
        /// </summary>
        /// <param name="direction">要设置的投射方向</param>
        public void SetCastDirection(CastDirection direction) => castDirection = direction;

        /// <summary>
        /// 设置射线投射的起始位置
        /// </summary>
        /// <param name="pos">世界空间中的起始位置</param>
        public void SetCastOrigin(Vector3 pos) => origin = tr.InverseTransformPoint(pos);

        /// <summary>
        /// 根据当前投射方向获取世界空间中的方向向量
        /// </summary>
        /// <returns>对应方向的世界空间向量</returns>
        private Vector3 GetCastDirection()
        {
            return castDirection switch
            {
                CastDirection.Forward => tr.forward,
                CastDirection.Right => tr.right,
                CastDirection.Up => tr.up,
                CastDirection.Backward => -tr.forward,
                CastDirection.Left => -tr.right,
                CastDirection.Down => -tr.up,
                _ => Vector3.one
            };
        }
    }
}