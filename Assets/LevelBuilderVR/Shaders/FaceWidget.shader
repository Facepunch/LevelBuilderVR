Shader "LevelBuilderVR/FaceWidget"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Emission("Emission", Color) = (0,0,0,0)
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _AlphaCutoff ("Alpha Cutoff", Float) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="AlphaTest" "ForceNoShadowCasting"="True" }
        LOD 200

        Cull Off

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard alphatest:_AlphaCutoff

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        struct Input
        {
            float4 screenPos;
        };

        sampler2D _MainTex;

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        fixed4 _Emission;

        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = _Color;
            o.Albedo = c.rgb;
            o.Emission = _Emission;

            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;

            float3 normScreenPos = IN.screenPos.xyz / IN.screenPos.w;
            float2 ditherCoord = normScreenPos.xy * _ScreenParams.xy;

            o.Alpha = (int(ditherCoord.x) ^ int(ditherCoord.y)) & 1;
        }
        ENDCG
    }

    FallBack "Diffuse"
}
