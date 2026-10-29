Shader "RelicKeeper/Status/Ignite Burn"
{
    Properties
    {
        [PerRendererData] _MainTex ("Animated sprite", 2D) = "white" {}
        [PerRendererData] _BurnMaskTex ("Burning enemy silhouette", 2D) = "white" {}
        [PerRendererData] _PreviousMaskTex ("Previous emission silhouette", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _BurnRect ("Local sprite bounds", Vector) = (-.5,0,1,1)
        [HideInInspector] _BurnPixels ("Pixel grid", Vector) = (24,36,0,0)
        [HideInInspector] _BurnMode ("Body or rising fire", Float) = 0
        [HideInInspector] _BurnTime ("Presentation time", Float) = 0
        [HideInInspector] _BurnStrength ("Strength", Float) = 1
        [HideInInspector] _BurnHeight ("Tongue height", Float) = 1
        [HideInInspector] _MaskRect ("Silhouette bounds", Vector) = (-.5,0,1,1)
        [HideInInspector] _MaskUVX ("Atlas U mapping", Vector) = (1,0,.5,0)
        [HideInInspector] _MaskUVY ("Atlas V mapping", Vector) = (0,1,0,0)
        [HideInInspector] _BurnArea ("Fire area in body coordinates", Vector) = (-.6,.08,1.2,1.42)
        [HideInInspector] _PreviousMaskRect ("Previous bounds", Vector) = (-.5,0,1,1)
        [HideInInspector] _PreviousMaskUVX ("Previous atlas U", Vector) = (1,0,.5,0)
        [HideInInspector] _PreviousMaskUVY ("Previous atlas V", Vector) = (0,1,0,0)
        [HideInInspector] _MaskBlend ("Emission transition", Float) = 1
        [HideInInspector] _RKGradeGroup ("World color group", Float) = 1
        [HideInInspector] _RKGradeInfluence ("World color influence", Float) = 1
        [HideInInspector] _RKGradeIsEffect ("Combat effect", Float) = 1
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
            struct Varyings { COMMON_2D_OUTPUTS half4 color : COLOR; float2 burnUV : TEXCOORD4; };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            #include "WorldColorGrade.hlsl"
            TEXTURE2D(_BurnMaskTex); SAMPLER(sampler_BurnMaskTex);
            TEXTURE2D(_PreviousMaskTex); SAMPLER(sampler_PreviousMaskTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _BurnRect, _BurnPixels, _MaskRect, _MaskUVX, _MaskUVY, _BurnArea;
                float4 _PreviousMaskRect, _PreviousMaskUVX, _PreviousMaskUVY;
                float _BurnMode, _BurnTime, _BurnStrength, _BurnHeight, _MaskBlend, _RKGradeGroup, _RKGradeInfluence, _RKGradeIsEffect;
            CBUFFER_END
            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1,0)), f.x), lerp(Hash(i + float2(0,1)), Hash(i + 1), f.x), f.y);
            }
            float FireNoise(float2 p) { return Noise(p) * .65 + Noise(p * 2.03 + 17.1) * .35; }
            half Silhouette(float2 position, bool previous)
            {
                float4 rect = previous ? _PreviousMaskRect : _MaskRect;
                float4 rowX = previous ? _PreviousMaskUVX : _MaskUVX, rowY = previous ? _PreviousMaskUVY : _MaskUVY;
                float2 normalized = (position - rect.xy) / max(rect.zw,.001);
                if (any(normalized < 0) || any(normalized > 1)) return 0;
                float2 atlas = float2(dot(float3(position,1),rowX.xyz),dot(float3(position,1),rowY.xyz));
                if (previous) return SAMPLE_TEXTURE2D(_PreviousMaskTex,sampler_PreviousMaskTex,atlas).a;
                return SAMPLE_TEXTURE2D(_BurnMaskTex,sampler_BurnMaskTex,atlas).a;
            }
            float3 Emitter(int index, bool previous)
            {
                float4 rect = previous ? _PreviousMaskRect : _MaskRect;
                float2 anchor = float2(.5,.25 + floor(index * .5) * .21);
                float present = 0;
                [unroll] for (int site = 0; site < 5; site++)
                {
                    float2 candidate = float2(index % 2 == 0 ? .1 + site * .1 : .9 - site * .1,anchor.y);
                    if (_BurnArea.w < 0) candidate.y = 1-candidate.y;
                    float mask = Silhouette(rect.xy + candidate * rect.zw,previous);
                    if (present < .1 && mask >= .1) { anchor = candidate; present = mask; }
                }
                return float3(rect.xy + anchor * rect.zw,present);
            }
            Varyings Vert(Attributes input)
            {
                SetUpSpriteInstanceProperties();
                float2 uv = (input.positionOS.xy - _BurnRect.xy) / max(_BurnRect.zw, .001);
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings o = CommonUnlitVertex(input); o.burnUV = uv;
                o.color = input.color * _Color * unity_SpriteColor; return o;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // Continuous coordinates keep fire moving between the actor's pixel-art frames.
                float2 uv = input.burnUV;
                float time = _BurnTime * 1.65;
                float noise = FireNoise(float2(uv.x * 5, uv.y * 3 - time));
                half alpha, heat;
                if (_BurnMode < .5)
                {
                    half4 body = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                    half luma = dot(body.rgb, half3(.299,.587,.114));
                    // Broken hot ribbons crawl over the sprite, leaving its dark outlines intact.
                    heat = smoothstep(.48,.8,noise);
                    alpha = body.a * heat * .42 * lerp(.05,1,smoothstep(.04,.3,luma));
                }
                else
                {
                    float2 position = _BurnArea.xy + uv * _BurnArea.zw;
                    float2 p = (position - _MaskRect.xy) / max(_MaskRect.zw,.001);
                    if (_BurnArea.w < 0) p.y = 1-p.y;
                    // Evaluate derivatives before divergent tongue branches to avoid unstable tips.
                    float edge = max(fwidth(p.x) * .8,.002);
                    alpha = heat = 0;
                    // Separate, staggered flames grow, curl upward, taper and die.
                    // Only sites on the current sprite emit: no continuous aura shell.
                    [unroll] for (int i = 0; i < 8; i++)
                    {
                        if (_BurnMode > 1.5 && i >= 4) continue;
                        float3 current = Emitter(i,false), previous = current;
                        if (_MaskBlend < .999) previous = Emitter(i,true);
                        float oldWeight = previous.z * (1-_MaskBlend), newWeight = current.z * _MaskBlend;
                        float present = oldWeight + newWeight;
                        if (present < .001) continue;
                        float2 anchor = ((previous.xy * oldWeight + current.xy * newWeight) / present - _MaskRect.xy) / _MaskRect.zw;
                        if (_BurnArea.w < 0) anchor.y = 1-anchor.y;
                        float seed = Hash(float2(i + 3,17));
                        // Two overlapping smooth lives per site keep a replacement flame
                        // present while its predecessor fades, without blinking off.
                        [unroll] for (int cohort = 0; cohort < 2; cohort++)
                        {
                        float age = frac(_BurnTime * (.9 + seed * .3) + seed * 7 + cohort * .5);
                        float life = sin(age * 3.14159265); life *= life;
                        float height = (.42 + seed * .18) * _BurnHeight * (_BurnMode > 1.5 ? .85 : 1);
                        float t = (p.y - anchor.y - age * .09) / max(height,.01);
                        if (t < 0 || t > 1) continue;
                        float sway = (sin(_BurnTime * 4 + seed * 11 - t * 4) * .075 + sin(t * 7 - _BurnTime * 3 + i) * .035) * t;
                        float width = (.11 + seed * .045) * pow(1-t,.85) * (.45 + life * .55);
                        width *= .82 + .18 * sin(_BurnTime * 7 - t * 8 + seed * 13);
                        float distance = abs(p.x - anchor.x - sway);
                        float flame = (1 - smoothstep(width-edge,width+edge,distance)) * life * present * smoothstep(0,.08,t);
                        float core = (1 - smoothstep(width*.2,width*.7,distance)) * (1-t);
                        heat = max(heat,core * flame); alpha = max(alpha,flame);
                        }
                    }
                    if (_BurnMode > 1.5) alpha *= 1 - smoothstep(.7,.9,p.y);
                    alpha *= 1 - smoothstep(.92,1,uv.y);
                }
                half3 fire = lerp(half3(1,.09,.008),half3(1,.4,.025),saturate(heat * 1.4));
                fire = lerp(fire,half3(1,.8,.2),smoothstep(.65,1,heat));
                fire = RKGradeColor(fire, _RKGradeGroup, _RKGradeInfluence, _RKGradeIsEffect);
                return half4(fire, saturate(alpha * _BurnStrength * input.color.a));
            }
            ENDHLSL
        }
    }
}
