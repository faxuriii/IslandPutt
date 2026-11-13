// Cielo estilo low poly: degradado por bandas + sol facetado.
Shader "IslandPutt/CieloLowPoly"
{
    Properties
    {
        _ColorCenit ("Color cénit (arriba)", Color) = (0.20, 0.50, 0.88, 1)
        _ColorHorizonte ("Color horizonte", Color) = (0.68, 0.87, 1.00, 1)
        _ColorAbajo ("Color bajo el horizonte", Color) = (0.86, 0.93, 1.00, 1)
        _Bandas ("Bandas (0 = degradado suave)", Range(0, 24)) = 9
        _ColorSol ("Color sol", Color) = (1.00, 0.95, 0.75, 1)
        _TamSol ("Tamaño sol", Range(0, 0.3)) = 0.07
        _BrilloSol ("Halo del sol", Range(0, 1)) = 0.35
        _DirSol ("Dirección sol", Vector) = (0.4, 0.45, 0.3, 0)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _ColorCenit, _ColorHorizonte, _ColorAbajo, _ColorSol;
            float _Bandas, _TamSol, _BrilloSol;
            float4 _DirSol;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float Banda(float t, float n)
            {
                return n > 0.5 ? floor(t * n + 0.5) / n : t;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = d.y;
                float3 col;

                if (h >= 0)
                {
                    float t = Banda(pow(saturate(h), 0.55), _Bandas);
                    col = lerp(_ColorHorizonte.rgb, _ColorCenit.rgb, t);
                }
                else
                {
                    float t = Banda(saturate(-h * 2.0), max(_Bandas * 0.5, 0));
                    col = lerp(_ColorHorizonte.rgb, _ColorAbajo.rgb, t);
                }

                // Sol: disco con borde nítido + halo en anillos (aspecto facetado)
                float3 s = normalize(_DirSol.xyz);
                float sd = dot(d, s);
                float cosSol = cos(_TamSol);
                float disco = smoothstep(cosSol - 0.0015, cosSol, sd);
                float halo = Banda(pow(saturate(sd), 18.0), 4) * _BrilloSol;
                col = lerp(col, _ColorSol.rgb, saturate(halo));
                col = lerp(col, _ColorSol.rgb * 1.15, disco);

                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
