Shader "UI/ShadowValueVignette"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Strength ("Strength", Range(0, 1)) = 0
        _Feather ("Feather", Range(0.001, 1)) = 0.15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "RenderPipeline" = "UniversalPipeline"
        }

        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float _Strength;
                float _Feather;
                float _Aspect;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float t = saturate(_Strength);
                float f = max(0.0001, _Feather);
                // 按当前帧缓冲宽高比校正：等距线在屏幕像素空间为圆，避免仅 UV 正方形下看起来像圆
                float aspect = _ScreenParams.x / max(_ScreenParams.y, 1e-4);
                float2 d = (i.uv - 0.5) * 2.0;
                d.x *= aspect;
                float r = length(d);
                float R = length(float2(aspect, 1.0));
                float m = smoothstep((1.0 - t) * R - f, (1.0 - t) * R + f, r);
                float fadeFull = smoothstep(0.92, 1.0, t);
                float a = lerp(t * m, 1.0, fadeFull);
                half texA = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a;
                a *= texA * i.color.a;
                return half4(0, 0, 0, saturate(a));
            }
            ENDHLSL
        }
    }

    Fallback "UI/Default"
}
