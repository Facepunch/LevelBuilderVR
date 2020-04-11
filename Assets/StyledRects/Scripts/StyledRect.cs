using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements.Experimental;

namespace Facepunch.UI
{
    [ExecuteInEditMode]
    public class StyledRect : MaskableGraphic
    {
        public enum EasingType
        {
            Linear = 0,
            InQuad,
            OutQuad,
            InOutQuad,
            InCubic,
            OutCubic,
            InOutCubic,
            InSine,
            OutSine,
            InOutSine
        }

        [HideInInspector]
        public Shader Shader;

        public RectStyle style;
        public Texture2D texture;

        private ulong _lastChangeId;

        public new RectTransform transform => (RectTransform)base.transform;

        private bool _crossFading;
        private float _crossFadeProgress;
        private float _crossFadeSpeed;
        private EasingType _crossFadeEasing;

        private RectStyle _oldStyle;
        private RectStyle _styleForRendering;
        private bool _ownsMaterial;

        public override Texture mainTexture => texture;

        protected override void OnRectTransformDimensionsChange()
        {
            SetMaterialDirty();

            base.OnRectTransformDimensionsChange();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            if (style != null)
            {
                UpdateFromStyle();
            }

            base.OnValidate();
        }
#endif

        protected override void OnEnable()
        {
            if (Shader == null)
            {
                Shader = Shader.Find("UI/StyledRect");
            }

            if (!_ownsMaterial || material == null || material.shader != Shader)
            {
                material = new Material(Shader);
                _ownsMaterial = true;
            }

            base.OnEnable();
        }

        private void Update()
        {
            if (style == null) return;

            if (_crossFading)
            {
                _crossFadeProgress += _crossFadeSpeed * Time.deltaTime;
                _crossFading = _crossFadeProgress < 1f;

                SetMaterialDirty();
                SetVerticesDirty();
            }

            if (style.ChangeId != _lastChangeId)
            {
                _lastChangeId = style.ChangeId;

                SetMaterialDirty();
                SetVerticesDirty();
            }
        }

        private void UpdateFromStyle()
        {

        }

        public void CrossFadeStyle(RectStyle newStyle, float duration, EasingType easing = EasingType.Linear)
        {
            if (_oldStyle == null)
            {
                _oldStyle = ScriptableObject.CreateInstance<RectStyle>();
            }

            if (style != null)
            {
                _oldStyle.CopyFrom(style);
            }

            style = newStyle;

            if (duration <= 0f)
            {
                _crossFading = false;
                return;
            }

            _crossFading = true;
            _crossFadeProgress = 0f;
            _crossFadeSpeed = 1f / duration;
            _crossFadeEasing = easing;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            var rect = transform.rect;
            var pixelAdjustedRect = this.GetPixelAdjustedRect();
            var color = (Color32)this.color;
            var minUv = new Vector2(0f, 0f);
            var maxUv = new Vector2(1f, 1f);

            if (style?.enableBoxShadow ?? false)
            {
                var minX = Math.Min(0f, style.boxShadowOffset.x - style.boxShadowBlurRadius);
                var minY = Math.Min(0f, style.boxShadowOffset.y - style.boxShadowBlurRadius);
                var maxX = Math.Max(0f, style.boxShadowOffset.x + style.boxShadowBlurRadius);
                var maxY = Math.Max(0f, style.boxShadowOffset.y + style.boxShadowBlurRadius);

                minUv += new Vector2(minX / rect.width, minY / rect.height);
                maxUv += new Vector2(maxX / rect.width, maxY / rect.height);

                pixelAdjustedRect.xMin += minX;
                pixelAdjustedRect.yMin += minY;
                pixelAdjustedRect.xMax += maxX;
                pixelAdjustedRect.yMax += maxY;
            }

            var bounds = new Vector4(pixelAdjustedRect.x, pixelAdjustedRect.y, pixelAdjustedRect.x + pixelAdjustedRect.width, pixelAdjustedRect.y + pixelAdjustedRect.height);

            vh.Clear();
            vh.AddVert(new Vector3(bounds.x, bounds.y), color, new Vector2(minUv.x, minUv.y));
            vh.AddVert(new Vector3(bounds.x, bounds.w), color, new Vector2(minUv.x, maxUv.y));
            vh.AddVert(new Vector3(bounds.z, bounds.w), color, new Vector2(maxUv.x, maxUv.y));
            vh.AddVert(new Vector3(bounds.z, bounds.y), color, new Vector2(maxUv.x, minUv.y));
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }

        private static float Ease(float t, EasingType easingType)
        {
            switch (easingType)
            {
                case EasingType.Linear:
                    return Easing.Linear(t);
                case EasingType.InQuad:
                    return Easing.InQuad(t);
                case EasingType.OutQuad:
                    return Easing.OutQuad(t);
                case EasingType.InOutQuad:
                    return Easing.InOutQuad(t);
                case EasingType.InCubic:
                    return Easing.InCubic(t);
                case EasingType.OutCubic:
                    return Easing.OutCubic(t);
                case EasingType.InOutCubic:
                    return Easing.InOutCubic(t);
                case EasingType.InSine:
                    return Easing.InSine(t);
                case EasingType.OutSine:
                    return Easing.OutSine(t);
                case EasingType.InOutSine:
                    return Easing.InOutSine(t);
                default:
                    return 0;
            }
        }

        protected override void UpdateMaterial()
        {
            if (_styleForRendering == null)
            {
                _styleForRendering = ScriptableObject.CreateInstance<RectStyle>();
            }

            if (_crossFading && _oldStyle != null && style != null)
            {
                _styleForRendering.CopyFromLerped(_oldStyle, style, Ease(_crossFadeProgress, _crossFadeEasing));
            }
            else if (style != null)
            {
                _styleForRendering.CopyFrom(style);
            }

            var mat = materialForRendering;

            _styleForRendering.UpdateMaterial(mat, transform.rect.size);

            base.UpdateMaterial();
        }
    }
}
