using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace LevelBuilderVR.Systems
{
    public class DebugRender : ComponentSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((Entity entity, ref Corner corner) =>
            {
                Debug.DrawLine(new Vector3(corner.X, 0f, corner.Z), new Vector3(corner.X, 8f, corner.Z), Color.green);
            });

            Entities.ForEach((Entity entity, ref Wall wall) =>
            {
                var corner0 = EntityManager.GetComponentData<Corner>(wall.Anchor0.Corner);
                var corner1 = EntityManager.GetComponentData<Corner>(wall.Anchor1.Corner);

                Debug.DrawLine(new Vector3(corner0.X, 0f, corner0.Z), new Vector3(corner1.X, 0f, corner1.Z), Color.white);
            });
        }
    }
}