Shader "RelicKeeper/Hub Fog"
{
    Properties
    {
        [HideInInspector] _RKGradeGroup ("World color group", Float) = 0
        [HideInInspector] _RKGradeInfluence ("World color influence", Float) = 1
        [HideInInspector] _RKGradeIsEffect ("Combat effect", Float) = 0
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _FadeBottom ("Fade bottom (world Y)", Float) = 0
        _FadeTop ("Invisible above (world Y)", Float) = 6.5
        _Softness ("Edge softness (source pixels)", Range(0, 3)) = 1.5
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex FogVertex
            #pragma fragment FogFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "RelicFX/WorldColorGrade.hlsl"
            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
            };
            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
                float worldY : TEXCOORD2;
            };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            float4 _MainTex_TexelSize;
            CBUFFER_START(UnityPerMaterial)
                float _RKGradeGroup, _RKGradeInfluence, _RKGradeIsEffect;
                half4 _Color;
                float _FadeBottom;
                float _FadeTop;
                float _Softness;
            CBUFFER_END
            Varyings FogVertex(Attributes input)
            {
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings output = CommonUnlitVertex(input);
                output.color = input.color * _Color * unity_SpriteColor;
                output.worldY = TransformObjectToWorld(input.positionOS).y;
                return output;
            }
            half4 FogFragment(Varyings input) : SV_Target
            {
                float2 stepUV = _MainTex_TexelSize.xy * _Softness;
                half4 total = 0;
                // Premultiplied averaging avoids dark fringes around transparent pixels.
                [unroll] for (int y = -1; y <= 1; y++)
                [unroll] for (int x = -1; x <= 1; x++)
                {
                    half4 sample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(x, y) * stepUV);
                    half weight = (x == 0 ? 2 : 1) * (y == 0 ? 2 : 1);
                    total += half4(sample.rgb * sample.a, sample.a) * weight;
                }
                half alpha = total.a / 16;
                half3 color = total.rgb / max(total.a, 0.00001h);
                float height = saturate((input.worldY - _FadeBottom) / max(_FadeTop - _FadeBottom, 0.0001));
                half fade = 1 - smoothstep(0, 1, height);
                color = RKGradeColor(color * input.color.rgb, _RKGradeGroup, _RKGradeInfluence, _RKGradeIsEffect);
                return half4(color, alpha * input.color.a * fade);
            }
            ENDHLSL
        }
    }
}
