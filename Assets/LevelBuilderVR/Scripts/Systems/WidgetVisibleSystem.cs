using LevelBuilderVR.Behaviours;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace LevelBuilderVR.Systems
{
    public class WidgetVisibleSystem : ComponentSystem
    {
        private HybridLevel _hybridLevel;

        private EntityQuery _getNonRenderableVertices;
        private EntityQuery _getRenderableVertices;
        private EntityQuery _getHiddenVertices;

        protected override void OnCreate()
        {
            _getNonRenderableVertices = Entities
                .WithAllReadOnly<Vertex, WithinLevel>()
                .WithNone<RenderMesh, LocalToWorld, RenderBounds, Hidden>()
                .ToEntityQuery();

            _getRenderableVertices = Entities
                .WithAllReadOnly<Vertex, WithinLevel, RenderMesh, LocalToWorld, RenderBounds>()
                .ToEntityQuery();

            _getHiddenVertices = Entities
                .WithAllReadOnly<Vertex, WithinLevel, RenderMesh, LocalToWorld, RenderBounds>()
                .WithAllReadOnly<Hidden>()
                .ToEntityQuery();
        }

        protected override void OnUpdate()
        {
            var hybridLevel = _hybridLevel ?? (_hybridLevel = Object.FindObjectOfType<HybridLevel>());

            Entities
                .WithAllReadOnly<Level, WidgetsVisible>()
                .ForEach((Entity levelEntity, ref WidgetsVisible widgetsVisible) =>
                {
                    var toHideQuery = _getRenderableVertices;

                    NativeArray<Entity> entities;

                    if (widgetsVisible.Vertex)
                    {
                        toHideQuery = _getHiddenVertices;

                        _getNonRenderableVertices.SetSharedComponentFilter(new WithinLevel(levelEntity));
                        entities = _getNonRenderableVertices.ToEntityArray(Allocator.TempJob);

                        var renderMesh = new RenderMesh
                        {
                            mesh = hybridLevel.VertexWidgetMesh,
                            material = hybridLevel.VertexWidgetBaseMaterial,
                            castShadows = ShadowCastingMode.Off,
                            receiveShadows = false
                        };

                        for (var i = 0; i < entities.Length; ++i)
                        {
                            var entity = entities[i];

                            PostUpdateCommands.AddSharedComponent(entity, renderMesh);
                            PostUpdateCommands.AddComponent<LocalToWorld>(entity);
                            PostUpdateCommands.AddComponent<RenderBounds>(entity);
                            PostUpdateCommands.AddComponent<DirtyMaterial>(entity);
                        }

                        entities.Dispose();
                    }

                    toHideQuery.SetSharedComponentFilter(new WithinLevel(levelEntity));
                    entities = toHideQuery.ToEntityArray(Allocator.TempJob);

                    for (var i = 0; i < entities.Length; ++i)
                    {
                        var entity = entities[i];

                        PostUpdateCommands.RemoveComponent<RenderMesh>(entity);
                        PostUpdateCommands.RemoveComponent<LocalToWorld>(entity);
                        PostUpdateCommands.RemoveComponent<RenderBounds>(entity);
                    }

                    entities.Dispose();
                });
        }
    }
}
