Shader "RelicKeeper/Hub Building Wind"
{
    Properties
    {
        [HideInInspector] _RKGradeGroup ("World color group", Float) = 0
        [HideInInspector] _RKGradeInfluence ("World color influence", Float) = 1
        [HideInInspector] _RKGradeIsEffect ("Combat effect", Float) = 0
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Period ("Sway period (seconds)", Float) = 6
        _Amplitude ("Sway (source pixels)", Range(0, 1)) = 1
        _Tavern ("Tavern bounds (world min XY, max XY)", Vector) = (-25.2,0,-15.5,7)
        _Warehouse ("Warehouse bounds (world min XY, max XY)", Vector) = (-14.5,0,-6.8,4.5)
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _PreviewTime ("Preview time (-1 uses game time)", Float) = -1
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
            #pragma vertex WindVertex
            #pragma fragment WindFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "RelicFX/WorldColorGrade.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"
            #include "RelicFX/RelicLocalLight.hlsl"
            struct Attributes { COMMON_2D_INPUTS half4 color : COLOR; };
            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
                float2 world : TEXCOORD2;
                float2 lightingUV : TEXCOORD4;
            };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            float4 _MainTex_TexelSize;
            CBUFFER_START(UnityPerMaterial)
                float _RKGradeGroup, _RKGradeInfluence, _RKGradeIsEffect;
                half4 _Color;
                float _Period, _Amplitude, _PreviewTime;
                float4 _Tavern, _Warehouse;
            CBUFFER_END
            Varyings WindVertex(Attributes input)
            {
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings output = CommonUnlitVertex(input);
                output.color = input.color * _Color * unity_SpriteColor;
                output.world = TransformObjectToWorld(input.positionOS).xy;
                output.lightingUV = ComputeScreenPos(output.positionCS / output.positionCS.w).xy;
                return output;
            }
            bool Inside(float2 p, float4 rect)
            { return all(p >= rect.xy) && all(p <= rect.zw); }
            bool Foliage(half4 c)
            {
                // The supplied straw/leaves palette is yellow-green; timber and
                // grey masonry remain fixed, including the chimney and ridge beam.
                // Texture samples are linear colors: the 0.65 ratio includes
                // every straw shade rather than just its brightest highlight.
                return c.a > 0.1h && c.g > c.r * 0.65h && c.g > c.b * 1.55h && c.r > c.b * 1.3h;
            }
            half4 WindFragment(Varyings input) : SV_Target
            {
                half4 original = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                if (!Inside(input.world, _Tavern) && !Inside(input.world, _Warehouse))
                {
                    half4 result = original * input.color;
                    result.rgb = RKApplyLocalLight(result.rgb, input.lightingUV, _RKGradeGroup, _RKGradeIsEffect);
                    result.rgb = RKGradeColor(result.rgb, _RKGradeGroup, _RKGradeInfluence, _RKGradeIsEffect);
                    return result;
                }
                float time = _PreviewTime >= 0 ? _PreviewTime : _Time.y;
                // Small patches have different phases. Whole source-pixel steps
                // preserve sharpness; the building transform never moves.
                float2 patch = floor(input.world * 24 / float2(9, 7));
                float phase = time * 6.2831853 / max(_Period, 0.1) + patch.x * 0.37 + patch.y * 0.59;
                float offset = round(sin(phase) * _Amplitude);
                half4 moved = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(offset, 0) * _MainTex_TexelSize.xy);
                // Never displace structural pixels. Neighboring straw colors
                // drift within the roof, without making gaps in its silhouette.
                half4 result = (Foliage(original) && Foliage(moved) ? moved : original) * input.color;
                result.rgb = RKApplyLocalLight(result.rgb, input.lightingUV, _RKGradeGroup, _RKGradeIsEffect);
                result.rgb = RKGradeColor(result.rgb, _RKGradeGroup, _RKGradeInfluence, _RKGradeIsEffect);
                return result;
            }
            ENDHLSL
        }
    }
}
