Shader "CubeGame/PilotVertexColor"
{
    // Built-in render pipeline variant of the pilot terrain shader with real-time sun shadows.
    // No Alpha 2013 shaders or textures are used.
    Properties
    {
        _Unused("Unused", Float) = 0
        _DaylightBrightness("Daylight brightness", Range(0, 1)) = 1
        [HideInInspector] _UseVertexAlpha("Use vertex alpha", Float) = 0
        [HideInInspector] _SrcBlend("Source blend", Float) = 1
        [HideInInspector] _DstBlend("Destination blend", Float) = 0
        [HideInInspector] _ZWrite("Depth write", Float) = 1
        [HideInInspector] _HorizonInverseRange("Inverse horizon range", Float) = 0
        [HideInInspector] _HorizonColor("Horizon color", Color) = (0,0,0,1)
    }
    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" }

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"

            float _DaylightBrightness;
            float _UseVertexAlpha;
            float _HorizonInverseRange;
            float4 _HorizonColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                SHADOW_COORDS(1)
                float2 horizonDistance : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 n = UnityObjectToWorldNormal(v.normal);
                float lambert = saturate(dot(n, normalize(float3(0.35, 0.80, 0.25))));
                o.color = float4(v.color.rgb * (0.55 + 0.45 * lambert) * _DaylightBrightness,
                    lerp(1.0, v.color.a, _UseVertexAlpha));
                // Cube.exe 0x6FD928 passes planar and full squared distances to its pixel shaders.
                float3 eyeOffset = (mul(unity_ObjectToWorld, v.vertex).xyz - _WorldSpaceCameraPos)
                    * _HorizonInverseRange;
                o.horizonDistance = float2(dot(eyeOffset.xz, eyeOffset.xz), dot(eyeOffset, eyeOffset));
                TRANSFER_SHADOW(o);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                fixed shadow = SHADOW_ATTENUATION(i);
                // Terrain PS 0x6FF7C8. The existing camera clear color supplies the Unity horizon.
                float t = saturate(i.horizonDistance.x);
                float t2 = t * t;
                float remaining = 1.0 - t2 * t2 * t;
                float fog = 1.0 - remaining * remaining;
                if (_UseVertexAlpha > 0.0 && _HorizonInverseRange > 0.0)
                {
                    // Water PS 0x6FEB50 combines planar fog with a sky fade using the full distance.
                    float clearWater = 1.0 - saturate(t * 1.5 - 0.025);
                    float sky = smoothstep(0.6, 1.0, i.horizonDistance.y);
                    fog = lerp(0.75 * (1.0 - clearWater * clearWater), 1.0, sky * sky);
                }
                return float4(lerp(i.color.rgb * shadow, _HorizonColor.rgb, fog), i.color.a);
            }
            ENDCG
        }

        Pass
        {
            Tags { "LightMode" = "ShadowCaster" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"

            struct v2f { V2F_SHADOW_CASTER; };

            v2f vert(appdata_base v)
            {
                v2f o;
                TRANSFER_SHADOW_CASTER(o);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                SHADOW_CASTER_FRAGMENT(i);
            }
            ENDCG
        }
    }
    Fallback Off
}
