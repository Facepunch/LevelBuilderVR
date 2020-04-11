using LevelBuilderVR.Entities;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace LevelBuilderVR.Behaviours
{
    public class HybridLevel : MonoBehaviour
    {
        private Entity _level;

        public Material Material;

        private void Start()
        {
            _level = World.DefaultGameObjectInjectionWorld.EntityManager.CreateLevelTemplate(new float3(8f, 3f, 12f));
        }

        private void Update()
        {
            if (_level != Entity.Null)
            {
                World.DefaultGameObjectInjectionWorld.EntityManager.SetComponentData(_level, new LocalToWorld
                {
                    Value = transform.localToWorldMatrix
                });
            }
        }
    }
}
