using System.IO;
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

        public Color HoverTint = new Color(0.5f, 0.5f, 0.5f, 1f);
        public Color SelectedTint = new Color(0.89f, 0.596f, 0.09f, 1f);
        public Color HoverSelectedTint = new Color(1f, 0.675f, 0.098f, 1f);

        public Mesh VertexWidgetMesh;
        public Material VertexWidgetBaseMaterial;

        [HideInInspector]
        public Material VertexWidgetHoverMaterial;
        [HideInInspector]
        public Material VertexWidgetSelectedMaterial;
        [HideInInspector]
        public Material VertexWidgetHoverSelectedMaterial;

        public string FilePath;

        public GridGuide GridGuide;

        private void Start()
        {
            VertexWidgetHoverMaterial = Instantiate(VertexWidgetBaseMaterial);
            VertexWidgetHoverMaterial.SetColor("_Emission", HoverTint);

            VertexWidgetSelectedMaterial = Instantiate(VertexWidgetBaseMaterial);
            VertexWidgetSelectedMaterial.SetColor("_Emission", SelectedTint);

            VertexWidgetHoverSelectedMaterial = Instantiate(VertexWidgetBaseMaterial);
            VertexWidgetHoverSelectedMaterial.SetColor("_Emission", HoverSelectedTint);

            SetDragOffset(Vector3.zero);

            if (!string.IsNullOrEmpty(FilePath) && File.Exists(FilePath))
            {

            }
            else
            {
                Level = World.DefaultGameObjectInjectionWorld.EntityManager.CreateLevelTemplate(new float3(8f, 3f, 12f));
            }
        }

        public void SetDragOffset(Vector3 offset)
        {
            // TODO

            var scale = math.length(transform.localScale.x) / (1f / 64f);

            VertexWidgetSelectedMaterial.SetVector("_DragOffset", offset * scale);
            VertexWidgetHoverSelectedMaterial.SetVector("_DragOffset", offset * scale);
        }

        private void Update()
        {
            if (Level == Entity.Null)
            {
                return;
            }

            GridGuide.transform.localRotation = transform.localRotation * Quaternion.AngleAxis(90f, Vector3.right);

            GridGuide.transform.localScale = Vector3.one
                * GridGuide.MinorDivisionSize
                * GridGuide.MinorDivisionsPerUnit
                * transform.localScale.x;

            var localGuideOrigin = GridGuide.Origin;
            var majorSize = GridGuide.MinorDivisionSize * GridGuide.MinorDivisionsPerMajor;

            localGuideOrigin.x = Mathf.Round(localGuideOrigin.x / majorSize) * majorSize;
            localGuideOrigin.z = Mathf.Round(localGuideOrigin.z / majorSize) * majorSize;

            GridGuide.transform.localPosition = transform.TransformPoint(localGuideOrigin);
            GridGuide.WorldSpaceOrigin = transform.TransformPoint(GridGuide.Origin);

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
