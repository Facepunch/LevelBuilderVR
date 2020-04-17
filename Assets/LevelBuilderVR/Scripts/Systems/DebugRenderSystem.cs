using Unity.Entities;
using UnityEngine;

namespace LevelBuilderVR.Systems
{
    [DisableAutoCreation]
    public class DebugRenderSystem : ComponentSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((Entity entity, ref Vertex corner) =>
            {
                Debug.DrawLine(new Vector3(corner.X, 0f, corner.Z), new Vector3(corner.X, 8f, corner.Z), Color.green);
            });

            var getVertex = GetComponentDataFromEntity<Vertex>(true);
            var getHalfEdge = GetComponentDataFromEntity<HalfEdge>(true);

            Entities.ForEach((Entity entity, ref HalfEdge wall) =>
            {
                var corner0 = getVertex[wall.Vertex];
                var corner1 = getVertex[getHalfEdge[wall.Next].Vertex];

                Debug.DrawLine(new Vector3(corner0.X, 0f, corner0.Z), new Vector3(corner1.X, 0f, corner1.Z), Color.white);
            });
        }
    }
}
