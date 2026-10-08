Shader "WorldInteraction/VertexColor"
{
    SubShader {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Lambert
        struct Input { float4 color:COLOR; };
        void surf(Input IN,inout SurfaceOutput o){o.Albedo=IN.color.rgb;o.Alpha=1;}
        ENDCG
    }
    Fallback "Diffuse"
}
