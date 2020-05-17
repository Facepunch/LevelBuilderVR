using System;
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

        public Material FloorCeilingWidgetBaseMaterial;

        [HideInInspector]
        public Material FloorCeilingWidgetHoverMaterial;
        [HideInInspector]
        public Material FloorCeilingWidgetSelectedMaterial;
        [HideInInspector]
        public Material FloorCeilingWidgetHoverSelectedMaterial;

        public Transform ExtrudeWidget;

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

            FloorCeilingWidgetHoverMaterial = Instantiate(FloorCeilingWidgetBaseMaterial);
            FloorCeilingWidgetHoverMaterial.SetColor("_Emission", HoverTint);

            FloorCeilingWidgetSelectedMaterial = Instantiate(FloorCeilingWidgetBaseMaterial);
            FloorCeilingWidgetSelectedMaterial.SetColor("_Emission", SelectedTint);

            FloorCeilingWidgetHoverSelectedMaterial = Instantiate(FloorCeilingWidgetBaseMaterial);
            FloorCeilingWidgetHoverSelectedMaterial.SetColor("_Emission", HoverSelectedTint);

            SetDragOffset(Vector3.zero);

            GridGuide.enabled = false;

            ExtrudeWidget.gameObject.SetActive(false);

            if (!Load(FilePath) && !Load("Example.level"))
            {
                Level = World.DefaultGameObjectInjectionWorld.EntityManager.CreateLevelTemplate(new float3(8f, 3f, 12f));
            }
        }

        private void OnApplicationQuit()
        {
            if (Level != Entity.Null)
            {
                Save(FilePath);
            }
        }

        public void Save(string filePath)
        {
            Debug.Log($"Writing level to \"{filePath}\"");

            var dir = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using (var writer = File.CreateText(filePath))
            {
                var em = World.DefaultGameObjectInjectionWorld.EntityManager;

                em.SaveLevel(Level, writer);
            }
        }

        public bool Load(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            if (!File.Exists(filePath))
            {
                return false;
            }

            Debug.Log($"Loading level from \"{filePath}\"");

            using (var reader = File.OpenText(filePath))
            {
                var em = World.DefaultGameObjectInjectionWorld.EntityManager;

                try
                {
                    Level = em.LoadLevel(reader);
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                    return false;
                }
            }

            return true;
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
