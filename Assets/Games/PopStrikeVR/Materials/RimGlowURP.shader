Shader "PopStrikeVR/RimGlowURP"
{
    Properties
    {
        _BaseColor ("Inner Base Color", Color) = (0.2, 0.2, 0.2, 1)
        [HDR] _RimColor ("Outer Glow Color", Color) = (0, 0.8, 1, 3)
        _RimPower ("Glow Sharpness", Range(0.1, 10.0)) = 3.0
    }
    SubShader
    {
        // Changed to Transparent rendering so alpha works
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            
            // Enable standard alpha blending and disable depth writing
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // Include URP Core HLSL libraries
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 viewDirWS   : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _RimColor;
                float _RimPower;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                
                // Convert Object space to Clip space for rendering
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                
                // Calculate World Normal for lighting math
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                
                // Calculate View Direction (Camera to Object)
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.viewDirWS = GetCameraPositionWS() - positionWS;
                
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 n = normalize(IN.normalWS);
                float3 v = normalize(IN.viewDirWS);
                
                // Calculate Fresnel Effect (1.0 - dot(Normal, ViewDirection))
                float NdotV = saturate(dot(n, v));
                float fresnel = pow(1.0 - NdotV, _RimPower);
                
                // Combine the normal inner color with the glowing HDR edge color
                half3 finalColor = _BaseColor.rgb + (_RimColor.rgb * fresnel);
                
                // The center opacity is controlled by your BaseColor.a
                // The glowing edge forces the alpha to 1 so the glow is always visible
                half finalAlpha = saturate(_BaseColor.a + fresnel);
                
                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
