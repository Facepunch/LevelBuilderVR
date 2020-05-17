using LevelBuilderVR.Behaviours;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace LevelBuilderVR.Systems
{
    [UpdateAfter(typeof(MoveSystem)), UpdateAfter(typeof(WidgetVisibleSystem))]
    public class WidgetMaterialSystem : ComponentSystem
    {
        private HybridLevel _hybridLevel;

        private void HandleMaterials<T>(Material baseMat, Material hoverMat, Material selectedMat, Material hoverSelectedMat)
            where T : struct, IComponentData
        {
            Entities
                .WithAllReadOnly<T, Hovered, DirtyMaterial>()
                .WithNone<Selected>()
                .WithAll<RenderMesh>()
                .ForEach(entity =>
                {
                    var renderMesh = EntityManager.GetSharedComponentData<RenderMesh>(entity);

                    renderMesh.material = hoverMat;

                    PostUpdateCommands.SetSharedComponent(entity, renderMesh);
                    PostUpdateCommands.RemoveComponent<DirtyMaterial>(entity);
                });

            Entities
                .WithAllReadOnly<T, Selected, DirtyMaterial>()
                .WithNone<Hovered>()
                .WithAll<RenderMesh>()
                .ForEach(entity =>
                {
                    var renderMesh = EntityManager.GetSharedComponentData<RenderMesh>(entity);

                    renderMesh.material = selectedMat;

                    PostUpdateCommands.SetSharedComponent(entity, renderMesh);
                    PostUpdateCommands.RemoveComponent<DirtyMaterial>(entity);
                });

            Entities
                .WithAllReadOnly<T, Hovered, Selected, DirtyMaterial>()
                .WithAll<RenderMesh>()
                .ForEach(entity =>
                {
                    var renderMesh = EntityManager.GetSharedComponentData<RenderMesh>(entity);

                    renderMesh.material = hoverSelectedMat;

                    PostUpdateCommands.SetSharedComponent(entity, renderMesh);
                    PostUpdateCommands.RemoveComponent<DirtyMaterial>(entity);
                });

            Entities
                .WithAllReadOnly<T, DirtyMaterial>()
                .WithNone<Hovered, Selected>()
                .WithAll<RenderMesh>()
                .ForEach(entity =>
                {
                    var renderMesh = EntityManager.GetSharedComponentData<RenderMesh>(entity);

                    renderMesh.material = baseMat;

                    PostUpdateCommands.SetSharedComponent(entity, renderMesh);
                    PostUpdateCommands.RemoveComponent<DirtyMaterial>(entity);
                });
        }

        protected override void OnUpdate()
        {
            var hybridLevel = _hybridLevel ?? (_hybridLevel = Object.FindObjectOfType<HybridLevel>());

            HandleMaterials<Vertex>(
                hybridLevel.VertexWidgetBaseMaterial,
                hybridLevel.VertexWidgetHoverMaterial,
                hybridLevel.VertexWidgetSelectedMaterial,
                hybridLevel.VertexWidgetHoverSelectedMaterial);

            HandleMaterials<FloorCeiling>(
                hybridLevel.FloorCeilingWidgetBaseMaterial,
                hybridLevel.FloorCeilingWidgetHoverMaterial,
                hybridLevel.FloorCeilingWidgetSelectedMaterial,
                hybridLevel.FloorCeilingWidgetHoverSelectedMaterial);
        }
    }
}
