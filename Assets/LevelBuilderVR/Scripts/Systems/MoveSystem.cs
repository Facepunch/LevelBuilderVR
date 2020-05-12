using Unity.Entities;

namespace LevelBuilderVR.Systems
{
    public class MoveSystem : ComponentSystem
    {
        private EntityQuery _movedVertices;
        private EntityQuery _movedFloorCeilings;

        protected override void OnCreate()
        {
            _movedVertices = Entities
                .WithAllReadOnly<Move, Vertex>()
                .ToEntityQuery();

            _movedFloorCeilings = Entities
                .WithAllReadOnly<Move, FloorCeiling>()
                .ToEntityQuery();
        }

        protected override void OnUpdate()
        {
            PostUpdateCommands.AddComponent<DirtyMesh>(_movedVertices);
            PostUpdateCommands.RemoveComponent<Move>(_movedVertices);
            PostUpdateCommands.RemoveComponent<Move>(_movedFloorCeilings);

            Entities
                .WithAllReadOnly<Move>()
                .WithAll<Vertex>()
                .ForEach((Entity entity, ref Vertex vertex, ref Move move) =>
                {
                    vertex.X += move.Offset.x;
                    vertex.Z += move.Offset.z;
                });

            Entities
                .WithAllReadOnly<Move>()
                .WithAll<FloorCeiling>()
                .ForEach((Entity entity, ref FloorCeiling floorCeiling, ref Move move) =>
                {
                    floorCeiling.Plane.Point.y += move.Offset.y;

                    if (floorCeiling.Above != Entity.Null)
                    {
                        PostUpdateCommands.AddComponent<DirtyMesh>(floorCeiling.Above);
                    }

                    if (floorCeiling.Below != Entity.Null)
                    {
                        PostUpdateCommands.AddComponent<DirtyMesh>(floorCeiling.Below);
                    }
                });
        }
    }
}
