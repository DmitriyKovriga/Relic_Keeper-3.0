Shader "RelicKeeper/Atmosphere/Source Glow"
{
    Properties
    {
        [PerRendererData] _MainTex ("PixelLab glow density", 2D) = "black" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _GlowSoftness ("Softness", Float) = 1
        [HideInInspector] _RKGradeGroup ("World color group", Float) = 2
        [HideInInspector] _RKGradeInfluence ("World color influence", Float) = 1
        [HideInInspector] _RKGradeIsEffect ("Combat effect", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha One, Zero One
        Cull Off ZWrite Off
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            struct Attributes { COMMON_2D_INPUTS half4 color : COLOR; };
            struct Varyings { COMMON_2D_OUTPUTS half4 color : COLOR; };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            #include "WorldColorGrade.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _GlowSoftness, _RKGradeGroup, _RKGradeInfluence, _RKGradeIsEffect;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 sample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                // PixelLab's grayscale bands encode density. Pure black adds no light.
                half density = sample.r * sample.a;
                density = pow(density, rcp(max(0.25, _GlowSoftness)));
                half3 tint = RKGradeColor(input.color.rgb, _RKGradeGroup, _RKGradeInfluence, _RKGradeIsEffect);
                return half4(tint, density * input.color.a);
            }
            ENDHLSL
        }
    }
}
