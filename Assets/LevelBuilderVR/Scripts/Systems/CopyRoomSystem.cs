using LevelBuilderVR.Entities;
using Unity.Entities;

namespace LevelBuilderVR.Systems
{
    [UpdateAfter(typeof(VertexMergeSystem))]
    public class CopyRoomSystem : ComponentSystem
    {
        protected override void OnUpdate()
        {
            var getFlatFloor = GetComponentDataFromEntity<FlatFloor>(true);
            var getSlopedFloor = GetComponentDataFromEntity<SlopedFloor>(true);

            var getFlatCeiling = GetComponentDataFromEntity<FlatCeiling>(true);
            var getSlopedCeiling = GetComponentDataFromEntity<SlopedCeiling>(true);

            Entities
                .WithAllReadOnly<CopyRoom>()
                .ForEach((Entity entity, ref CopyRoom copyRoom) =>
                {
                    PostUpdateCommands.RemoveComponent<CopyRoom>(entity);

                    var srcRoom = copyRoom.Room;

                    PostUpdateCommands.SetSharedComponent(entity, EntityManager.GetSharedComponentData<WithinLevel>(srcRoom));

                    if (getFlatFloor.HasComponent(srcRoom))
                    {
                        PostUpdateCommands.AddComponent(entity, getFlatFloor[srcRoom]);
                    }
                    else if (getSlopedFloor.HasComponent(srcRoom))
                    {
                        PostUpdateCommands.AddComponent(entity, getSlopedFloor[srcRoom]);
                    }

                    if (getFlatCeiling.HasComponent(srcRoom))
                    {
                        PostUpdateCommands.AddComponent(entity, getFlatCeiling[srcRoom]);
                    }
                    else if (getSlopedCeiling.HasComponent(srcRoom))
                    {
                        PostUpdateCommands.AddComponent(entity, getSlopedCeiling[srcRoom]);
                    }

                    EntityManager.SetupRoomRendering(entity);
                });
        }
    }
}
