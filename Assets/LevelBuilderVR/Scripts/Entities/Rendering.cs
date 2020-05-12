using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using Random = Unity.Mathematics.Random;

namespace LevelBuilderVR.Entities
{
    public static partial class EntityHelper
    {
        private static bool SetMaterialFlag<TFlag>(this EntityManager em, Entity entity, bool enabled)
            where TFlag : struct, IComponentData
        {
            var changed = false;

            if (enabled && !em.HasComponent<TFlag>(entity))
            {
                em.AddComponent<TFlag>(entity);
                changed = true;
            }
            else if (!enabled && em.HasComponent<TFlag>(entity))
            {
                em.RemoveComponent<TFlag>(entity);
                changed = true;
            }

            if (changed && em.HasComponent<RenderMesh>(entity))
            {
                em.AddComponent<DirtyMaterial>(entity);
            }

            return changed;
        }

        public static bool SetHovered(this EntityManager em, Entity entity, bool hovered)
        {
            return em.SetMaterialFlag<Hovered>(entity, hovered);
        }

        public static bool GetSelected(this EntityManager em, Entity entity)
        {
            return em.HasComponent<Selected>(entity);
        }

        public static bool SetSelected(this EntityManager em, Entity entity, bool selected)
        {
            return em.SetMaterialFlag<Selected>(entity, selected);
        }

        public static void DeselectAll(this EntityManager em)
        {
            em.AddComponent<DirtyMaterial>(_sSelectedQuery);
            em.RemoveComponent<Selected>(_sSelectedQuery);
        }

        public static void SetVisible(this EntityManager em, Entity entity, bool value)
        {
            if (value)
            {
                em.RemoveComponent<Hidden>(entity);
            }
            else
            {
                em.AddComponent<Hidden>(entity);
            }
        }

        public static void SetupRoomRendering(this EntityManager em, Entity room)
        {
            em.SetComponentData(room, new LocalToWorld
            {
                Value = float4x4.identity
            });

            var mesh = new Mesh();
            var material = Object.Instantiate(HybridLevel.Material);

            material.color = Color.HSVToRGB(UnityEngine.Random.value, 0.125f, 1f);

            mesh.MarkDynamic();

            em.SetSharedComponentData(room, new RenderMesh
            {
                mesh = mesh,
                material = material,
                castShadows = ShadowCastingMode.Off,
                receiveShadows = true
            });

            em.AddComponent<DirtyMesh>(room);
        }
    }
}
