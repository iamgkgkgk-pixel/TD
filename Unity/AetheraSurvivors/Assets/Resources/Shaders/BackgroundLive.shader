// Unity Shader: BackgroundLive.shader
// 让单张背景图"活"起来 — 区域性UV形变
// 云区水平波动 | 树区摇曳 | 太阳脉动 | 全局微呼吸

Shader "UI/BackgroundLive"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // 云区参数（画面上半部分水平波动）
        _CloudSpeed ("Cloud Speed", Float) = 0.02
        _CloudAmplitude ("Cloud Amplitude", Float) = 0.008
        _CloudFrequency ("Cloud Frequency", Float) = 2.0
        _CloudYStart ("Cloud Y Start (0=bottom,1=top)", Float) = 0.55
        _CloudYEnd ("Cloud Y End", Float) = 0.95

        // 树区参数（画面下方左右摇曳）
        _TreeSpeed ("Tree Speed", Float) = 1.5
        _TreeAmplitude ("Tree Amplitude", Float) = 0.004
        _TreeFrequency ("Tree Frequency", Float) = 3.0
        _TreeYMax ("Tree Y Max (below this)", Float) = 0.35

        // 太阳区参数（画面中部径向脉动）
        _SunCenter ("Sun Center UV", Vector) = (0.4, 0.55, 0, 0)
        _SunRadius ("Sun Effect Radius", Float) = 0.15
        _SunPulseSpeed ("Sun Pulse Speed", Float) = 0.8
        _SunPulseAmplitude ("Sun Pulse Amplitude", Float) = 0.005
        _SunGlowAmplitude ("Sun Glow Brightness", Float) = 0.15

        // 全局微呼吸
        _BreathSpeed ("Breath Speed", Float) = 0.3
        _BreathAmplitude ("Breath Amplitude", Float) = 0.001

        // UI必需
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _ClipRect;

            // 云
            float _CloudSpeed;
            float _CloudAmplitude;
            float _CloudFrequency;
            float _CloudYStart;
            float _CloudYEnd;

            // 树
            float _TreeSpeed;
            float _TreeAmplitude;
            float _TreeFrequency;
            float _TreeYMax;

            // 太阳
            float4 _SunCenter;
            float _SunRadius;
            float _SunPulseSpeed;
            float _SunPulseAmplitude;
            float _SunGlowAmplitude;

        // 呼吸
        float _BreathSpeed;
        float _BreathAmplitude;

        // 城堡刚性区域（此区域内禁止一切UV形变）
        // 城堡位于画面右上：x 55%~88%, y 28%~96%
        // 用smoothstep做柔和过渡，避免形变突然中断
        float CastleRigidMask(float2 uv)
        {
            float maskX = smoothstep(0.52, 0.58, uv.x) * smoothstep(0.92, 0.86, uv.x);
            float maskY = smoothstep(0.25, 0.32, uv.y) * smoothstep(0.98, 0.94, uv.y);
            return maskX * maskY; // 1=城堡内（禁止形变），0=城堡外（允许形变）
        }

            v2f vert(appdata_t v)
            {
                v2f OUT;
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float time = _Time.y;

                // 城堡刚性遮罩（城堡区域内=1，禁止形变）
                float castleRigid = CastleRigidMask(uv);
                float deformable = 1.0 - castleRigid; // 可形变系数

                // ========================================
                // 1. 云区水平波动（上半部分）
                // ========================================
                float cloudMask = smoothstep(_CloudYStart, _CloudYStart + 0.1, uv.y)
                                * smoothstep(_CloudYEnd, _CloudYEnd - 0.1, uv.y);
                float cloudWave = sin(uv.y * _CloudFrequency * 6.28 + time * _CloudSpeed * 6.28) * _CloudAmplitude;
                cloudWave += sin(uv.y * _CloudFrequency * 0.5 * 6.28 + time * _CloudSpeed * 0.3 * 6.28) * _CloudAmplitude * 0.6;
                uv.x += cloudWave * cloudMask * deformable;

                // ========================================
                // 2. 树区摇曳（下方左右区域）
                // ========================================
                float treeMaskY = smoothstep(0.0, _TreeYMax, uv.y) * smoothstep(_TreeYMax + 0.05, _TreeYMax, uv.y);
                float treeMaskLeft = smoothstep(0.3, 0.0, uv.x);
                float treeMaskRight = smoothstep(0.7, 1.0, uv.x);
                float treeMask = max(treeMaskLeft, treeMaskRight) * treeMaskY;
                float treeWave = sin(time * _TreeSpeed + uv.x * _TreeFrequency * 6.28) * _TreeAmplitude * (uv.y / max(_TreeYMax, 0.01));
                uv.x += treeWave * treeMask * deformable;

                // ========================================
                // 3. 太阳径向脉动（城堡区域排除）
                // ========================================
                float2 sunCenterUV = _SunCenter.xy;
                float2 toSun = uv - sunCenterUV;
                toSun.x *= 1.78;
                float distToSun = length(toSun);
                float sunMask = smoothstep(_SunRadius, _SunRadius * 0.3, distToSun);
                float sunPulse = sin(time * _SunPulseSpeed) * _SunPulseAmplitude;
                float2 radialDir = normalize(toSun + 0.0001);
                radialDir.x /= 1.78;
                uv += radialDir * sunPulse * sunMask * deformable;

                // ========================================
                // 4. 全局微呼吸（城堡区域排除）
                // ========================================
                float breath = sin(time * _BreathSpeed) * _BreathAmplitude * deformable;
                uv = (uv - 0.5) * (1.0 + breath) + 0.5;

                // ========================================
                // 采样
                // ========================================
                fixed4 color = tex2D(_MainTex, uv) * IN.color;

                // ========================================
                // 5. 太阳区域增亮（Glow叠加）
                // ========================================
                float sunGlow = sunMask * (0.5 + 0.5 * sin(time * _SunPulseSpeed * 0.7)) * _SunGlowAmplitude;
                color.rgb += sunGlow * fixed3(1.0, 0.9, 0.7);

                // UI Clip
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);

                return color;
            }
            ENDCG
        }
    }
}
