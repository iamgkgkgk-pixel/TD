Shader "Hidden/VisualPolish_BloomBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BlurSize ("Blur Size", Float) = 1.0
    }
    SubShader
    {
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
            float4 _MainTex_TexelSize;
            float _BlurSize;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 texelSize = _MainTex_TexelSize.xy * _BlurSize;
                fixed4 col = fixed4(0,0,0,0);

                // 9-tap高斯模糊（对角线方向）
                col += tex2D(_MainTex, i.uv + float2(-4, -4) * texelSize) * 0.01621622;
                col += tex2D(_MainTex, i.uv + float2(-3, -3) * texelSize) * 0.05405405;
                col += tex2D(_MainTex, i.uv + float2(-2, -2) * texelSize) * 0.12162162;
                col += tex2D(_MainTex, i.uv + float2(-1, -1) * texelSize) * 0.19459459;
                col += tex2D(_MainTex, i.uv) * 0.22702703;
                col += tex2D(_MainTex, i.uv + float2(1, 1) * texelSize) * 0.19459459;
                col += tex2D(_MainTex, i.uv + float2(2, 2) * texelSize) * 0.12162162;
                col += tex2D(_MainTex, i.uv + float2(3, 3) * texelSize) * 0.05405405;
                col += tex2D(_MainTex, i.uv + float2(4, 4) * texelSize) * 0.01621622;

                return col;
            }
            ENDCG
        }
    }
}
