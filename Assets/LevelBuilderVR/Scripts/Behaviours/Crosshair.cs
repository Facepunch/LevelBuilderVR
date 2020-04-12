using UnityEngine;
using Valve.VR.InteractionSystem;

namespace LevelBuilderVR.Behaviours
{
    [RequireComponent(typeof(MeshRenderer))]
    public class Crosshair : MonoBehaviour
    {
        public Hand Hand;

        private MeshRenderer _meshRenderer;
        private Material _material;
        private Texture2D _defaultTexture;

        private void Start()
        {
            _meshRenderer = GetComponent<MeshRenderer>();
            _meshRenderer.material = _material = Instantiate(_meshRenderer.sharedMaterial);
            _defaultTexture = (Texture2D) _material.mainTexture;
        }

        private void Update()
        {
            transform.rotation = Quaternion.LookRotation(Player.instance.hmdTransform.forward);

            if (Hand != null && Hand.TryGetPointerPosition(out var worldPos))
            {
                transform.position = worldPos;
            }
        }

        public void ResetTexture()
        {
            _material.mainTexture = _defaultTexture;
        }

        public void SetTexture(Texture2D texture)
        {
            _material.mainTexture = texture;
        }
    }
}
