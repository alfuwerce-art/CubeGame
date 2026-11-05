// Vertex-colour voxel shader with a fixed key light, so part shapes read in screenshots.
Shader "AlphaTransfer/VoxelShaded"
{
    Properties { _LightDir ("Light direction", Vector) = (0.35, 0.8, 0.5, 0) }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _LightDir;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; };
            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 n = normalize(UnityObjectToWorldNormal(v.normal));
                float k = 0.55 + 0.45 * saturate(dot(n, normalize(_LightDir.xyz)));
                o.color = fixed4(v.color.rgb * k, 1);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target { return i.color; }
            ENDCG
        }
    }
    Fallback "Unlit/Color"
}
