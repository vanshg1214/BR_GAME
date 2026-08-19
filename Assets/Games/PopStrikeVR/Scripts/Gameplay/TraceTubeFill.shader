Shader "PopstrikeVR/TraceTubeFill"
{
    Properties
    {
        [HDR] _GreenColor ("Base Color (Green)", Color) = (0.2, 1.0, 0.3, 0.8)
        [HDR] _GoldColor ("Fill Color (Gold)", Color) = (1.0, 0.8, 0.0, 1.0)
        _FillAmount ("Fill Amount", Range(0,1)) = 0.0
        _EdgeSharpness ("Edge Sharpness", Float) = 4.0
        _InnerAlpha ("Inner Alpha", Range(0,1)) = 0.1
        _IsReversed ("Is Reversed", Float) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+2990" "IgnoreProjector"="True" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            float4 _GreenColor;
            float4 _GoldColor;
            float _FillAmount;
            float _EdgeSharpness;
            float _InnerAlpha;
            float _IsReversed;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv; // x is length along line, y is width
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Edge glow math: distance from center (0.5)
                float distFromCenter = abs(i.uv.y - 0.5) * 2.0; // 0 at center, 1 at edge
                
                // Curve the glow for sharp rim
                float edgeGlow = pow(distFromCenter, _EdgeSharpness);
                
                // Base alpha is inner alpha + edge glow
                float finalAlpha = max(_InnerAlpha, edgeGlow);
                
                // Determine if this pixel is behind the finger (filled)
                // If normal direction, fill from 0 to _FillAmount
                // If reversed, fill from 1 down to _FillAmount
                float fillMask = _IsReversed > 0.5 ? step(_FillAmount, i.uv.x) : step(i.uv.x, _FillAmount);
                
                // Soften the boundary slightly so it's not perfectly pixelated
                float softBlend = _IsReversed > 0.5 
                    ? smoothstep(_FillAmount - 0.02, _FillAmount, i.uv.x)
                    : smoothstep(i.uv.x - 0.02, i.uv.x, _FillAmount);
                
                float4 finalColor = lerp(_GreenColor, _GoldColor, softBlend);
                finalColor.a = finalAlpha * finalColor.a;
                
                return finalColor;
            }
            ENDCG
        }
    }
}
