// Guaranteed runtime fallback shader (M0 runtime recovery R1).
//
// Lives in a Resources folder, so it is always included in a Player build and is loaded by asset
// reference (Resources.Load), never by Shader.Find over built-in shaders that the build strips.
// Used for every material the game creates in code: marked placeholders, beams, structure blocks.
Shader "CubeGame/SolidColor"
{
    Properties { _Color ("Color", Color) = (1, 1, 1, 1) }
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
            fixed4 _Color;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; };
            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 n = normalize(UnityObjectToWorldNormal(v.normal));
                float k = 0.55 + 0.45 * saturate(dot(n, normalize(float3(0.35, 0.8, 0.5))));
                o.color = fixed4(_Color.rgb * k, 1);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target { return i.color; }
            ENDCG
        }
    }
}
