using Unity.Entities;
using UnityEngine;

namespace LevelBuilderVR.Systems
{
    public class DebugRender : ComponentSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((Entity entity, ref Vertex corner) =>
            {
                Debug.DrawLine(new Vector3(corner.X, 0f, corner.Z), new Vector3(corner.X, 8f, corner.Z), Color.green);
            });

            Entities.ForEach((Entity entity, ref HalfEdge wall) =>
            {
                var corner0 = EntityManager.GetComponentData<Vertex>(wall.Vertex0);
                var corner1 = EntityManager.GetComponentData<Vertex>(wall.Vertex1);

                Debug.DrawLine(new Vector3(corner0.X, 0f, corner0.Z), new Vector3(corner1.X, 0f, corner1.Z), Color.white);
            });
        }
    }
}