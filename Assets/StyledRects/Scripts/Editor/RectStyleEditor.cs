using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Facepunch.UI.Editor
{
    [CustomEditor(typeof(RectStyle))]
    [CanEditMultipleObjects]
    public class RectStyleEditor : UnityEditor.Editor
    {
        private SerializedProperty _color;

        private SerializedProperty _enableGradient;
        private SerializedProperty _gradientAngle;

        private SerializedProperty _borderMode;
        private SerializedProperty _borderColor;
        private SerializedProperty _borderWidth;

        private SerializedProperty _cornerRadius;

        private SerializedProperty _enableBoxShadow;
        private SerializedProperty _boxShadowColor;
        private SerializedProperty _boxShadowBlurRadius;
        private SerializedProperty _boxShadowOffset;
        private SerializedProperty _boxShadowInner;

        private GUIStyle _errorStyle;

        private string _lastGeneratedCss;
        private string _currentEditedCss;

        private bool _cssError;

        private void OnEnable()
        {
            _color = serializedObject.FindProperty(nameof(RectStyle.color));

            _enableGradient = serializedObject.FindProperty(nameof(RectStyle.enableGradient));
            _gradientAngle = serializedObject.FindProperty(nameof(RectStyle.gradientAngle));

            _borderMode = serializedObject.FindProperty(nameof(RectStyle.borderMode));
            _borderColor = serializedObject.FindProperty(nameof(RectStyle.borderColor));
            _borderWidth = serializedObject.FindProperty(nameof(RectStyle.borderWidth));

            _cornerRadius = serializedObject.FindProperty(nameof(RectStyle.cornerRadius));

            _enableBoxShadow = serializedObject.FindProperty(nameof(RectStyle.enableBoxShadow));
            _boxShadowColor = serializedObject.FindProperty(nameof(RectStyle.boxShadowColor));
            _boxShadowBlurRadius = serializedObject.FindProperty(nameof(RectStyle.boxShadowBlurRadius));
            _boxShadowOffset = serializedObject.FindProperty(nameof(RectStyle.boxShadowOffset));
            _boxShadowInner = serializedObject.FindProperty(nameof(RectStyle.boxShadowInner));
        }

        public override void OnInspectorGUI()
        {
            if (_errorStyle == null)
            {
                _errorStyle = new GUIStyle(GUI.skin.label)
                {
                    normal = { textColor = Color.red }
                };
            }

            var target = (RectStyle) this.target;
            var generatedCss = target.GetCss();

            if (generatedCss != _lastGeneratedCss)
            {
                _lastGeneratedCss = generatedCss;
                _currentEditedCss = generatedCss;

                _cssError = false;
            }

            serializedObject.Update();
            EditorGUILayout.LabelField("Style", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_color);
            EditorGUILayout.PropertyField(_enableGradient);
            EditorGUILayout.PropertyField(_enableBoxShadow);
            EditorGUILayout.PropertyField(_cornerRadius);
            EditorGUILayout.PropertyField(_borderMode);

            var modified = false;

            if (_enableGradient.boolValue)
            {
                if (target.gradient == null)
                {
                    target.gradient = new RectStyle.Gradient();
                }

                if (target.gradient.keyPointCount < 1)
                {
                    target.gradient.Add(new RectStyle.GradientKeyPoint
                    {
                        color = Color.black,
                        progress = 0f
                    });
                    target.gradient.Add(new RectStyle.GradientKeyPoint
                    {
                        color = Color.white,
                        progress = 1f
                    });
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Gradient", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_gradientAngle);

                var keyPointCount = EditorGUILayout.IntSlider("Key Points", target.gradient.keyPointCount, 2, RectStyle.Gradient.MaxKeyPoints);

                modified |= target.gradient.keyPointCount != keyPointCount;

                target.gradient.keyPointCount = keyPointCount;

                for (var i = 0; i < keyPointCount; ++i)
                {
                    var keyPoint = target.gradient[i];

                    EditorGUILayout.Space();
                    var newColor = EditorGUILayout.ColorField($"Color {i + 1}", keyPoint.color);
                    var newProgress = EditorGUILayout.FloatField($"Progress {i + 1}", keyPoint.progress * 100f) / 100f;

                    modified |= keyPoint.color != newColor || Math.Abs(keyPoint.progress - newProgress) > 0f;

                    keyPoint.color = newColor;
                    keyPoint.progress = newProgress;

                    target.gradient[i] = keyPoint;
                }
            }

            if ((RectStyle.BorderMode) _borderMode.intValue != RectStyle.BorderMode.None)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Border", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_borderColor);
                EditorGUILayout.PropertyField(_borderWidth);
            }

            if (_enableBoxShadow.boolValue)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Box Shadow", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_boxShadowColor);
                EditorGUILayout.PropertyField(_boxShadowBlurRadius);
                EditorGUILayout.PropertyField(_boxShadowOffset);
                EditorGUILayout.PropertyField(_boxShadowInner);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Paste CSS", EditorStyles.boldLabel);
            var css = EditorGUILayout.TextArea(_currentEditedCss, GUILayout.Height(128f));
            if (_currentEditedCss != css)
            {
                _currentEditedCss = css;
                _cssError = !target.SetCss(css);

                if (!_cssError)
                {
                    modified = true;
                    target.Validate();
                }
            }

            if (_cssError)
            {
                EditorGUILayout.LabelField("Unable to parse CSS!", _errorStyle);
            }

            serializedObject.ApplyModifiedProperties();

            if (modified)
            {
                EditorUtility.SetDirty(target);
            }
        }

        public override Texture2D RenderStaticPreview(string assetPath, Object[] subAssets, int width, int height)
        {
            var dummyTex = new Texture2D(1, 1);
            dummyTex.SetPixels(new[] { Color.white });
            dummyTex.Apply();

            const int gridRes = 8;

            var gridPixels = new Color[gridRes * gridRes];
            for (var i = 0; i < gridPixels.Length; ++i)
            {
                var x = i % gridRes;
                var y = i / gridRes;
                gridPixels[i] = ((x & 1) ^ (y & 1)) == 0 ? new Color(0.75f, 0.75f, 0.75f) : new Color(0.8f, 0.8f, 0.8f);
            }

            var gridTex = new Texture2D(gridRes, gridRes)
            {
                filterMode = FilterMode.Point
            };

            gridTex.SetPixels(gridPixels);
            gridTex.Apply();

            var renderTexture = new RenderTexture(width, height, 8);
            var material = new Material(Shader.Find("UI/StyledRect"));

            ((RectStyle) target).UpdateMaterial(material, new Vector2(64f, 64f));

            RenderTexture.active = renderTexture;

            GL.PushMatrix();
            GL.LoadPixelMatrix(0f, width, height, 0f);

            GL.Clear(true, true, Color.black);

            Graphics.DrawTexture(new Rect(0, 0, width, height), gridTex);

            var margin = width * 0.0625f;
            Graphics.DrawTexture(new Rect(0f, 0f, width, height), dummyTex, new Rect(-margin / width, -margin / height, 1f + 2f * margin / width, 1f + 2f * margin / height), 0, 0, 0, 0, Color.white, material);

            var tex = new Texture2D(width, height);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            GL.PopMatrix();

            RenderTexture.active = null;

            DestroyImmediate(material);
            DestroyImmediate(dummyTex);
            DestroyImmediate(gridTex);
            DestroyImmediate(renderTexture);

            return tex;
        }
    }
}