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
        private EntityQuery _getVerticesVisible;

        protected override void OnCreate()
        {
            _getVerticesVisible = Entities
                .WithAllReadOnly<Vertex, WithinLevel>()
                .WithAll<LocalToWorld, RenderBounds>()
                .WithNone<Hidden>()
                .ToEntityQuery();
        }

        protected override void OnUpdate()
        {
            Entities
                .WithAllReadOnly<Level, WidgetsVisible, LocalToWorld>()
                .ForEach((Entity levelEntity, ref WidgetsVisible widgetsVisible, ref LocalToWorld levelLocalToWorld) =>
                {
                    _getVerticesVisible.SetSharedComponentFilter(new WithinLevel(levelEntity));

                    var vertices = _getVerticesVisible.ToComponentDataArray<Vertex>(Allocator.TempJob);
                    var localToWorlds = _getVerticesVisible.ToComponentDataArray<LocalToWorld>(Allocator.TempJob);
                    var renderBoundsArr = _getVerticesVisible.ToComponentDataArray<RenderBounds>(Allocator.TempJob);

                    // TODO
                    var xScale = (1f / 64f) / math.length(levelLocalToWorld.Value.c0);

                    for (var i = 0; i < vertices.Length; ++i)
                    {
                        var vertex = vertices[i];
                        var localToWorld = localToWorlds[i];
                        var renderBounds = renderBoundsArr[i];

                        var translation = new float3(vertex.X, (vertex.MinY + vertex.MaxY) * 0.5f, vertex.Z);
                        var scale = new float3(1f * xScale, (vertex.MaxY - vertex.MinY) * 0.5f, 1f * xScale);
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

                    _getVerticesVisible.CopyFromComponentDataArray(localToWorlds);
                    _getVerticesVisible.CopyFromComponentDataArray(renderBoundsArr);

                    vertices.Dispose();
                    renderBoundsArr.Dispose();
                    localToWorlds.Dispose();
                });
        }
    }
}
