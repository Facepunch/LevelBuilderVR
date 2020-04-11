using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Facepunch.UI
{
    [CreateAssetMenu(fileName = "Style", menuName = "Facepunch/Rect Style")]
    public class RectStyle : ScriptableObject
    {
        [Serializable]
        public class Gradient : IEnumerable<GradientKeyPoint>
        {
            public const int MaxKeyPoints = 4;

            [Range(0, MaxKeyPoints)] public int keyPointCount;

            // Can't get these to serialize in a list for some reason?

            [SerializeField] private GradientKeyPoint _keyPoint0;
            [SerializeField] private GradientKeyPoint _keyPoint1;
            [SerializeField] private GradientKeyPoint _keyPoint2;
            [SerializeField] private GradientKeyPoint _keyPoint3;

            public void Clear()
            {
                keyPointCount = 0;
            }

            public void Add(GradientKeyPoint keyPoint)
            {
                this[keyPointCount++] = keyPoint;
            }

            public GradientKeyPoint this[int index]
            {
                get
                {
                    switch (index)
                    {
                        case 0:
                            return _keyPoint0;
                        case 1:
                            return _keyPoint1;
                        case 2:
                            return _keyPoint2;
                        case 3:
                            return _keyPoint3;
                        default:
                            return default(GradientKeyPoint);
                    }
                }
                set
                {
                    switch (index)
                    {
                        case 0:
                            _keyPoint0 = value;
                            break;
                        case 1:
                            _keyPoint1 = value;
                            break;
                        case 2:
                            _keyPoint2 = value;
                            break;
                        case 3:
                            _keyPoint3 = value;
                            break;
                    }
                }
            }

            public IEnumerator<GradientKeyPoint> GetEnumerator()
            {
                for (var i = 0; i < keyPointCount && i < MaxKeyPoints; ++i)
                {
                    yield return this[i];
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        [Serializable]
        public struct GradientKeyPoint
        {
            public Color color;
            public float progress;
        }

        [Flags]
        public enum BorderMode
        {
            None,
            Left = 1,
            Top = 2,
            Right = 4,
            Bottom = 8,
            All = Left | Right | Top | Bottom
        }

        public Color color = Color.white;

        public bool enableGradient;
        public float gradientAngle = 0f;

        [SerializeField]
        public Gradient gradient = new Gradient();

        public BorderMode borderMode = BorderMode.None;
        public Color borderColor = Color.white;
        public float borderWidth;

        public float cornerRadius;

        public bool enableBoxShadow;
        public Color boxShadowColor = Color.black;
        public float boxShadowBlurRadius;
        public Vector2 boxShadowOffset;

        [Range(0f, 1f)]
        public float boxShadowInner = 1f;

        public bool HasBorder => borderMode != BorderMode.None;

        public ulong ChangeId { get; private set; }

        public void Validate()
        {
            OnValidate();
        }

        private void OnValidate()
        {
            gradientAngle -= Mathf.Floor((gradientAngle + 180f) / 360f) * 360f;
            ++ChangeId;
        }

        public void CopyFromLerped(RectStyle a, RectStyle b, float t)
        {
            color = Color.Lerp(a.color, b.color, t);

            if ((a.gradient?.keyPointCount ?? 0) != (b.gradient?.keyPointCount ?? 0))
            {
                // TODO
            }
            else if (a.gradient != null && b.gradient != null && a.gradient.keyPointCount > 0)
            {
                gradientAngle = Mathf.LerpAngle(a.gradientAngle, b.gradientAngle, t);

                if (gradient == null)
                {
                    gradient = new Gradient();
                }
                else
                {
                    gradient.Clear();
                }

                for (var i = 0; i < a.gradient.keyPointCount; ++i)
                {
                    gradient.Add(new GradientKeyPoint
                    {
                        color = Color.Lerp(a.gradient[i].color, b.gradient[i].color, t),
                        progress = Mathf.Lerp(a.gradient[i].progress, b.gradient[i].progress, t)
                    });
                }
            }

            enableGradient = a.enableGradient || b.enableGradient;
            borderMode = a.borderMode | b.borderMode;
            borderColor = a.HasBorder != b.HasBorder
                ? a.HasBorder
                    ? a.borderColor
                    : b.borderColor
                : Color.Lerp(a.borderColor, b.borderColor, t);
            borderWidth = Mathf.Lerp(a.HasBorder ? a.borderWidth : 0f, b.HasBorder ? b.borderWidth : 0f, t);

            cornerRadius = Mathf.Lerp(a.cornerRadius, b.cornerRadius, t);

            var boxShadowColorA = a.enableBoxShadow ? a.boxShadowColor : b.boxShadowColor;
            var boxShadowColorB = b.enableBoxShadow ? b.boxShadowColor : a.boxShadowColor;

            if (!a.enableBoxShadow) boxShadowColorA.a = 0f;
            if (!b.enableBoxShadow) boxShadowColorB.a = 0f;

            enableBoxShadow = a.enableBoxShadow || b.enableBoxShadow;
            boxShadowColor = Color.Lerp(boxShadowColorA, boxShadowColorB, t);
            boxShadowBlurRadius = Mathf.Lerp(
                a.enableBoxShadow ? a.boxShadowBlurRadius : b.boxShadowBlurRadius, 
                b.enableBoxShadow ? b.boxShadowBlurRadius : a.boxShadowBlurRadius, t);
            boxShadowOffset = Vector2.Lerp(
                a.enableBoxShadow ? a.boxShadowOffset : b.boxShadowOffset,
                b.enableBoxShadow ? b.boxShadowOffset : a.boxShadowOffset, t);
            boxShadowInner = Mathf.Lerp(
                a.enableBoxShadow ? a.boxShadowInner : b.boxShadowInner,
                b.enableBoxShadow ? b.boxShadowInner : a.boxShadowInner, t);
        }

        public void CopyFrom(RectStyle other)
        {
            color = other.color;

            enableGradient = other.enableGradient;
            gradientAngle = other.gradientAngle;

            gradient?.Clear();

            if (other.gradient != null)
            {
                if (gradient == null) gradient = new Gradient();

                foreach (var keyPoint in other.gradient)
                {
                    gradient.Add(keyPoint);
                }
            }

            borderMode = other.borderMode;
            borderColor = other.borderColor;
            borderWidth = other.borderWidth;

            cornerRadius = other.cornerRadius;

            enableBoxShadow = other.enableBoxShadow;
            boxShadowColor = other.boxShadowColor;
            boxShadowBlurRadius = other.boxShadowBlurRadius;
            boxShadowOffset = other.boxShadowOffset;
            boxShadowInner = other.boxShadowInner;
        }

        private readonly Color[] _gradientColors = new Color[Gradient.MaxKeyPoints];
        private readonly float[] _gradientProgresses = new float[Gradient.MaxKeyPoints];

        public void UpdateMaterial(Material material, Vector2 rectSize)
        {
            material.color = color;

            material.SetVector("_Size", rectSize);

            if (gradient != null && gradient.keyPointCount > 1 && enableGradient)
            {
                material.EnableKeyword("CHIPPY_GRADIENT");

                // TODO: account for rounded corners

                var gradientAngleRads = (90f - gradientAngle) * Mathf.Deg2Rad;
                var gradientDir = new Vector2(Mathf.Cos(gradientAngleRads), Mathf.Sin(gradientAngleRads));

                var halfSize = rectSize * 0.5f;

                // Corner in direction of gradient, X/Y
                var cx = halfSize.x * Mathf.Sign(gradientDir.x);
                var cy = halfSize.y * Mathf.Sign(gradientDir.y);

                // Travel to get to the corner
                var ct = Vector2.Dot(gradientDir, new Vector2(cx, cy));

                var anchorDiff = ct * gradientDir * 2f;
                var anchor0 = anchorDiff * -0.5f + rectSize * 0.5f;

                material.SetVector("_GradientStart", anchor0);
                material.SetVector("_GradientEnd", anchor0 + anchorDiff);

                for (var i = 0; i < gradient.keyPointCount; ++i)
                {
                    _gradientProgresses[i] = gradient[i].progress;

                    if (PlayerSettings.colorSpace == ColorSpace.Linear)
                    {
                        var clr = gradient[i].color;
                        const float gamma = 2.2f;

                        _gradientColors[i] = new Color(
                            Mathf.Pow(clr.r, gamma),
                            Mathf.Pow(clr.g, gamma),
                            Mathf.Pow(clr.b, gamma),
                            clr.a);
                    }
                    else
                    {
                        _gradientColors[i] = gradient[i].color;
                    }
                }

                material.SetInt("_GradientKeyPointCount", gradient.keyPointCount);
                material.SetColorArray("_GradientColors", _gradientColors);
                material.SetFloatArray("_GradientKeyPoints", _gradientProgresses);
            }
            else
            {
                material.DisableKeyword("CHIPPY_GRADIENT");
            }

            if (borderWidth > 0f && borderMode != BorderMode.None)
            {
                var borderWidths = new Vector4(
                    (borderMode & BorderMode.Left) != 0 ? borderWidth : 0f,
                    (borderMode & BorderMode.Bottom) != 0 ? borderWidth : 0f,
                    (borderMode & BorderMode.Right) != 0 ? borderWidth : 0f,
                    (borderMode & BorderMode.Top) != 0 ? borderWidth : 0f);

                material.EnableKeyword("CHIPPY_BORDER");
                material.SetVector("_BorderWidths", borderWidths);
                material.SetColor("_BorderColor", borderColor);
            }
            else
            {
                material.DisableKeyword("CHIPPY_BORDER");
            }

            if (enableBoxShadow && boxShadowColor.a > 0f)
            {
                material.EnableKeyword("CHIPPY_BOX_SHADOW");
                material.SetColor("_BoxShadowColor", boxShadowColor);
                material.SetFloat("_BoxShadowBlurRadius", boxShadowBlurRadius);
                material.SetVector("_BoxShadowOffset", boxShadowOffset);
                material.SetFloat("_BoxShadowInner", boxShadowInner);
            }
            else
            {
                material.DisableKeyword("CHIPPY_BOX_SHADOW");
            }

            if (cornerRadius > 0f)
            {
                material.EnableKeyword("CHIPPY_ROUNDED_CORNERS");
                material.SetFloat("_CornerRadius", cornerRadius);
            }
            else
            {
                material.DisableKeyword("CHIPPY_ROUNDED_CORNERS");
            }
        }

        private string GetColorCss(Color color)
        {
            var color32 = (Color32) color;

            return color32.a == 255
                ? $"#{color32.r:X2}{color32.g:X2}{color32.b:X2}"
                : $"rgba({color32.r}, {color32.g}, {color32.b}, {color.a})";
        }

        private static float ParsePixels(string str)
        {
            var match = _sCssPixels.Match(str);
            if (!match.Success)
            {
                return 0f;
            }

            return float.Parse(match.Groups["value"].Value);
        }

        private static Color ParseColor(string str, float defaultAlpha = 1f)
        {
            var match = _sCssColor.Match(str);
            if (!match.Success)
            {
                return new Color(1f, 0f, 1f, defaultAlpha);
            }

            return ParseColor(match, defaultAlpha);
        }

        private static Color ParseColor(Match match, float defaultAlpha = 1f)
        {
            if (match.Groups["rgb"].Success)
            {
                var rgb = uint.Parse(match.Groups["rgb"].Value, NumberStyles.HexNumber);

                switch (match.Groups["rgb"].Length)
                {
                    case 3:
                        return new Color(((rgb >> 8) & 0xf) / 15f, ((rgb >> 4) & 0xf) / 15f, (rgb & 0xf) / 15f, defaultAlpha);
                    default:
                        return new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, (byte) Mathf.RoundToInt(defaultAlpha * 255f));
                }
            }

            var r = int.Parse(match.Groups["red"].Value);
            var g = int.Parse(match.Groups["green"].Value);
            var b = int.Parse(match.Groups["blue"].Value);
            var a = defaultAlpha;

            if (match.Groups["alpha"].Success)
            {
                a = float.Parse(match.Groups["alpha"].Value);
            }

            return new Color(r / 255f, g / 255f, b / 255f, a);
        }

        private static readonly Regex _sCssComment = new Regex(@"//[^\n]*\n|/\*([^*]|\*[^/])*\*/");
        private static readonly Regex _sCssStatement = new Regex(@"\s*(?<command>[a-zA-Z-][a-zA-Z0-9-]*)\s*:\s*(?<value>[^;]+)\s*;");
        private static readonly Regex _sCssColor = new Regex(@"\#(?<rgb>[0-9a-fA-F]{6}|[0-9a-fA-F]{3})
            |rgb\s*\(\s*(?<red>\d+)\s*,\s*(?<green>\d+)\s*,\s*(?<blue>\d+)\s*\)
            |rgba\s*\(\s*(?<red>\d+)\s*,\s*(?<green>\d+)\s*,\s*(?<blue>\d+)\s*,\s*(?<alpha>\d+(\.\d+)?|\.\d+)\s*\)", RegexOptions.IgnorePatternWhitespace);
        private static readonly Regex _sCssGradientPoint = new Regex($@"(?<color>{_sCssColor})\s*((?<percent>-?\d+(\.\d+)?)\s*%\s*)?", RegexOptions.IgnorePatternWhitespace);
        private static readonly Regex _sCssLinearGradient = new Regex($@"linear-gradient\s*\(\s*(?<angle>-?\d+(\.\d+)?)\s*(deg\s*)?(,\s*(?<point>{_sCssGradientPoint})){{2,4}}\)", RegexOptions.IgnorePatternWhitespace);
        private static readonly Regex _sCssPixels = new Regex(@"(?<value>\d+(\.\d+)?|\.\d+)(px)?");
        private static readonly Regex _sCssBorder = new Regex($@"(?<width>{_sCssPixels})\s+(?<style>[a-zA-Z-][a-zA-Z0-9-]*)\s+(?<color>{_sCssColor})", RegexOptions.IgnorePatternWhitespace);

        public bool SetCss(string css)
        {
            if (css == null)
            {
                return true;
            }

            // Get rid of comments
            css = _sCssComment.Replace(css, match => new string(match.Value
                .Select(x => char.IsWhiteSpace(x) ? x : ' ')
                .ToArray()));

            var newColor = color;
            var newEnableGradient = enableGradient;
            var newGradient = new Gradient();

            if (gradient != null)
            {
                foreach (var keyPoint in gradient)
                {
                    newGradient.Add(keyPoint);
                }
            }

            var newGradientAngle = gradientAngle;

            var newBorderMode = borderMode;
            var newBorderColor = borderColor;
            var newBorderWidth = borderWidth;

            var newCornerRadius = cornerRadius;

            var valid = true;
            var hasOpacity = false;

            foreach (Match match in _sCssStatement.Matches(css))
            {
                var command = match.Groups["command"].Value;
                var value = match.Groups["value"].Value;

                switch (command)
                {
                    case "background":
                    {
                        var colorMatch = _sCssColor.Match(value);
                        if (colorMatch.Success && colorMatch.Index == 0)
                        {
                            newColor = ParseColor(colorMatch, hasOpacity ? newColor.a : 1f);
                            newEnableGradient = false;
                            break;
                        }

                        var gradientMatch = _sCssLinearGradient.Match(value);
                        if (gradientMatch.Success && gradientMatch.Index == 0)
                        {
                            newColor = Color.white;
                            newGradientAngle = float.Parse(gradientMatch.Groups["angle"].Value);
                            newEnableGradient = true;

                            newGradient.Clear();

                            foreach (Capture capture in gradientMatch.Groups["point"].Captures)
                            {
                                var pointMatch = _sCssGradientPoint.Match(capture.Value);

                                newGradient.Add(new GradientKeyPoint
                                {
                                    color = ParseColor(pointMatch.Groups["color"].Value),
                                    progress = pointMatch.Groups["percent"].Success
                                        ? float.Parse(pointMatch.Groups["percent"].Value) / 100f : 0f
                                });
                            }

                            break;
                        }

                        valid = false;
                        break;
                    }
                    case "opacity":
                    {
                        float opacity;
                        if (float.TryParse(value, out opacity))
                        {
                            newColor.a = opacity;
                            hasOpacity = true;
                            break;
                        }

                        valid = false;
                        break;
                    }
                    case "border":
                    {
                        var borderMatch = _sCssBorder.Match(value);
                        if (borderMatch.Success && borderMatch.Index == 0)
                        {
                            newBorderMode = BorderMode.All;
                            newBorderColor = ParseColor(borderMatch.Groups["color"].Value);
                            newBorderWidth = ParsePixels(borderMatch.Groups["width"].Value);

                            valid = true;
                            break;
                        }

                        valid = false;
                        break;
                    }
                    case "border-radius":
                    {
                        var pixelsMatch = _sCssPixels.Match(value);
                        if (pixelsMatch.Success && pixelsMatch.Index == 0)
                        {
                            newCornerRadius = ParsePixels(pixelsMatch.Value);
                            valid = true;
                            break;
                        }

                        valid = false;
                        break;
                    }
                }
            }

            if (valid)
            {
                color = newColor;
                enableGradient = newEnableGradient;
                gradientAngle = newGradientAngle;
                borderMode = newBorderMode;
                borderColor = newBorderColor;
                borderWidth = newBorderWidth;
                cornerRadius = newCornerRadius;
                gradient = newGradient;
            }

            return valid;
        }

        public string GetCss()
        {
            var writer = new StringWriter();

            var opaqueColor = new Color(color.r, color.g, color.b, 1f);

            if (gradient != null && gradient.keyPointCount > 1 && enableGradient)
            {
                writer.WriteLine($"background: linear-gradient({gradientAngle}deg, ");

                for (var i = 0; i < gradient.keyPointCount; ++i)
                {
                    var keyPointColor = gradient[i].color * opaqueColor;
                    writer.WriteLine($"    {GetColorCss(keyPointColor)} {gradient[i].progress * 100f}%{(i == gradient.keyPointCount - 1 ? ");" : ",")}");
                }
            }
            else
            {
                writer.WriteLine($"background: {GetColorCss(opaqueColor * (gradient != null && gradient.keyPointCount == 1 ? gradient[0].color : Color.white))};");
            }

            if (color.a < 1f)
            {
                writer.WriteLine($"opacity: {color.a};");
            }

            if (borderWidth > 0f && borderMode != BorderMode.None)
            {
                writer.WriteLine($"border: {borderWidth}px solid {GetColorCss(borderColor)};");
            }

            if (cornerRadius > 0f)
            {
                writer.WriteLine($"border-radius: {cornerRadius}px;");
            }

            if (enableBoxShadow && boxShadowColor.a > 0f)
            {
                writer.WriteLine($"box-shadow: {boxShadowOffset.x}px {boxShadowOffset.y}px {boxShadowBlurRadius}px 0px {GetColorCss(boxShadowColor)};");
            }

            return writer.ToString();
        }
    }
}
