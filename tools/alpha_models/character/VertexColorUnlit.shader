Shader "CubeGamePilot/VertexColorUnlit"
{
    // S05A-2 tint (T3, mask based):
    //   * the source voxel colour stays in COLOR and is never modified;
    //   * vertices whose source voxel colour equals the configured mask colour carry uv2.x = 1;
    //   * the fragment blends to _MaskTint only where the mask is set.
    // Geometry, normals and the palette outside the mask are untouched.
    Properties
    {
        _Tint ("Uniform Tint", Color) = (1,1,1,1)
        _MaskTint ("Mask Tint", Color) = (1,1,1,1)
        _MaskWeight ("Mask Weight", Range(0,1)) = 1
        _MaskOverlay ("Mask Overlay (debug)", Range(0,1)) = 0
    }
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

            fixed4 _Tint;
            fixed4 _MaskTint;
            float _MaskWeight;
            float _MaskOverlay;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float mask : TEXCOORD1;   // kept for documentation; the mask is read from COLOR.a
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float mask : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Tint;
                o.mask = max(v.color.a, _MaskOverlay);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed3 rgb = lerp(i.color.rgb, _MaskTint.rgb * i.color.a, saturate(i.mask) * _MaskWeight);
                return fixed4(rgb, i.color.a);
            }
            ENDCG
        }
    }
    Fallback "Unlit/Color"
}
