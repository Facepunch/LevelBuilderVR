Shader "UI/StyledRect"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}

        _Color("Base Color", Color) = (1, 1, 1, 1)

        _GradientStart("Gradient Start", Vector) = (0, 0, 0)
        _GradientEnd("Gradient End", Vector) = (0, 1, 0)

        _GradientKeyPointCount("Gradient Key Point Count", Int) = 2

        _BorderColor("Border Color", Color) = (1, 1, 1, 1)
        _BorderWidths("Border Widths", Vector) = (0, 0, 0, 0)

        _CornerRadius("Curve Radius", Float) = 0

        _BoxShadowColor("Box Shadow Color", Color) = (0, 0, 0, 1)
        _BoxShadowOffset("Box Shadow Offset", Vector) = (0, 0, 0)
        _BoxShadowBlurRadius("Box Shadow Blur Radius", Float) = 0
        _BoxShadowInner("Box Shadow Inner", Float) = 0

        _Size("Size", Vector) = (64, 64, 0)

        [Toggle(CHIPPY_GRADIENT)] _UseGradient("Use Gradient", Float) = 0
        [Toggle(CHIPPY_BORDER)] _UseBorder("Use Border", Float) = 0
        [Toggle(CHIPPY_BOX_SHADOW)] _UseBoxShadow("Use Box Shadow", Float) = 0
        [Toggle(CHIPPY_ROUNDED_CORNERS)] _UseRoundedCorners("Use Rounded Corners", Float) = 0

        // Unity UI Properties

        _StencilComp("Stencil Comparison", Float) = 8
        _Stencil("Stencil ID", Float) = 0
        _StencilOp("Stencil Operation", Float) = 0
        _StencilWriteMask("Stencil Write Mask", Float) = 255
        _StencilReadMask("Stencil Read Mask", Float) = 255

        _ColorMask("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }

        Stencil
        {
            Ref[_Stencil]
            Comp[_StencilComp]
            Pass[_StencilOp]
            ReadMask[_StencilReadMask]
            WriteMask[_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest[unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask[_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile __ CHIPPY_GRADIENT
            #pragma multi_compile __ CHIPPY_BORDER
            #pragma multi_compile __ CHIPPY_BOX_SHADOW
            #pragma multi_compile __ CHIPPY_ROUNDED_CORNERS

            #pragma multi_compile __ UNITY_UI_CLIP_RECT
            #pragma multi_compile __ UNITY_UI_ALPHACLIP

            #define GAMMA 2.2
            #define MAX_KEYPOINTS 4

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            fixed4 _Color;

            fixed4 _GradientColors[MAX_KEYPOINTS];
            float _GradientKeyPoints[MAX_KEYPOINTS];
            float2 _GradientStart;
            float2 _GradientEnd;
            int _GradientKeyPointCount;

            float2 _Size;

            fixed4 _BorderColor;
            float4 _BorderWidths;

            fixed4 _BoxShadowColor;
            float2 _BoxShadowOffset;
            float _BoxShadowBlurRadius;
            float _BoxShadowInner;

            float _CornerRadius;

            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);

                OUT.color = v.color;
                OUT.texcoord = v.texcoord;

                return OUT;
            }

            fixed4 lerpColors(fixed4 a, fixed4 b, float t)
            {
                /*
                float3 gamma = float3(GAMMA, GAMMA, GAMMA);
                float3 invGamma = float3(1.0, 1.0, 1.0) / GAMMA;

                a.rgb = pow(a.rgb, gamma);
                b.rgb = pow(b.rgb, gamma);

                fixed4 mixed = lerp(a, b, t);

                mixed.rgb = pow(mixed.rgb, invGamma);

                return mixed;
                */

                return lerp(a, b, t);
            }

            // Source: http://madebyevan.com/shaders/fast-rounded-rectangle-shadows/
            // License: CC0 (http://creativecommons.org/publicdomain/zero/1.0/)

            // A standard gaussian function, used for weighting samples
            float gaussian(float x, float sigma) {
                const float pi = 3.141592653589793;
                return exp(-(x * x) / (2.0 * sigma * sigma)) / (sqrt(2.0 * pi) * sigma);
            }

            // This approximates the error function, needed for the gaussian integral
            float2 erf2(float2 x)
            {
                float2 s = sign(x), a = abs(x);
                x = 1.0 + (0.278393 + (0.230389 + 0.078108 * (a * a)) * a) * a;
                x *= x;
                return s - s / (x * x);
            }

            float4 erf4(float4 x)
            {
                float4 s = sign(x), a = abs(x);
                x = 1.0 + (0.278393 + (0.230389 + 0.078108 * (a * a)) * a) * a;
                x *= x;
                return s - s / (x * x);
            }

            // Return the mask for the shadow of a box from lower to upper
            float boxShadow(float2 lower, float2 upper, float2 pos, float sigma)
            {
                float4 query = float4(pos - lower, upper - pos);
                float4 integral = 0.5 + 0.5 * erf4(query * (sqrt(0.5) / sigma));
                return min(integral.z, integral.x) * min(integral.w, integral.y);
            }

            // Return the blurred mask along the x dimension
            float roundedBoxShadowX(float x, float y, float sigma, float corner, float2 halfSize)
            {
                float delta = min(halfSize.y - corner - abs(y), 0.0);
                float curved = halfSize.x - corner + sqrt(max(0.0, corner * corner - delta * delta));
                float2 integral = 0.5 + 0.5 * erf2((x + float2(-curved, curved)) * (sqrt(0.5) / sigma));
                return integral.y - integral.x;
            }

            // Return the mask for the shadow of a box from lower to upper
            float roundedBoxShadow(float2 lower, float2 upper, float2 pos, float sigma, float corner)
            {
                // Center everything to make the math easier
                float2 center = (lower + upper) * 0.5;
                float2 halfSize = (upper - lower) * 0.5;
                pos -= center;

                // The signal is only non-zero in a limited range, so don't waste samples
                float low = pos.y - halfSize.y;
                float high = pos.y + halfSize.y;
                float start = clamp(-3.0 * sigma, low, high);
                float end = clamp(3.0 * sigma, low, high);

                // Accumulate samples (we can get away with surprisingly few samples)
                float step = (end - start) / 4.0;
                float y = start + step * 0.5;
                float value = 0.0;
                for (int i = 0; i < 4; i++) {
                    value += roundedBoxShadowX(pos.x, pos.y - y, sigma, corner, halfSize) * gaussian(y, sigma) * step;
                    y += step;
                }

                return value;
            }

            // End 3rd party

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = _Color * tex2D(_MainTex, IN.texcoord);
                float2 pos = IN.texcoord * _Size;

            #ifdef CHIPPY_GRADIENT
                float2 gradientDiff = _GradientEnd - _GradientStart;
                float t = dot(pos - _GradientStart, gradientDiff) / dot(gradientDiff, gradientDiff);

                float negT = -1e+38;
                float posT = 1e+38;

                fixed4 negColor = fixed4(0.0, 1.0, 0.0, 1.0);
                fixed4 posColor = fixed4(1.0, 0.0, 1.0, 1.0);

                for (int i = 0; i < _GradientKeyPointCount && i < MAX_KEYPOINTS; ++i)
                {
                    float kpT = _GradientKeyPoints[i];
                    fixed4 kpColor = _GradientColors[i];

                    bool isNeg = kpT < t && kpT > negT;
                    bool isPos = kpT > t && kpT < posT;

                    negT = isNeg ? kpT : negT;
                    negColor = isNeg ? kpColor : negColor;

                    posT = isPos ? kpT : posT;
                    posColor = isPos ? kpColor : posColor;
                }

                negColor = negT <= -1e+37 ? posColor : negColor;
                posColor = negT <= -1e+37 ? negColor : posColor;

                color *= lerpColors(negColor, posColor, clamp((t - negT) / (posT - negT), 0.0, 1.0));
            #endif

                // X/Y signed distance from the edge of the shape
                float2 sd2 = min(IN.texcoord, float2(1.0, 1.0) - IN.texcoord) * _Size;

            #ifndef CHIPPY_ROUNDED_CORNERS
                float sd = min(sd2.x, sd2.y);
            #endif

            #ifdef CHIPPY_ROUNDED_CORNERS
                float radius = min(_CornerRadius, min(_Size.x * 0.5, _Size.y * 0.5));
                float2 cornerCenter = float2(radius, radius);

                float sd = lerp(min(sd2.x, sd2.y), radius - length(cornerCenter - sd2), float(sd2.x < radius && sd2.y < radius));
                float sdBorder = sd - _BorderWidths.x;

            #elif CHIPPY_BORDER
                sd2 = _Size;

                sd2.x = lerp(sd2.x, min(sd2.x, pos.x - _BorderWidths.x), float(_BorderWidths.x > 0.001));
                sd2.y = lerp(sd2.y, min(sd2.y, pos.y - _BorderWidths.y), float(_BorderWidths.y > 0.001));
                sd2.x = lerp(sd2.x, min(sd2.x, _Size.x - pos.x - _BorderWidths.z), float(_BorderWidths.z > 0.001));
                sd2.y = lerp(sd2.y, min(sd2.y, _Size.y - pos.y - _BorderWidths.w), float(_BorderWidths.w > 0.001));

                float sdBorder = min(sd2.x, sd2.y);
            #endif

            #ifdef CHIPPY_BORDER
                color = lerpColors(_BorderColor, color, clamp((sdBorder + 0.5) * 2.0, 0.0, 1.0));
            #endif

            #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
            #endif

            #ifdef CHIPPY_ROUNDED_CORNERS
                color.a *= clamp((sd + 0.5) * 2.0, 0.0, 1.0);
            #else
                color.a *= float(sd >= 0.0);
            #endif

            #ifdef CHIPPY_BOX_SHADOW
                half4 shadowColor = _BoxShadowColor;

            #ifdef CHIPPY_ROUNDED_CORNERS
                shadowColor.a *= roundedBoxShadow(_BoxShadowOffset, _Size + _BoxShadowOffset, pos, _BoxShadowBlurRadius * 0.25, radius);
            #else
                shadowColor.a *= boxShadow(_BoxShadowOffset, _Size + _BoxShadowOffset, pos, _BoxShadowBlurRadius * 0.25);
            #endif

                shadowColor.a *= max(float(sd < 0.0), _BoxShadowInner);

                half4 mixedColor = half4(color.rgb * color.a + shadowColor.rgb * shadowColor.a * (1 - color.a), color.a + shadowColor.a * (1 - color.a));

                mixedColor.rgb /= mixedColor.a;

                color = mixedColor;
            #endif

            #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
            #endif

                return IN.color * color;
            }
        ENDCG
        }
    }
}
