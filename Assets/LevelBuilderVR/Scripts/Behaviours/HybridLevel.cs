using LevelBuilderVR.Entities;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace LevelBuilderVR.Behaviours
{
    public class HybridLevel : MonoBehaviour
    {
        public Entity Level { get; private set; }

        public Material Material;

        public Mesh VertexWidgetMesh;
        public Material VertexWidgetMaterial;
        public Material VertexWidgetHoverMaterial;

        private void Start()
        {
            Level = World.DefaultGameObjectInjectionWorld.EntityManager.CreateLevelTemplate(new float3(8f, 3f, 12f));
        }

        private void Update()
        {
            if (Level == Entity.Null)
            {
                return;
            }

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            em.SetComponentData(Level, new LocalToWorld
            {
                Value = transform.localToWorldMatrix
            });

            em.SetComponentData(Level, new WorldToLocal
            {
                Value = transform.worldToLocalMatrix
            });
        }
    }
}
