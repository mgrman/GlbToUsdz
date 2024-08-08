
using SharpGLTF.Schema2;
using System.Globalization;
using System.IO.Compression;
using System.Numerics;
using System.Text;

// usda samples https://github.com/ft-lab/sample_usd/blob/main/readme.md
// gltf samples https://github.com/KhronosGroup/glTF-Sample-Models/
// usdz samples https://developer.apple.com/augmented-reality/quick-look/


public static class GlbToUsdzConverter
{
    public static byte[] ConvertToUsdz(ModelRoot modelRoot)
    {
        var usda = ConvertToUsda(modelRoot);

        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry("model.usda", CompressionLevel.NoCompression);
            using var entryStream = entry.Open();

            using var writer = new StreamWriter(entryStream);
            writer.Write(usda);
        }

        return ms.ToArray();
    }

    public static string ConvertToUsda(ModelRoot model)
    {
        var oldCultureInfo = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var rootNoted = model.DefaultScene.VisualChildren;

            var sb = new StringBuilder();
            sb.AppendLine("#usda 1.0");

            void ProcessScene(IVisualNodeContainer visualNode)
            {

                if (visualNode is Node node)
                {
                    if (node.Mesh != null)
                    {
                        foreach (var primitive in node.Mesh.Primitives)
                        {
                            if (primitive.DrawPrimitiveType == PrimitiveType.TRIANGLES)
                            {
                                // TODO replace wiht scene tree, as this is only approximation and does not handle all cases, ie shear
                                Matrix4x4.Decompose(node.WorldMatrix, out var scale, out var rotation, out var translation);

                                var indices = primitive.GetIndices();

                                var vertices = primitive.GetVertices("POSITION");
                                var normals = primitive.GetVertices("NORMAL");
                                var uvs_0 = primitive.GetVertices("TEXCOORD_0");

                                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

                                sb.AppendLine($"");
                                sb.AppendLine($"def Xform \"root\"");
                                sb.AppendLine($"{{");
                                sb.AppendLine($"    double3 xformOp:rotateXYZ = {rotation.X:F3}, {rotation.Y:F3}, {rotation.Z:F3})");
                                sb.AppendLine($"    double3 xformOp:scale = {scale.ToUsdString()}");
                                sb.AppendLine($"    double3 xformOp:translate = {translation.ToUsdString()}");
                                sb.AppendLine($"    uniform token[] xformOpOrder = [\"xformOp:translate\", \"xformOp:rotateXYZ\", \"xformOp:scale\"]");
                                sb.AppendLine($"");
                                sb.AppendLine($"    def Mesh \"quad\"");
                                sb.AppendLine($"    {{");
                                sb.AppendLine($"        point3f[] points = [{string.Join(",", vertices.AsVector3Array().Select(ToUsdString))}]");
                                sb.AppendLine($"        point3f[] normals  = [{string.Join(",", normals.AsVector3Array().Select(ToUsdString))}]");
                                sb.AppendLine($"        int[] faceVertexIndices = [{string.Join(", ", indices)}]");
                                sb.AppendLine($"        int[] faceVertexCounts = [{string.Join(", ", Enumerable.Repeat(3, indices.Count / 3))}]");
                                sb.AppendLine($"    }}");
                                sb.AppendLine($"}}");
                            }
                        }
                    }
                }



                foreach (var child in visualNode.VisualChildren)
                {
                    ProcessScene(child);
                }

            }


            void ProcessMaterial(Material material)
            {
                sb.AppendLine($@"def Material ""{material.Name}""
    {{
        token inputs:frame:stPrimvarName = ""st""
        token outputs:surface.connect = </Root/{material.Name}/PBRShader.outputs:surface>

        def Shader ""PBRShader""
        {{
            uniform token info:id = ""UsdPreviewSurface""
            color3f inputs:diffuseColor = (1.0, 0.4, 0.2)
            float inputs:metallic = 0
            float inputs:roughness = 0.5
            token outputs:surface
        }}
    }}");

            }


            ProcessScene(model.DefaultScene);
            foreach (var material in model.LogicalMaterials)
            {
                ProcessMaterial(material);
            }

            return sb.ToString();
        }
        finally
        {
            CultureInfo.CurrentCulture = oldCultureInfo;
        }
    }

    private static string ToUsdString(this Vector3 p) => $"({p.X:F3}, {p.Y:F3}, {p.Z:F3})";

}