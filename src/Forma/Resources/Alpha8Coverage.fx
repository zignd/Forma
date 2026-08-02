float4x4 MatrixTransform;

Texture2D Texture;
sampler TextureSampler = sampler_state
{
    Texture = <Texture>;
};

struct VertexShaderInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TextureCoordinate : TEXCOORD0;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TextureCoordinate : TEXCOORD0;
};

VertexShaderOutput MainVS(VertexShaderInput input)
{
    VertexShaderOutput output;
    output.Position = mul(input.Position, MatrixTransform);
    output.Color = input.Color;
    output.TextureCoordinate = input.TextureCoordinate;
    return output;
}

float4 MainPS(VertexShaderOutput input) : COLOR0
{
    float4 sample = tex2D(TextureSampler, input.TextureCoordinate);
    float coverage = sample.a < 0.999 ? sample.a : sample.r;
    return input.Color * coverage;
}

technique Alpha8Coverage
{
    pass Pass0
    {
        VertexShader = compile vs_3_0 MainVS();
        PixelShader = compile ps_3_0 MainPS();
    }
}