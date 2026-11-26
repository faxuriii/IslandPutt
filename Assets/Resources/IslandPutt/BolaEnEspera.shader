Shader "IslandPutt/Bola en espera"
{
    Properties { _Color ("Color", Color) = (1,1,1,0.45) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END
            struct Input { float4 position : POSITION; float3 normal : NORMAL; };
            struct Output { float4 position : SV_POSITION; half shade : TEXCOORD0; };
            Output Vert(Input v)
            {
                Output o;
                o.position = TransformObjectToHClip(v.position.xyz);
                o.shade = 0.65h + 0.35h * saturate(dot(TransformObjectToWorldNormal(v.normal), normalize(float3(-1,2,1))));
                return o;
            }
            half4 Frag(Output i) : SV_Target { return half4(_Color.rgb * i.shade, _Color.a); }
            ENDHLSL
        }
    }
}
