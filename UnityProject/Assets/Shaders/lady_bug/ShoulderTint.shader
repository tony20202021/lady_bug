Shader "Custom/ShoulderTint"
{
    Properties
    {
        _MainTex ("Shoulder Tile", 2D) = "white" {}
        [Toggle] _RoadEdgeAtHighU ("Road Edge At High U", Float) = 1
        _EdgeInset ("Road-edge inset (UV width)", Range(0, 0.25)) = 0
        _EdgeWaveAmp ("Road-edge wave amplitude (UV)", Range(0, 0.2)) = 0.04
        _EdgeWaveFreq ("Road-edge waves per strip length", Float) = 13
        _EdgeAmpVar ("Per-wave height variation", Range(0, 1)) = 1
        _EdgeSoftness ("Road-edge alpha feather (UV)", Range(0, 0.08)) = 0.032
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 200

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _RoadEdgeAtHighU;
            float _EdgeInset;
            float _EdgeWaveAmp;
            float _EdgeWaveFreq;
            float _EdgeAmpVar;
            float _EdgeSoftness;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float Hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            float WaveAmpScale(float2 uv, float seed)
            {
                float cell = uv.y * _EdgeWaveFreq;
                float i = floor(cell);
                float f = frac(cell);
                f = f * f * (3.0 - 2.0 * f);
                float r0 = Hash11(i * 12.9898 + 78.233 + seed);
                float r1 = Hash11((i + 1.0) * 12.9898 + 78.233 + seed);
                return lerp(r0, r1, f);
            }

            float StripEdgeMask(float2 uv, float edgeAtHighU, float seed)
            {
                float edgeDist = edgeAtHighU > 0.5 ? (1.0 - uv.x) : uv.x;
                float cell = uv.y * _EdgeWaveFreq;
                float waveIndex = floor(cell);
                float phase = (Hash11(waveIndex * 41.17 + 9.3 + seed) - 0.5) * 0.45;
                float t = cell + phase;
                float s = sin(t * 6.2831853);
                float wave = sign(s) * pow(abs(s), 0.72);
                float rnd = WaveAmpScale(uv, seed);
                float rnd2 = Hash11(waveIndex * 23.45 + 5.1 + seed);
                float slowRnd = Hash11(floor(uv.y * 3.2) * 31.1 + 2.7 + seed);
                float ampScale = lerp(0.12, 2.15, rnd);
                ampScale *= lerp(0.55, 1.55, slowRnd);
                float insetJitter = lerp(-0.012, 0.02, rnd2);
                float ampMix = lerp(1.0, ampScale, _EdgeAmpVar);
                float edge = _EdgeInset + insetJitter * _EdgeAmpVar + _EdgeWaveAmp * ampMix * wave;
                return edgeDist - edge;
            }

            float CombinedEdgeMask(float2 uv)
            {
                float road = StripEdgeMask(uv, _RoadEdgeAtHighU, 0.0);
                float grass = StripEdgeMask(uv, 1.0 - _RoadEdgeAtHighU, 31.7);
                return min(road, grass);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                float edge = CombinedEdgeMask(i.uv);
                float alpha = smoothstep(0.0, _EdgeSoftness, edge);
                col.a = alpha;
                return col;
            }
            ENDCG
        }
    }

    FallBack "Diffuse"
}
