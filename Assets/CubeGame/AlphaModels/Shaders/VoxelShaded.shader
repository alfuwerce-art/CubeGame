// Vertex-colour voxel shader with a fixed key light and real-time sun shadows.
Shader "AlphaTransfer/VoxelShaded"
{
    // _Tint: Alpha's per-draw colour constant (render_setUniform30): hair = hair_rgb/255, others white
    Properties { _LightDir ("Light direction", Vector) = (0.35, 0.8, 0.5, 0) _Tint ("Tint", Color) = (1, 1, 1, 1) }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            float4 _LightDir;
            fixed4 _Tint;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; SHADOW_COORDS(1) };
            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 n = normalize(UnityObjectToWorldNormal(v.normal));
                float k = 0.55 + 0.45 * saturate(dot(n, normalize(_LightDir.xyz)));
                o.color = fixed4(v.color.rgb * _Tint.rgb * k, 1);
                TRANSFER_SHADOW(o);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target { return i.color * SHADOW_ATTENUATION(i); }
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
            v2f vert (appdata_base v) { v2f o; TRANSFER_SHADOW_CASTER(o); return o; }
            float4 frag (v2f i) : SV_Target { SHADOW_CASTER_FRAGMENT(i); }
            ENDCG
        }
    }
    Fallback Off
}
