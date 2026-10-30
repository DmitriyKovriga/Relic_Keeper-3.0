Shader "RelicKeeper/Status/Shock Arc"
{
    Properties
    {
        [PerRendererData] _MainTex ("Animated sprite",2D) = "white" {}
        [HideInInspector] _Color ("Tint",Color) = (1,1,1,1)
        [HideInInspector] _ArcRect ("Sprite bounds",Vector) = (-.5,0,1,1)
        [HideInInspector] _ArcPixels ("Sprite width in pixels",Float) = 24
        [HideInInspector] _ArcTime ("Time",Float) = 0
        [HideInInspector] _ArcSeed ("Discharge shape",Float) = 0
        [HideInInspector] _ArcPulse ("Discharge pulse",Float) = 1
        [HideInInspector] _ArcActive ("Active",Float) = 0
        [HideInInspector] _ArcBuildup ("Buildup",Float) = 0
        [HideInInspector] _ArcActivation ("Activation",Float) = 0
        [HideInInspector] _ArcStrength ("Strength",Float) = 1
        [HideInInspector] _ArcReduced ("Reduced",Float) = 0
        [HideInInspector] _ArcColor ("Arc color",Color) = (.55,.9,1,1)
        [HideInInspector] _ArcCore ("Core color",Color) = (1,1,1,1)
        [HideInInspector] _RKGradeGroup ("World color group",Float) = 1
        [HideInInspector] _RKGradeInfluence ("World color influence",Float) = 1
        [HideInInspector] _RKGradeIsEffect ("Combat effect",Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "DisableBatching"="True" }
        Blend SrcAlpha OneMinusSrcAlpha, Zero One
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
            struct Varyings { COMMON_2D_OUTPUTS half4 color : COLOR; float2 arcUV : TEXCOORD4; };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            #include "WorldColorGrade.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color,_ArcColor,_ArcCore;
                float4 _ArcRect;
                float _ArcPixels,_ArcTime,_ArcSeed,_ArcPulse,_ArcActive,_ArcBuildup,_ArcActivation,_ArcStrength,_ArcReduced;
                float _RKGradeGroup,_RKGradeInfluence,_RKGradeIsEffect;
            CBUFFER_END
            float Hash(float p) { return frac(sin(p * 127.1) * 43758.5453); }
            float Zig(float y,float seed)
            {
                float p = y * 7, i = floor(p), jump = _ArcSeed * 13.19;
                return lerp(Hash(i+seed+jump),Hash(i+1+seed+jump),frac(p)) * 2 - 1;
            }
            Varyings Vert(Attributes input)
            {
                SetUpSpriteInstanceProperties();
                float2 uv = (input.positionOS.xy-_ArcRect.xy)/max(_ArcRect.zw,.001);
                input.positionOS = UnityFlipSprite(input.positionOS,unity_SpriteProps.xy);
                Varyings o = CommonUnlitVertex(input); o.arcUV = uv;
                o.color = input.color * _Color * unity_SpriteColor; return o;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half mask = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv).a;
                float2 uv = input.arcUV;
                float pixel = 1/max(_ArcPixels,1), aa = max(fwidth(uv.x)*.5,.002);
                float active = _ArcActive, progress = saturate(_ArcBuildup);
                float width = pixel * lerp(.9,1,active) * lerp(1,.8,_ArcReduced);
                float center = .48 + (Hash(_ArcSeed+51)-.5)*.24;
                float x = lerp(.48,.32,active) + Zig(uv.y,3)*lerp(.1,.16,active);
                float trace = 1-smoothstep(width-aa,width+aa,abs(uv.x-x));
                float low = lerp(center-.14, .08,active), high = lerp(center+.14+progress*.12,.9,active);
                float span = smoothstep(low,low+.05,uv.y)*(1-smoothstep(high-.05,high,uv.y));
                float second = 1-smoothstep(width-aa,width+aa,abs(uv.x-(.7+Zig(uv.y,23)*.12)));
                second *= active * (1-_ArcReduced) * smoothstep(.2,.25,uv.y)*(1-smoothstep(.85,.92,uv.y));
                float intro = 1-smoothstep(pixel,pixel+aa,abs(uv.x-(.18+uv.y*.64+Zig(uv.y,47)*.07)));
                intro *= _ArcActivation * (1-_ArcReduced*.7) * smoothstep(.12,.2,uv.y)*(1-smoothstep(.85,.92,uv.y));
                float pixelY = pixel * _ArcRect.z / max(_ArcRect.w,.001);
                float partialY = .5 + (Hash(_ArcSeed+71)-.5)*.14 + Zig(uv.x,11)*.045;
                float partial = 1-smoothstep(pixelY*.7,pixelY*1.2+fwidth(uv.y)*.5,abs(uv.y-partialY));
                partial *= smoothstep(.12,.2,uv.x)*(1-smoothstep(.78,.88,uv.x));
                float arcs = lerp(partial*_ArcPulse,max(max(trace*span,second*.8)*_ArcPulse,intro),active);
                float strength = lerp(.38+progress*.32,1,active);
                half3 color = lerp(_ArcColor.rgb,_ArcCore.rgb,smoothstep(.45,.95,arcs)*active);
                color = RKGradeColor(color,_RKGradeGroup,_RKGradeInfluence,_RKGradeIsEffect);
                return half4(color,mask * arcs * strength * _ArcStrength * input.color.a);
            }
            ENDHLSL
        }
    }
}
