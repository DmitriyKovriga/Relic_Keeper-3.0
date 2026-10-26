// URP 17.3 sprite albedo path plus an opt-in normal pass. The albedo and world
// grading are unchanged; only source-light response within the region varies.
Shader "RelicKeeper/Sprites/Stone Relief"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _NormalMap ("Normal Map", 2D) = "bump" {}
        [HideInInspector] _RKStoneReliefReceiver ("Relief receiver", Float) = 0
        [HideInInspector] _RKStoneReliefRegion ("Relief region", Vector) = (0,0,0,0)
        [HideInInspector] _RKGradeGroup ("World color group", Float) = 1
        [HideInInspector] _RKGradeInfluence ("World color influence", Float) = 1
        [HideInInspector] _RKGradeIsEffect ("Combat effect", Float) = 0
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SurfaceVertex
            #pragma fragment SurfaceFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"
            #include "RelicLocalLight.hlsl"
            struct Attributes { COMMON_2D_INPUTS half4 color : COLOR; UNITY_SKINNED_VERTEX_INPUTS };
            struct Varyings { COMMON_2D_OUTPUTS float2 lightingUV : TEXCOORD4; half4 color : COLOR; };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            #include "WorldColorGrade.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _RKStoneReliefRegion;
                float _RKStoneReliefReceiver;
                float _RKGradeGroup, _RKGradeInfluence, _RKGradeIsEffect;
                half4 _Color;
            CBUFFER_END
            Varyings SurfaceVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings o = CommonUnlitVertex(input);
                o.lightingUV = ComputeScreenPos(o.positionCS / o.positionCS.w).xy;
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }
            half4 SurfaceFragment(Varyings input) : SV_Target
            {
                half4 color = CommonUnlitFragment(input, input.color);
                color.rgb = RKApplyLocalLight(color.rgb, input.lightingUV, _RKGradeGroup, _RKGradeIsEffect);
                color.rgb = RKGradeColor(color.rgb, _RKGradeGroup, _RKGradeInfluence, _RKGradeIsEffect);
                return color;
            }
            ENDHLSL
        }
        Pass
        {
            Tags { "LightMode"="NormalsRendering" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ReliefVertex
            #pragma fragment ReliefFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            struct Attributes { COMMON_2D_NORMALS_INPUTS float4 color : COLOR; UNITY_SKINNED_VERTEX_INPUTS };
            struct Varyings { COMMON_2D_NORMALS_OUTPUTS float2 world : TEXCOORD4; half4 color : COLOR; };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Normals2DCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _RKStoneReliefRegion;
                float _RKStoneReliefReceiver;
                float _RKGradeGroup, _RKGradeInfluence, _RKGradeIsEffect;
                half4 _Color;
            CBUFFER_END
            float _RKStoneReliefStrength;
            Varyings ReliefVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings o = CommonNormalsVertex(input);
                o.world = TransformObjectToWorld(input.positionOS).xy;
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }
            half4 ReliefFragment(Varyings input) : SV_Target
            {
                half4 albedo = input.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float2 border = min(input.world - _RKStoneReliefRegion.xy, _RKStoneReliefRegion.zw - input.world);
                float region = smoothstep(0, 0.35, min(border.x, border.y));
                half3 normal = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv));
                normal = normalize(lerp(half3(0,0,1), normal, saturate(_RKStoneReliefStrength * _RKStoneReliefReceiver) * region));
                return NormalsRenderingShared(albedo, normal, input.tangentWS, input.bitangentWS, input.normalWS);
            }
            ENDHLSL
        }
    }
}
