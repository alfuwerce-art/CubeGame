Shader "CubeGame/PilotVertexColor"
{
    // Built-in render pipeline variant of the pilot terrain shader.
    // The URP variant is added in a separate commit once the URP package is confirmed installed
    // (see docs/stages/S01_PILOT_REPORT.md). No Alpha 2013 shaders or textures are used.
    Properties
    {
        _Unused("Unused", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

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
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 n = UnityObjectToWorldNormal(v.normal);
                float lambert = saturate(dot(n, normalize(float3(0.35, 0.80, 0.25))));
                o.color = float4(v.color.rgb * (0.55 + 0.45 * lambert), 1.0);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                return i.color;
            }
            ENDCG
        }
    }
    Fallback Off
}
