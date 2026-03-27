Shader "Hidden/VisualPolish_PostProcess"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _VignetteIntensity ("Vignette Intensity", Float) = 0.35
        _VignetteSmoothness ("Vignette Smoothness", Float) = 0.4
        _Saturation ("Saturation", Float) = 1.15
        _Contrast ("Contrast", Float) = 1.08
        _Brightness ("Brightness", Float) = 1.02
        _WarmTint ("Warm Tint", Color) = (1, 0.97, 0.92, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float2 uv : TEXCOORD0; float4 vertex : SV_POSITION; };

            sampler2D _MainTex;
            float _VignetteIntensity;
            float _VignetteSmoothness;
            float _Saturation;
            float _Contrast;
            float _Brightness;
            float4 _WarmTint;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);

                // 色温调整
                col.rgb *= _WarmTint.rgb;

                // 亮度
                col.rgb *= _Brightness;

                // 对比度（以0.5为中心）
                col.rgb = (col.rgb - 0.5) * _Contrast + 0.5;

                // 饱和度
                float luma = dot(col.rgb, float3(0.2126, 0.7152, 0.0722));
                col.rgb = lerp(float3(luma, luma, luma), col.rgb, _Saturation);

                // 暗角
                float2 uv = i.uv - 0.5;
                float dist = length(uv);
                float vignette = smoothstep(0.5, 0.5 - _VignetteSmoothness, dist * (1.0 + _VignetteIntensity));
                col.rgb *= vignette;

                return col;
            }
            ENDCG
        }
    }
}
