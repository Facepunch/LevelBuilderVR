using LevelBuilderVR.Behaviours;
using LevelBuilderVR.Entities;
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

        private EntityQuery _getRenderableVertices;
        private EntityQuery _getNonRenderableVertices;
        private EntityQuery _getHiddenVertices;
        private EntityQuery _getRenderableFloorCeilings;
        private EntityQuery _getNonRenderableFloorCeilings;
        private EntityQuery _getHiddenFloorCeilings;

        private void CreateQueries<T>(out EntityQuery renderable, out EntityQuery nonRenderable, out EntityQuery hidden)
            where T : IComponentData
        {
            hidden = Entities
                .WithAllReadOnly<T, WithinLevel, RenderMesh>()
                .WithAllReadOnly<Hidden>()
                .ToEntityQuery();

            nonRenderable = Entities
                .WithAllReadOnly<T, WithinLevel>()
                .WithNone<RenderMesh, Hidden>()
                .ToEntityQuery();

            renderable = Entities
                .WithAllReadOnly<T, WithinLevel, RenderMesh>()
                .ToEntityQuery();
        }

        protected override void OnCreate()
        {
            CreateQueries<Vertex>(
                out _getRenderableVertices,
                out _getNonRenderableVertices,
                out _getHiddenVertices);
            CreateQueries<FloorCeiling>(
                out _getRenderableFloorCeilings,
                out _getNonRenderableFloorCeilings,
                out _getHiddenFloorCeilings);
        }

        private void UpdateWidgets(bool visible, Mesh defaultMesh, Material defaultMaterial,
            ref EntityQuery renderable, ref EntityQuery nonRenderable, ref EntityQuery hidden)
        {
            var toHideQuery = renderable;
            var withinLevel = EntityManager.GetWithinLevel(_hybridLevel.Level);

            if (visible)
            {
                toHideQuery = hidden;

                nonRenderable.SetSharedComponentFilter(withinLevel);

                var renderMesh = new RenderMesh
                {
                    mesh = defaultMesh,
                    material = defaultMaterial,
                    castShadows = ShadowCastingMode.Off,
                    receiveShadows = false
                };

                PostUpdateCommands.AddComponent<LocalToWorld>(nonRenderable);
                PostUpdateCommands.AddComponent<RenderBounds>(nonRenderable);
                PostUpdateCommands.AddComponent<DirtyMaterial>(nonRenderable);

                if (defaultMesh == null)
                {
                    var entities = nonRenderable.ToEntityArray(Allocator.TempJob);

                    foreach (var entity in entities)
                    {
                        renderMesh.mesh = new Mesh();
                        renderMesh.mesh.MarkDynamic();

                        PostUpdateCommands.AddSharedComponent(entity, renderMesh);
                    }

                    entities.Dispose();
                }
                else
                {
                    PostUpdateCommands.AddSharedComponent(nonRenderable, renderMesh);
                }
            }

            toHideQuery.SetSharedComponentFilter(withinLevel);

            if (defaultMesh == null)
            {
                var entities = toHideQuery.ToEntityArray(Allocator.TempJob);

                foreach (var entity in entities)
                {
                    var renderMesh = EntityManager.GetSharedComponentData<RenderMesh>(entity);

                    if (renderMesh.mesh != null)
                    {
                        Object.Destroy(renderMesh.mesh);
                    }
                }

                entities.Dispose();
            }

            PostUpdateCommands.RemoveComponent<LocalToWorld>(toHideQuery);
            PostUpdateCommands.RemoveComponent<RenderBounds>(toHideQuery);
            PostUpdateCommands.RemoveComponent<RenderMesh>(toHideQuery);
        }

        protected override void OnUpdate()
        {
            if (_hybridLevel == null)
            {
                _hybridLevel = Object.FindObjectOfType<HybridLevel>();
            }

            Entities
                .WithAllReadOnly<Level, WidgetsVisible>()
                .ForEach((Entity levelEntity, ref WidgetsVisible widgetsVisible) =>
                    {
                        UpdateWidgets(widgetsVisible.Vertex, _hybridLevel.VertexWidgetMesh, _hybridLevel.VertexWidgetBaseMaterial,
                            ref _getRenderableVertices, ref _getNonRenderableVertices, ref _getHiddenVertices);
                        UpdateWidgets(widgetsVisible.FloorCeiling, null, _hybridLevel.FloorCeilingWidgetBaseMaterial,
                            ref _getRenderableFloorCeilings, ref _getNonRenderableFloorCeilings, ref _getHiddenFloorCeilings);

                        if (widgetsVisible.FloorCeiling)
                        {
                            _getNonRenderableFloorCeilings.SetSharedComponentFilter(EntityManager.GetWithinLevel(levelEntity));
                            var floorCeilings = _getNonRenderableFloorCeilings.ToComponentDataArray<FloorCeiling>(Allocator.TempJob);

                            foreach (var floorCeiling in floorCeilings)
                            {
                                if (floorCeiling.Above != Entity.Null)
                                {
                                    PostUpdateCommands.AddComponent<DirtyMesh>(floorCeiling.Above);
                                }

                                if (floorCeiling.Below != Entity.Null)
                                {
                                    PostUpdateCommands.AddComponent<DirtyMesh>(floorCeiling.Below);
                                }
                            }

                            floorCeilings.Dispose();
                        }
                    });
        }
    }
}
