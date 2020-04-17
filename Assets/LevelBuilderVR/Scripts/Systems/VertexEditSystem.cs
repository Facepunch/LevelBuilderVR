using Unity.Entities;

namespace LevelBuilderVR.Systems
{
    public class VertexEditSystem : ComponentSystem
    {
        private EntityQuery _movedVertices;

        protected override void OnCreate()
        {
            _movedVertices = Entities
                .WithAllReadOnly<Move, Vertex>()
                .ToEntityQuery();
        }

        protected override void OnUpdate()
        {
            Entities
                .WithAllReadOnly<Move>()
                .WithAll<Vertex>()
                .ForEach((Entity entity, ref Vertex vertex, ref Move move) =>
                {
                    vertex.X += move.Offset.x;
                    vertex.Z += move.Offset.z;
                });

            PostUpdateCommands.RemoveComponent<Move>(_movedVertices);
            PostUpdateCommands.AddComponent<DirtyMesh>(_movedVertices);
        }
    }
}
