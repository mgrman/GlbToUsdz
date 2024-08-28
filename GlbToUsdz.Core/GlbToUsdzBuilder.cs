﻿
using SharpGLTF.Schema2;
using System.Globalization;
using System.IO.Compression;
using System.Numerics;
using GlbToUsdz.Core.Utils;

// usda samples https://github.com/ft-lab/sample_usd/blob/main/readme.md
// gltf samples https://github.com/KhronosGroup/glTF-Sample-Models/
// usdz samples https://developer.apple.com/augmented-reality/quick-look/

// usd spec https://openusd.org/release/spec_usdpreviewsurface.html

namespace GlbToUsdz.Core;

public class GlbToUsdzBuilder
{
    private readonly Dictionary<string, byte[]> textures = new Dictionary<string, byte[]>();

    public List<(ModelRoot model, Matrix4x4 pose)> Models { get; private set; } = new List<(ModelRoot model, Matrix4x4 pose)>();

    public Matrix4x4 RootTransform { get; set; } = Matrix4x4.Identity;

    public void AddModel(ModelRoot model, Matrix4x4 pose) => Models.Add((model, pose));

    public async ValueTask WriteUsdzAsync(Stream stream)
    {
        var oldCultureInfo = CultureInfo.CurrentCulture;
        try
        {
            textures.Clear();

            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
            {
                var entry = archive.CreateEntry("model.usda", CompressionLevel.NoCompression);
                using (var entryStream = entry.Open())
                {
                    using var writer = new StreamWriter(entryStream);
                    await ConvertToUsda(writer);
                }

                foreach (var item in textures)
                {
                    var textureEntry = archive.CreateEntry($"textures/{item.Key}", CompressionLevel.NoCompression);
                    using (var entryStream = textureEntry.Open())
                    {
                        await entryStream.WriteAsync(item.Value);
                    }
                }
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = oldCultureInfo;
        }
    }

    private async ValueTask ConvertToUsda(StreamWriter sw)
    {
        await sw.WriteLineAsync("#usda 1.0");
        await sw.WriteLineAsync($"");
        await sw.WriteLineAsync($"def Xform \"root\"");
        await sw.WriteLineAsync($"{{");

        for (int i = 0; i < Models.Count; i++)
        {
            var model = Models[i];
            var mat = Matrix4x4.Multiply(model.pose, RootTransform);
            await ProcessScene(i, model.model.DefaultScene, mat, sw);
        }

        await sw.WriteLineAsync($"}}");

        for (int i = 0; i < Models.Count; i++)
        {
            var model = Models[i];
            foreach (var material in model.model.LogicalMaterials)
            {
                await ProcessMaterial(i, material, sw);
            }
        }
    }

    async ValueTask ProcessScene(int modelIndex, IVisualNodeContainer visualNode, Matrix4x4 modelPose, StreamWriter sw)
    {
        if (visualNode is Node node)
        {
            if (node.Mesh != null)
            {
                foreach (var primitive in node.Mesh.Primitives)
                {
                    if (primitive.DrawPrimitiveType == PrimitiveType.TRIANGLES)
                    {
                        var worldMat = Matrix4x4.Multiply(node.WorldMatrix, modelPose);

                        var indices = primitive.GetIndices();
                        var vertices = primitive.VertexAccessors.ContainsKey("POSITION") ? primitive.GetVertices("POSITION") : null;
                        var normals = primitive.VertexAccessors.ContainsKey("NORMAL") ? primitive.GetVertices("NORMAL") : null;
                        var uvs_0 = primitive.VertexAccessors.ContainsKey("TEXCOORD_0") ? primitive.GetVertices("TEXCOORD_0") : null;

                        await sw.WriteLineAsync($"    def Mesh \"mesh_{modelIndex}_{node.LogicalIndex}\"");
                        await sw.WriteLineAsync($"    {{");

                        await sw.WriteLineAsync($"        matrix4d xformOp:transform = {worldMat.ToUsdString()}");
                        await sw.WriteLineAsync($"        uniform token[] xformOpOrder = [ \"xformOp:transform\"]");

                        await sw.WriteLineAsync($"");
                        if (vertices != null)
                        {
                            await sw.WriteLineAsync($"        point3f[] points = {vertices.AsVector3Array().ToUsdString()}");
                        }
                        if (normals != null)
                        {
                            await sw.WriteLineAsync($"        normal3f[] normals  = {normals.AsVector3Array().ToUsdString()} (");
                            await sw.WriteLineAsync($"            interpolation = \"vertex\"");
                            await sw.WriteLineAsync($"        )");
                        }
                        await sw.WriteLineAsync($"        int[] faceVertexIndices = {indices.ToUsdString()}");
                        await sw.WriteLineAsync($"        int[] faceVertexCounts = {Enumerable.Repeat((uint)3, indices.Count / 3).ToUsdString()}");
                        if (uvs_0 != null)
                        {
                            await sw.WriteLineAsync($"        texCoord2f[] primvars:st = {uvs_0.AsVector2Array().Select(o => new Vector2(o.X, 1 - o.Y)).ToUsdString()} (");
                            await sw.WriteLineAsync($"            interpolation = \"vertex\"");
                            await sw.WriteLineAsync($"        )");
                        }
                        if (primitive.Material != null)
                        {
                            await sw.WriteLineAsync($"        rel material:binding = </{GetMatName(modelIndex, primitive.Material)}>");
                        }
                        await sw.WriteLineAsync($"    }}");
                    }
                }
            }
        }

        foreach (var child in visualNode.VisualChildren)
        {
            await ProcessScene(modelIndex, child, modelPose, sw);
        }
    }

    async ValueTask ProcessMaterial(int modelIndex, Material material, StreamWriter sw)
    {
        var diffuseChannel = material.Channels.FirstOrDefault(o => o.Key == "BaseColor");
        var texture = diffuseChannel.Texture;
        var matName = GetMatName(modelIndex, material);

        await sw.WriteLineAsync($"def Material \"{matName}\"");
        await sw.WriteLineAsync($"{{");
        await sw.WriteLineAsync($"    token inputs:frame:stPrimvarName = \"st\"");
        await sw.WriteLineAsync($"    token outputs:surface.connect = </{matName}/PBRShader.outputs:surface>");
        await sw.WriteLineAsync($"");
        await sw.WriteLineAsync($"    def Shader \"PBRShader\"");
        await sw.WriteLineAsync($"    {{");
        await sw.WriteLineAsync($"        uniform token info:id = \"UsdPreviewSurface\"");
        await sw.WriteLineAsync($"        color3f inputs:diffuseColor = {diffuseChannel.Color.ToXYZUsdString()}");
        if (texture != null)
        {
            await sw.WriteLineAsync($"        color3f inputs:diffuseColor.connect = </{matName}/diffuseTexture.outputs:rgb>");
        }
        await sw.WriteLineAsync($"        float inputs:metallic = 0");
        await sw.WriteLineAsync($"        float inputs:roughness = 1");
        await sw.WriteLineAsync($"        token outputs:surface");
        await sw.WriteLineAsync($"    }}");
        if (texture != null)
        {
            var content = texture.PrimaryImage.Content;
            var textureName = $"{modelIndex}_{diffuseChannel.Texture.LogicalIndex}.{content.FileExtension}";
            textures[textureName] = content.Content.ToArray();
            await sw.WriteLineAsync($"");
            await sw.WriteLineAsync($"    def Shader \"stReader\"");
            await sw.WriteLineAsync($"    {{");
            await sw.WriteLineAsync($"        uniform token info:id = \"UsdPrimvarReader_float2\"");
            await sw.WriteLineAsync($"        token inputs:varname.connect = </{matName}.inputs:frame:stPrimvarName>");
            await sw.WriteLineAsync($"        float2 outputs:result");
            await sw.WriteLineAsync($"    }}");
            await sw.WriteLineAsync($"");
            await sw.WriteLineAsync($"    def Shader \"diffuseTexture\"");
            await sw.WriteLineAsync($"    {{");
            await sw.WriteLineAsync($"        uniform token info:id = \"UsdUVTexture\"");
            await sw.WriteLineAsync($"        asset inputs:file = @textures/{textureName}@");
            await sw.WriteLineAsync($"        token inputs:sourceColorSpace = \"raw\"");
            await sw.WriteLineAsync($"        token inputs:st.connect = </{matName}/stReader.outputs:result>");
            await sw.WriteLineAsync($"        token inputs:wrapS = \"repeat\"");
            await sw.WriteLineAsync($"        token inputs:wrapT = \"repeat\"");
            await sw.WriteLineAsync($"        float3 outputs:rgb");
            await sw.WriteLineAsync($"    }}");
            await sw.WriteLineAsync($"");
        }
        await sw.WriteLineAsync($"}}");
    }

    private static string GetMatName(int modelIndex, Material material)
    {
        return $"mat_{modelIndex}_{material.LogicalIndex}";
    }
}