// Adapted from URP 17.3 Sprite-Unlit-Default. Keep lighting, normals and sprite deformation in sync on URP upgrades.
Shader "RelicKeeper/Sprites/World Color Unlit"
{
    Properties
    {
        [HideInInspector] _RKGradeGroup ("World color group", Float) = 0
        [HideInInspector] _RKGradeInfluence ("World color influence", Float) = 1
        [HideInInspector] _RKGradeIsEffect ("Combat effect", Float) = 0
        _MainTex ("Sprite Texture", 2D) = "white" {}
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0

        // Legacy properties. They're here so that materials using this shader can gracefully fallback to the legacy sprite shader.
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma target 3.5
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"
            #include "RelicLocalLight.hlsl"

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                float2 lightingUV : TEXCOORD4;
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
          
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            // NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
            #include "WorldColorGrade.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _RKGradeGroup, _RKGradeInfluence, _RKGradeIsEffect;
                half4 _Color;
            CBUFFER_END

            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.lightingUV = ComputeScreenPos(o.positionCS / o.positionCS.w).xy;
                o.color = input.color *_Color * unity_SpriteColor;
                return o;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                half4 result = CommonUnlitFragment(input, input.color);
                result.rgb = RKApplyLocalLight(result.rgb, input.lightingUV, _RKGradeGroup, _RKGradeIsEffect);
                result.rgb = RKGradeColor(result.rgb, _RKGradeGroup, _RKGradeInfluence, _RKGradeIsEffect);
                return result;
            }
            ENDHLSL
        }
    }
}
