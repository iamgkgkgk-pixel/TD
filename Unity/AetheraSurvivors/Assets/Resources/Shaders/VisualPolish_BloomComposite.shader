Shader "Hidden/VisualPolish_BloomComposite"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BloomTex ("Bloom", 2D) = "black" {}
        _BloomIntensity ("Bloom Intensity", Float) = 0.3
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
            sampler2D _BloomTex;
            float _BloomIntensity;

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
                fixed4 bloom = tex2D(_BloomTex, i.uv);
                col.rgb += bloom.rgb * _BloomIntensity;
                return col;
            }
            ENDCG
        }
    }
}
