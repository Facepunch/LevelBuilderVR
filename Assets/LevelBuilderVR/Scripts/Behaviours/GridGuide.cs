using UnityEngine;

namespace LevelBuilderVR.Behaviours
{
    [RequireComponent(typeof(MeshRenderer))]
    public class GridGuide : MonoBehaviour
    {
        private MeshRenderer _meshRenderer;
        private MaterialPropertyBlock _propertyBlock;

        public Vector3 Origin;
        public Vector3 WorldSpaceOrigin;
        public float MinorDivisionSize = 1f / 64f;

        public int MinorDivisionsPerMajor = 4;
        public int MinorDivisionsPerUnit = 64;

        private void Awake()
        {
            _meshRenderer = GetComponent<MeshRenderer>();
            _propertyBlock = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            _meshRenderer.enabled = true;
        }

        private void OnDisable()
        {
            _meshRenderer.enabled = false;
        }

        private void Update()
        {
            _propertyBlock.SetVector("_GradientOrigin", WorldSpaceOrigin);
            _meshRenderer.SetPropertyBlock(_propertyBlock);
        }
    }
}
