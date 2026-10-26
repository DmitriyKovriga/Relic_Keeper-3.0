Shader "RelicKeeper/Atmosphere/Local Light Shaft"
{
    Properties
    {
        [PerRendererData] _MainTex ("PixelLab shaft", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RKGradeGroup ("World color group", Float) = 3
        [HideInInspector] _RKGradeInfluence ("World color influence", Float) = 1
        [HideInInspector] _RKGradeIsEffect ("Combat effect", Float) = 0
        [HideInInspector] _ShaftImageSize ("Source size", Vector) = (64,160,0,1)
        [HideInInspector] _ShaftShape ("Softness and end fade", Vector) = (1,0.2,0,0)
        [HideInInspector] _ShaftVisibility ("Baked terrain visibility", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
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
            TEXTURE2D(_ShaftVisibility);
            SAMPLER(sampler_ShaftVisibility);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _RKGradeGroup, _RKGradeInfluence, _RKGradeIsEffect;
                float4 _ShaftImageSize;
                float4 _ShaftShape;
                float _ShaftClearPixels[64];
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
                int column = clamp((int)floor(input.uv.x * _ShaftImageSize.x), 0, 63);
                float row = floor((1 - input.uv.y) * _ShaftImageSize.y);
                if (_ShaftImageSize.z > 0.5)
                    clip(SAMPLE_TEXTURE2D(_ShaftVisibility, sampler_ShaftVisibility, input.uv).r - 0.5);
                else clip(_ShaftClearPixels[column] - row - 0.5);
                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                // The authored pixel tones also describe density: the outer band is almost transparent.
                half density = smoothstep(0.06, 0.60, max(texel.r, max(texel.g, texel.b)));
                density = pow(density, _ShaftShape.x);
                half tail = smoothstep(0.02, _ShaftShape.y, 1 - (row + 0.5) / _ShaftImageSize.y);
                half4 color = texel * input.color;
                color.rgb *= _ShaftImageSize.w;
                color.a *= density * tail;
                color.rgb = RKGradeColor(color.rgb, _RKGradeGroup, _RKGradeInfluence, _RKGradeIsEffect);
                return color;
            }
            ENDHLSL
        }
    }
}
