
using SharpGLTF.Schema2;
using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Text;

// usda samples https://github.com/ft-lab/sample_usd/blob/main/readme.md
// gltf samples https://github.com/KhronosGroup/glTF-Sample-Models/
// usdz samples https://developer.apple.com/augmented-reality/quick-look/

// usd spec https://openusd.org/release/spec_usdpreviewsurface.html
public static class GlbToUsdzConverter
{
    public static byte[] ConvertToUsdz(ModelRoot modelRoot)
    {
        var usda = ConvertToUsda(modelRoot);

        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            {
                var entry = archive.CreateEntry("model.usda", CompressionLevel.NoCompression);
                using (var entryStream = entry.Open())
                {
                    using var writer = new StreamWriter(entryStream);
                    writer.Write(usda.usda);
                }
            }

            foreach (var item in usda.textures)
            {
                var textureEntry = archive.CreateEntry($"textures/{item.Key}", CompressionLevel.NoCompression);
                using (var entryStream = textureEntry.Open())
                {
                    entryStream.Write(item.Value);
                }
            }
        }

        return ms.ToArray();
    }

    public static (string usda, Dictionary<string,byte[]> textures) ConvertToUsda(ModelRoot model)
    {
        var oldCultureInfo = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            var textures=new Dictionary<string, byte[]>();

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


                                var vertices = primitive.VertexAccessors.ContainsKey("POSITION")? primitive.GetVertices("POSITION"):null;
                                var normals = primitive.VertexAccessors.ContainsKey("NORMAL") ? primitive.GetVertices("NORMAL") : null;
                                var uvs_0 = primitive.VertexAccessors.ContainsKey("TEXCOORD_0") ? primitive.GetVertices("TEXCOORD_0") : null;

                                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

                                sb.AppendLine($"    double3 xformOp:rotateXYZ = {rotation.ToXYZUsdString()}");
                                sb.AppendLine($"    double3 xformOp:scale = {scale.ToUsdString()}");
                                sb.AppendLine($"    double3 xformOp:translate = {translation.ToUsdString()}");
                                sb.AppendLine($"    uniform token[] xformOpOrder = [\"xformOp:translate\", \"xformOp:rotateXYZ\", \"xformOp:scale\"]");
                                sb.AppendLine($"");
                                sb.AppendLine($"    def Mesh \"mesh_{node.LogicalIndex}\"");
                                sb.AppendLine($"    {{");
                                sb.AppendLine($"        point3f[] points = {vertices.AsVector3Array().ToUsdString()}");
                                if (normals != null)
                                {
                                    sb.AppendLine($"        normal3f[] normals  = {normals.AsVector3Array().ToUsdString()} (");
                                    sb.AppendLine($"            interpolation = \"vertex\"");
                                    sb.AppendLine($"        )");
                                }
                                sb.AppendLine($"        int[] faceVertexIndices = {indices.ToUsdString()}");
                                sb.AppendLine($"        int[] faceVertexCounts = {Enumerable.Repeat((uint)3, indices.Count / 3).ToUsdString()}");
                                if (uvs_0 != null)
                                {
                                    sb.AppendLine($"        texCoord2f[] primvars:st = { uvs_0.AsVector2Array().Select(o => new Vector2(o.X, 1 - o.Y)).ToUsdString()} (");
                                    sb.AppendLine($"            interpolation = \"vertex\"");
                                    sb.AppendLine($"        )");
                                }
                                if (primitive.Material != null)
                                {
                                    sb.AppendLine($"        rel material:binding = </mat_{primitive.Material.LogicalIndex}>");
                                }
                                sb.AppendLine($"    }}");
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
                var diffuseChannel = material.Channels.FirstOrDefault(o => o.Key == "BaseColor");

                var texture = diffuseChannel.Texture;

                var matName = $"mat_{material.LogicalIndex}";

                sb.AppendLine($"def Material \"{matName}\"");
                sb.AppendLine($"{{");
                sb.AppendLine($"    token inputs:frame:stPrimvarName = \"st\"");
                sb.AppendLine($"    token outputs:surface.connect = </{matName}/PBRShader.outputs:surface>");
                sb.AppendLine($"");
                sb.AppendLine($"    def Shader \"PBRShader\"");
                sb.AppendLine($"    {{");
                sb.AppendLine($"        uniform token info:id = \"UsdPreviewSurface\"");
                sb.AppendLine($"        color3f inputs:diffuseColor = {diffuseChannel.Color.ToXYZUsdString()}");
                if (texture != null)
                {
                    sb.AppendLine($"        color3f inputs:diffuseColor.connect = </{matName}/diffuseTexture.outputs:rgb>");
                }
                sb.AppendLine($"        float inputs:metallic = 0");
                sb.AppendLine($"        float inputs:roughness = 1");
                sb.AppendLine($"        token outputs:surface");
                sb.AppendLine($"    }}");
                if (texture != null)
                {
                    var content = texture.PrimaryImage.Content;
                    var textureName = $"{diffuseChannel.Texture.LogicalIndex}.{content.FileExtension}";
                    textures[textureName] = content.Content.ToArray();
                    sb.AppendLine($"");
                    sb.AppendLine($"    def Shader \"stReader\"");
                    sb.AppendLine($"    {{");
                    sb.AppendLine($"        uniform token info:id = \"UsdPrimvarReader_float2\"");
                    sb.AppendLine($"        token inputs:varname.connect = </{matName}.inputs:frame:stPrimvarName>");
                    sb.AppendLine($"        float2 outputs:result");
                    sb.AppendLine($"    }}");
                    sb.AppendLine($"");
                    sb.AppendLine($"    def Shader \"diffuseTexture\"");
                    sb.AppendLine($"    {{");
                    sb.AppendLine($"        uniform token info:id = \"UsdUVTexture\"");
                    sb.AppendLine($"        asset inputs:file = @textures/{textureName}@");
                    sb.AppendLine($"        token inputs:sourceColorSpace = \"raw\"");
                    sb.AppendLine($"        token inputs:st.connect = </{matName}/stReader.outputs:result>");
                    sb.AppendLine($"        token inputs:wrapS = \"repeat\"");
                    sb.AppendLine($"        token inputs:wrapT = \"repeat\"");
                    sb.AppendLine($"        float3 outputs:rgb");
                    sb.AppendLine($"    }}");
                    sb.AppendLine($"");
                }
                sb.AppendLine($"}}");

            }


            sb.AppendLine($"");
            sb.AppendLine($"def Xform \"root\"");
            sb.AppendLine($"{{");
            ProcessScene(model.DefaultScene);
            sb.AppendLine($"}}");

            foreach (var material in model.LogicalMaterials)
            {
                ProcessMaterial(material);
            }

            return (sb.ToString(), textures);
        }
        finally
        {
            CultureInfo.CurrentCulture = oldCultureInfo;
        }
    }
    private static string ToUsdString(this IEnumerable<Vector3> vertices) => $"[{string.Join(",", vertices.Select(ToUsdString))}]";
    private static string ToUsdString(this IEnumerable<Vector2> vertices) => $"[{string.Join(",", vertices.Select(ToUsdString))}]";
    private static string ToUsdString(this IEnumerable<uint> indices) => $"[{string.Join(", ", indices)}]";

    private static string ToUsdString(this Vector3 p) => $"({p.X:F7}, {p.Y:F7}, {p.Z:F7})";
    private static string ToUsdString(this Vector2 p) => $"({p.X:F7}, {p.Y:F7})";
    private static string ToXYZUsdString(this Vector4 p) => $"({p.X:F7}, {p.Y:F7}, {p.Z:F7})";
    private static string ToXYZUsdString(this Quaternion p) => p.ToEulerAngles().ToUsdString();


    private static Vector3 ToEulerAngles(this Quaternion q)
    {
        Vector3 angles = new();

        // roll / x
        double sinr_cosp = 2 * (q.W * q.X + q.Y * q.Z);
        double cosr_cosp = 1 - 2 * (q.X * q.X + q.Y * q.Y);
        angles.X = (float)Math.Atan2(sinr_cosp, cosr_cosp);

        // pitch / y
        double sinp = 2 * (q.W * q.Y - q.Z * q.X);
        if (Math.Abs(sinp) >= 1)
        {
            angles.Y = (float)Math.CopySign(Math.PI / 2, sinp);
        }
        else
        {
            angles.Y = (float)Math.Asin(sinp);
        }

        // yaw / z
        double siny_cosp = 2 * (q.W * q.Z + q.X * q.Y);
        double cosy_cosp = 1 - 2 * (q.Y * q.Y + q.Z * q.Z);
        angles.Z = (float)Math.Atan2(siny_cosp, cosy_cosp);

        return angles;
    }

}