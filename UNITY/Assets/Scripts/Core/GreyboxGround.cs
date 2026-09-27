using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 灰盒地面标记：美术场景提供了地形碰撞（TerrainCollider）时，ArtSceneLoader 关闭它的碰撞体，
    /// 让玩家走在自然地形上；没有美术地形时仍用它作为地面。
    /// </summary>
    public class GreyboxGround : MonoBehaviour
    {
    }
}
