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

        private EntityQuery _getNonVisibleVertices;
        private EntityQuery _getVisibleVertices;

        protected override void OnCreate()
        {
            _getNonVisibleVertices = Entities
                .WithAllReadOnly<Vertex, WithinLevel>()
                .WithNone<RenderMesh, LocalToWorld, RenderBounds>()
                .ToEntityQuery();

            _getVisibleVertices = Entities
                .WithAllReadOnly<Vertex, WithinLevel, RenderMesh, LocalToWorld, RenderBounds>()
                .ToEntityQuery();
        }

        protected override void OnUpdate()
        {
            var hybridLevel = _hybridLevel ?? (_hybridLevel = Object.FindObjectOfType<HybridLevel>());

            Entities
                .WithAllReadOnly<Level, WidgetsVisible>()
                .ForEach((Entity levelEntity, ref WidgetsVisible widgetsVisible) =>
                {
                    if (widgetsVisible.Vertex)
                    {
                        _getNonVisibleVertices.SetSharedComponentFilter(new WithinLevel(levelEntity));
                        var entities = _getNonVisibleVertices.ToEntityArray(Allocator.TempJob);

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
                    else
                    {
                        _getVisibleVertices.SetSharedComponentFilter(new WithinLevel(levelEntity));
                        var entities = _getVisibleVertices.ToEntityArray(Allocator.TempJob);

                        for (var i = 0; i < entities.Length; ++i)
                        {
                            var entity = entities[i];

                            PostUpdateCommands.RemoveComponent<RenderMesh>(entity);
                            PostUpdateCommands.RemoveComponent<LocalToWorld>(entity);
                            PostUpdateCommands.RemoveComponent<RenderBounds>(entity);
                        }

                        entities.Dispose();
                    }
                });
        }
    }
}
