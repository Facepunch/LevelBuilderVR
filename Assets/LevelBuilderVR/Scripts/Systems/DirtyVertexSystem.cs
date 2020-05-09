using Unity.Collections;
using Unity.Entities;

namespace LevelBuilderVR.Systems
{
    /// <summary>
    /// Find all <see cref="Vertex"/> entities with <see cref="DirtyMesh"/>,
    /// remove it, and apply it to the associated <see cref="Room"/> entities.
    /// </summary>
    [UpdateAfter(typeof(CopyRoomSystem))]
    public class DirtyVertexSystem : ComponentSystem
    {
        protected override void OnUpdate()
        {
            var getDirtyMesh = GetComponentDataFromEntity<DirtyMesh>(true);

            Entities
                .WithAllReadOnly<HalfEdge>()
                .ForEach((Entity entity, ref HalfEdge halfEdge) =>
            {
                if (getDirtyMesh.HasComponent(halfEdge.Vertex))
                {
                    PostUpdateCommands.RemoveComponent<DirtyMesh>(halfEdge.Vertex);
                    PostUpdateCommands.AddComponent<DirtyMesh>(halfEdge.Room);
                }
            });
        }
    }
}
