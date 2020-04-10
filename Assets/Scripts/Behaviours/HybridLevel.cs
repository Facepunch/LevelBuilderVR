using LevelBuilderVR.Entities;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace LevelBuilderVR.Behaviours
{
    public class HybridLevel : MonoBehaviour
    {
        private EntityManager _entityManager;
        private Entity _level;

        public Material Material;

        private void Start()
        {
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            _level = _entityManager.CreateLevelTemplate(new float3(8f, 3f, 12f));
        }

        private void Update()
        {
            _entityManager.SetComponentData(_level, new LocalToWorld
            {
                Value = transform.localToWorldMatrix
            });
        }
    }
}
