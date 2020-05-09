Shader "LevelBuilderVR/StickWidget"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Emission("Emission", Color) = (0,0,0,0)
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0

        _DragOffset ("Drag Offset", Vector) = (0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "ForceNoShadowCasting"="True" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard vertex:vert

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        struct Input
        {
            float dummy;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        fixed4 _Emission;

        float3 _DragOffset;

        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        void vert(inout appdata_full v)
        {
            float3 unitX = mul(unity_ObjectToWorld, float4(1, 0, 0, 0));
            float3 unitY = mul(unity_ObjectToWorld, float4(0, 1, 0, 0));
            float3 origin = mul(unity_ObjectToWorld, float4(0, 0, 0, 0));

            float scaleX = length(unitX - origin);
            float scaleY = length(unitY - origin);

            float src = v.vertex.y;
            float ref = sign(src);
            float offset = src - ref;

            v.vertex.y = ref + offset * scaleX / (scaleY);

            src = v.vertex.x;
            ref = round(src);
            v.vertex.x -= ref;

            v.vertex.xyz += ref * _DragOffset;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = _Color;
            o.Albedo = c.rgb;
            o.Emission = _Emission;
            // Metallic and smoothness come from slider variables
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
