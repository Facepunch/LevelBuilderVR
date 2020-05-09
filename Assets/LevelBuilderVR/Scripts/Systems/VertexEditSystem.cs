using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

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
            var getHalfEdge = GetComponentDataFromEntity<HalfEdge>(false);

            PostUpdateCommands.AddComponent<DirtyMesh>(_movedVertices);
            PostUpdateCommands.RemoveComponent<Move>(_movedVertices);

            Entities
                .WithAllReadOnly<Move>()
                .WithAll<Vertex>()
                .ForEach((Entity entity, ref Vertex vertex, ref Move move) =>
                {
                    vertex.X += move.Offset.x;
                    vertex.Z += move.Offset.z;
                });
        }
    }
}
