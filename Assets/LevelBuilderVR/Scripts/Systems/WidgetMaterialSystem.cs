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

        protected override void OnUpdate()
        {
            var hybridLevel = _hybridLevel ?? (_hybridLevel = Object.FindObjectOfType<HybridLevel>());

            Entities
                .WithAllReadOnly<Vertex, Hovered, DirtyMaterial>()
                .WithNone<Selected>()
                .WithAll<RenderMesh>()
                .ForEach(entity =>
                {
                    var renderMesh = EntityManager.GetSharedComponentData<RenderMesh>(entity);

                    renderMesh.material = hybridLevel.VertexWidgetHoverMaterial;

                    PostUpdateCommands.SetSharedComponent(entity, renderMesh);
                    PostUpdateCommands.RemoveComponent<DirtyMaterial>(entity);
                });

            Entities
                .WithAllReadOnly<Vertex, Selected, DirtyMaterial>()
                .WithNone<Hovered>()
                .WithAll<RenderMesh>()
                .ForEach(entity =>
                {
                    var renderMesh = EntityManager.GetSharedComponentData<RenderMesh>(entity);

                    renderMesh.material = hybridLevel.VertexWidgetSelectedMaterial;

                    PostUpdateCommands.SetSharedComponent(entity, renderMesh);
                    PostUpdateCommands.RemoveComponent<DirtyMaterial>(entity);
                });

            Entities
                .WithAllReadOnly<Vertex, Hovered, Selected, DirtyMaterial>()
                .WithAll<RenderMesh>()
                .ForEach(entity =>
                {
                    var renderMesh = EntityManager.GetSharedComponentData<RenderMesh>(entity);

                    renderMesh.material = hybridLevel.VertexWidgetHoverSelectedMaterial;

                    PostUpdateCommands.SetSharedComponent(entity, renderMesh);
                    PostUpdateCommands.RemoveComponent<DirtyMaterial>(entity);
                });

            Entities
                .WithAllReadOnly<Vertex, DirtyMaterial>()
                .WithNone<Hovered, Selected>()
                .WithAll<RenderMesh>()
                .ForEach(entity =>
                {
                    var renderMesh = EntityManager.GetSharedComponentData<RenderMesh>(entity);

                    renderMesh.material = hybridLevel.VertexWidgetBaseMaterial;

                    PostUpdateCommands.SetSharedComponent(entity, renderMesh);
                    PostUpdateCommands.RemoveComponent<DirtyMaterial>(entity);
                });
        }
    }
}
