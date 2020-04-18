using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace LevelBuilderVR.Systems
{
    [UpdateAfter(typeof(WidgetVisibleSystem))]
    public class WidgetTransformSystem : ComponentSystem
    {
        private EntityQuery _getVertices;

        protected override void OnCreate()
        {
            _getVertices = Entities
                .WithAllReadOnly<Vertex, WithinLevel>()
                .WithAll<LocalToWorld, RenderBounds>()
                .ToEntityQuery();
        }

        protected override void OnUpdate()
        {
            Entities
                .WithAllReadOnly<Level, WidgetsVisible, LocalToWorld>()
                .ForEach((Entity levelEntity, ref WidgetsVisible widgetsVisible, ref LocalToWorld levelLocalToWorld) =>
                {
                    _getVertices.SetSharedComponentFilter(new WithinLevel(levelEntity));

                    var vertices = _getVertices.ToComponentDataArray<Vertex>(Allocator.TempJob);
                    var localToWorlds = _getVertices.ToComponentDataArray<LocalToWorld>(Allocator.TempJob);
                    var renderBoundsArr = _getVertices.ToComponentDataArray<RenderBounds>(Allocator.TempJob);

                    for (var i = 0; i < vertices.Length; ++i)
                    {
                        var vertex = vertices[i];
                        var localToWorld = localToWorlds[i];
                        var renderBounds = renderBoundsArr[i];

                        var translation = new float3(vertex.X, (vertex.MinY + vertex.MaxY) * 0.5f, vertex.Z);
                        var scale = new float3(1f, (vertex.MaxY - vertex.MinY) * 0.5f, 1f);
                        var localTransform = float4x4.TRS(translation, quaternion.identity, scale);
                        var finalTransform = math.mul(levelLocalToWorld.Value, localTransform);

                        localToWorld.Value = finalTransform;

                        renderBounds.Value = new AABB
                        {
                            Center = float3.zero,
                            Extents = scale
                        };

                        localToWorlds[i] = localToWorld;
                        renderBoundsArr[i] = renderBounds;
                    }

                    _getVertices.CopyFromComponentDataArray(localToWorlds);
                    _getVertices.CopyFromComponentDataArray(renderBoundsArr);

                    vertices.Dispose();
                    renderBoundsArr.Dispose();
                    localToWorlds.Dispose();
                });
        }
    }
}
