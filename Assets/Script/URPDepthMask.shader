Shader "Custom/URPDepthMask"
{
    SubShader
    {
        // Queue = Transparent-1 (2999) ensures it renders AFTER the skybox and grass, 
        // completely preventing the smeared sky glitch, but BEFORE the transparent rabbit.
        Tags { "RenderType"="Transparent" "Queue"="Transparent-1" "RenderPipeline"="UniversalPipeline" }
        
        Pass
        {
            // Write to the depth buffer to hide things behind it
            ZWrite On
            // Do NOT draw any actual colors (makes it completely invisible)
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half4 frag() : SV_Target
            {
                return 0; // Return blank
            }
            ENDHLSL
        }
    }
}
