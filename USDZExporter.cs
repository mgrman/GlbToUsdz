﻿
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using SkiaSharp;
using THREE;


namespace THREEToUsdz
{

    public class USDZExporter
    {
        public void Parse(Scene scene, Action onDone, Action onError, Dictionary<string, object> options)
        {
            ParseAsync(scene, options).ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    onError();
                }
                else
                {
                    onDone();
                }
            });
        }

        public async Task ParseAsync(Scene scene, Dictionary<string, object>? options = null)
        {
            options = options ?? new Dictionary<string, object>
            {
                { "ar", new Dictionary<string, object>
                    {
                        { "anchoring", new Dictionary<string, string> { { "type", "plane" } } },
                        { "planeAnchoring", new Dictionary<string, string> { { "alignment", "horizontal" } } }
                    }
                },
                { "includeAnchoringProperties", true },
                { "quickLookCompatible", false },
                { "maxTextureSize", 1024 }
            };

            var files = new Dictionary<string, byte[]>();
            var modelFileName = "model.usda";
            files[modelFileName] = null;

            var output = BuildHeader();

            output += BuildSceneStart(options);

            var materials = new Dictionary<int, Material>();
            var textures = new Dictionary<int, Texture>();

            scene.TraverseVisible((object3D) =>
            {
                if (object3D is Mesh mesh)
                {
                    var geometry = mesh.Geometry;
                    var material = mesh.Material;

                    if (material is MeshStandardMaterial standardMaterial)
                    {
                        var geometryFileName = "geometries/Geometry_" + geometry.Id + ".usda";

                        if (!files.ContainsKey(geometryFileName))
                        {
                            var meshObject = BuildMeshObject(geometry);
                            files[geometryFileName] = BuildUSDFileAsString(meshObject);
                        }

                        if (!materials.ContainsKey(material.Id))
                        {
                            materials[material.Id] = material;
                        }

                        output += BuildXform(object3D, geometry, material);
                    }
                    else
                    {
                        Console.WriteLine("THREE.USDZExporter: Unsupported material type (USDZ only supports MeshStandardMaterial)");
                        Console.WriteLine(object3D);
                    }
                }
                // else if (object3D is Camera camera)
                // {
                //     output += BuildCamera(camera);
                // }
            });

            output += BuildSceneEnd();

            output += BuildMaterials(materials, textures, (bool)options["quickLookCompatible"]);

            files[modelFileName] = StrToU8(output);
            output = null;

            foreach (var id in textures.Keys)
            {
                var texture = textures[id];

                if (texture is CompressedTexture compressedTexture)
                {
                    throw new InvalidOperationException("CompressedTexture not supported");
                    //texture = Decompress(compressedTexture);
                }

                files[$"textures/Texture_{id}.png"] = texture.Image.Encode(SKEncodedImageFormat.Png, 5).ToArray();
            }

            var offset = 0;

            foreach (var filename in files.Keys)
            {
                var file = files[filename];
                var headerSize = 34 + filename.Length;

                offset += headerSize;

                var offsetMod64 = offset & 63;

                if (offsetMod64 != 4)
                {
                    var padLength = 64 - offsetMod64;
                    var padding = new byte[padLength];

                    files[filename] = new object[] { file, new { extra = new { 12345 = padding } } };
                }

                offset = file.Length;
            }

            var result = ZipSync(files, new { level = 0 });
            return result;
        }

        private static byte[] StrToU8(string str)
        {
            // Implement the conversion logic here
            return null;
        }

        private static byte[] ZipSync(Dictionary<string, byte[]> files, object options)
        {
            // Implement the zip logic here
            return null;
        }

        public string BuildHeader()
        {
            return "#usda 1.0\n(\n\tcustomLayerData = {\n\t\tstring creator = \"Three.js USDZExporter\"\n\t}\n\tdefaultPrim = \"Root\"\n\tmetersPerUnit = 1\n\tupAxis = \"Y\"\n)\n\n";
        }

        public string BuildSceneStart(Dictionary<string, object>? options)
        {
            bool includeAnchoringProperties = options.TryGetValue("includeAnchoringProperties", out var t1) ? (bool)t1 : false;

            string anchoringType = options.TryGetValue("includeAnchoringProperties", out var t2) ? (string)t2 : "";
            string planeAnchoringAlignment = options.TryGetValue("includeAnchoringProperties", out var t3) ? (string)t3 : "";
            string alignment = includeAnchoringProperties ? $"\n\t\ttoken preliminary:anchoring:type = \"{anchoringType}\"\n\t\ttoken preliminary:planeAnchoring:alignment = \"{planeAnchoringAlignment}\"\n" : "";
            return $"def Xform \"Root\"\n{{\n\tdef Scope \"Scenes\" (\n\t\tkind = \"sceneLibrary\"\n\t)\n\t{{\n\t\tdef Xform \"Scene\" (\n\t\t\tcustomData = {{\n\t\t\t\tbool preliminary_collidesWithEnvironment = 0\n\t\t\t\tstring sceneName = \"Scene\"\n\t\t\t}}\n\t\t\tsceneName = \"Scene\"\n\t\t)\n\t\t{{{alignment}\n";
        }

        public string BuildSceneEnd()
        {
            return "\n\t\t}\n\t}\n}\n\n";
        }

        public byte[] BuildUSDFileAsString(string dataToInsert)
        {
            string output = BuildHeader() + dataToInsert;
            return strToU8(output);
        }
        public string BuildXform(Object3D obj, Geometry geometry, Material material)
        {
            string name = "Object_" + obj.Id;
            string transform = BuildMatrix(obj.MatrixWorld);

            if (obj.MatrixWorld.Determinant() < 0)
            {
                Console.WriteLine("THREE.USDZExporter: USDZ does not support negative scales", obj);
            }

            return $"def Xform \"{name}\" (\n" +
                   $"    prepend references = @./geometries/Geometry_{geometry.id}.usda@</Geometry>\n" +
                   $"    prepend apiSchemas = [\"MaterialBindingAPI\"]\n" +
                   ")\n" +
                   "{\n" +
                   $"    matrix4d xformOp:transform = {transform}\n" +
                   "    uniform token[] xformOpOrder = [\"xformOp:transform\"]\n" +
                   "    rel material:binding = </Materials/Material_{material.id}>\n" +
                   "}\n";
        }

        public static string BuildMatrix(Matrix4 matrix)
        {
            var array = matrix.Elements;

            return $"( {BuildMatrixRow(array, 0)}, {BuildMatrixRow(array, 4)}, {BuildMatrixRow(array, 8)}, {BuildMatrixRow(array, 12)} )";
        }

        public static string BuildMatrixRow(float[] array, int offset)
        {
            return $"({array[offset + 0]}, {array[offset + 1]}, {array[offset + 2]}, {array[offset + 3]})";
        }

        public static string BuildMeshObject(Geometry geometry)
        {
            string mesh = BuildMesh(geometry);
            return $@"
def ""Geometry""
{{
{mesh}
}}
";
        }

        public static string BuildMesh(BufferGeometry geometry)
        {
            string name = "Geometry";
            int count = geometry.Attributes.Count;

            return $@"
    def Mesh ""{name}""
    {{
        int[] faceVertexCounts = [{BuildMeshVertexCount(geometry)}]
        int[] faceVertexIndices = [{BuildMeshVertexIndices(geometry)}]
        normal3f[] normals = [{BuildVector3Array(attributes.Normal, count)}] (
            interpolation = ""vertex""
        )
        point3f[] points = [{BuildVector3Array(attributes.Position, count)}]
{BuildPrimvars(attributes)}
        uniform token subdivisionScheme = ""none""
    }}
";
        }

        private static string BuildMeshVertexCount(BufferGeometry geometry)
        {
            int count = geometry.Index != null ? geometry.Index.Count : geometry.Attributes.Position.Count;
            return string.Join(", ", Enumerable.Repeat(3, count / 3));
        }

        private static string BuildMeshVertexIndices(BufferGeometry geometry)
        {
            var index = geometry.Index;
            var array = new List<int>();

            if (index != null)
            {
                for (int i = 0; i < index.Count; i++)
                {
                    array.Add(index.getX(i));
                }
            }
            else
            {
                int length = geometry.Attributes.Position.Count;
                for (int i = 0; i < length; i++)
                {
                    array.Add(i);
                }
            }

            return string.Join(", ", array);
        }

        private static string BuildVector3Array(attribute, count)
        {
            if (attribute == null)
            {
                Console.WriteLine("USDZExporter: Normals missing.");
                return string.Join(", ", Enumerable.Repeat("(0, 0, 0)", count));
            }

            var array = new List<string>();
            for (int i = 0; i < attribute.Count; i++)
            {
                double x = attribute.GetX(i);
                double y = attribute.GetY(i);
                double z = attribute.GetZ(i);

                array.Add($"({x.ToString("F" + PRECISION)}, {y.ToString("F" + PRECISION)}, {z.ToString("F" + PRECISION)})");
            }

            return string.Join(", ", array);
        }

        // private static string BuildVector2Array(attribute)
        // {
        //     var array = new List<string>();
        //     for (int i = 0; i < attribute.Count; i++)
        //     {
        //         double x = attribute.GetX(i);
        //         double y = attribute.GetY(i);
        //
        //         array.Add($"({x.ToString("F" + PRECISION)}, {1 - y.ToString("F" + PRECISION)})");
        //     }
        //
        //     return string.Join(", ", array);
        // }

        //     private static string BuildPrimvars(attributes)
        //     {
        //         string primvars = "";
        //
        //         for (int i = 0; i < 4; i++)
        //         {
        //             string id = i > 0 ? i.ToString() : "";
        //             var attribute = attributes["uv" + id];
        //
        //             if (attribute != null)
        //             {
        //                 primvars += $@"
        //     texCoord2f[] primvars:st{id} = [{BuildVector2Array(attribute)}] (
        //         interpolation = ""vertex""
        //     )";
        //             }
        //         }
        //
        //         var colorAttribute = attributes.Color;
        //         if (colorAttribute != null)
        //         {
        //             int count = colorAttribute.Count;
        //             primvars += $@"
        // color3f[] primvars:displayColor = [{BuildVector3Array(colorAttribute, count)}] (
        //     interpolation = ""vertex""
        // )";
        //         }
        //
        //         return primvars;
        //     }
        //

        public static string BuildMaterials(Dictionary<int, Material> materials, Dictionary<int, Texture> textures, bool quickLookCompatible = false)
        {
            List<string> array = new List<string>();

            foreach (var uuid in materials.Keys)
            {
                Material material = materials[uuid];
                array.Add(BuildMaterial(material, textures, quickLookCompatible));
            }

            return $"def \"Materials\"\n{{\n{string.Join("", array)}\n}}\n";
        }


        private static string BuildColor(Color color)
        {
            return $"({color.R}, {color.G}, {color.B})";
        }

        private static string BuildColor4(Color color)
        {
            return $"({color.R}, {color.G}, {color.B}, 1.0)";
        }

        private static string BuildVector2(Vector2 vector)
        {
            return $"({vector.X}, {vector.Y})";
        }

        // public static string BuildCamera(Camera camera)
        // {
        //     string name = camera.Name ?? "Camera_" + camera.Id;
        //     string transform = BuildMatrix(camera.MatrixWorld);
        //
        //     if (camera.MatrixWorld.Determinant() < 0)
        //     {
        //         Console.WriteLine("THREE.USDZExporter: USDZ does not support negative scales" + camera);
        //     }
        //
        //     if (camera is OrthographicCamera orthographicCamera)
        //     {
        //         return $"def Camera \"{name}\"\n{{\nmatrix4d xformOp:transform = {transform}\nuniform token[] xformOpOrder = [\"xformOp:transform\"]\nfloat2 clippingRange = ({camera.Near.toPrecision(PRECISION)}, {camera.Far.toPrecision(PRECISION)})\nfloat horizontalAperture = {((Math.Abs(camera.left) + Math.Abs(camera.right)) * 10).toPrecision(PRECISION)}\nfloat verticalAperture = {((Math.Abs(camera.top) + Math.Abs(camera.bottom)) * 10).toPrecision(PRECISION)}\ntoken projection = \"orthographic\"\n}}\n";
        //     }
        //     else
        //     {
        //         return $"def Camera \"{name}\"\n{{\nmatrix4d xformOp:transform = {transform}\nuniform token[] xformOpOrder = [\"xformOp:transform\"]\nfloat2 clippingRange = ({camera.Near.toPrecision(PRECISION)}, {camera.Far.toPrecision(PRECISION)})\nfloat focalLength = {camera.getFocalLength().toPrecision(PRECISION)}\nfloat focusDistance = {camera.focus.toPrecision(PRECISION)}\nfloat horizontalAperture = {camera.getFilmWidth().toPrecision(PRECISION)}\ntoken projection = \"perspective\"\nfloat verticalAperture = {camera.getFilmHeight().toPrecision(PRECISION)}\n}}\n";
        //     }
        // }

        public static string BuildMaterial(Material material, Dictionary<int, Texture> textures, bool quickLookCompatible = false)
        {
            string pad = "			";
            List<string> inputs = new List<string>();
            List<string> samplers = new List<string>();


            string BuildTexture(Texture texture, string mapType, Color? color = null)
            {
                int id = texture.Id;

                textures[id] = texture;

                //string uv = texture.Channel > 0 ? "st" + texture.Channel : "st";
                string uv = "st";

                Dictionary<int, string> WRAPPINGS = new Dictionary<int, string>
            {
                { 1000, "repeat" }, // RepeatWrapping
                { 1001, "clamp" }, // ClampToEdgeWrapping
                { 1002, "mirror" } // MirroredRepeatWrapping
            };

                Vector2 repeat = texture.Repeat.Clone();
                Vector2 offset = texture.Offset.Clone();
                float rotation = texture.Rotation;

                float xRotationOffset = (float)Math.Sin(rotation);
                float yRotationOffset = (float)Math.Cos(rotation);

                offset.Y = 1 - offset.Y - repeat.Y;

                if (quickLookCompatible)
                {
                    offset.X = offset.X / repeat.X;
                    offset.Y = offset.Y / repeat.Y;

                    offset.X += xRotationOffset / repeat.X;
                    offset.Y += yRotationOffset - 1;
                }
                else
                {
                    offset.X += xRotationOffset * repeat.X;
                    offset.Y += (1 - yRotationOffset) * repeat.Y;
                }

                return $@"
def Shader ""PrimvarReader_{mapType}""
{{
    uniform token info:id = ""UsdPrimvarReader_float2""
    float2 inputs:fallback = (0.0, 0.0)
    token inputs:varname = ""{uv}""
    float2 outputs:result
}}

def Shader ""Transform2d_{mapType}""
{{
    uniform token info:id = ""UsdTransform2d""
    token inputs:in.connect = </Materials/Material_{material.Id}/PrimvarReader_{mapType}.outputs:result>
    float inputs:rotation = {(rotation * (180 / Math.PI)).ToString("F2")}
    float2 inputs:scale = {BuildVector2(repeat)}
    float2 inputs:translation = {BuildVector2(offset)}
    float2 outputs:result
}}

def Shader ""Texture_{texture.Id}_{mapType}""
{{
    uniform token info:id = ""UsdUVTexture""
    asset inputs:file = @textures/Texture_{id}.png@
    float2 inputs:st.connect = </Materials/Material_{material.Id}/Transform2d_{mapType}.outputs:result>
    {(color != null ? "float4 inputs:scale = " + BuildColor4(color.Value) : "")}
    token inputs:sourceColorSpace = ""{(texture.Image.ColorSpace.IsSrgb ? "sRGB" : "raw")}""
    token inputs:wrapS = ""{WRAPPINGS[texture.WrapS]}""
    token inputs:wrapT = ""{WRAPPINGS[texture.WrapT]}""
    float outputs:r
    float outputs:g
    float outputs:b
    float3 outputs:rgb
    {(material.Transparent || material.AlphaTest > 0.0 ? "float outputs:a" : "")}
}}";
            }


            // if (material.Side == DoubleSide)
            // {
            //     Console.WriteLine("THREE.USDZExporter: USDZ does not support double sided materials" + material);
            // }

            if (material.Map != null)
            {
                inputs.Add($"{pad}color3f inputs:diffuseColor.connect = </Materials/Material_{material.Id}/Texture_{material.Map.Id}_diffuse.outputs:rgb>");

                if (material.Transparent)
                {
                    inputs.Add($"{pad}float inputs:opacity.connect = </Materials/Material_{material.Id}/Texture_{material.Map.Id}_diffuse.outputs:a>");
                }
                else if (material.AlphaTest > 0.0)
                {
                    inputs.Add($"{pad}float inputs:opacity.connect = </Materials/Material_{material.Id}/Texture_{material.Map.Id}_diffuse.outputs:a>");
                    inputs.Add($"{pad}float inputs:opacityThreshold = {material.AlphaTest}");
                }

                samplers.Add(BuildTexture(material.Map, "diffuse", material.Color));
            }
            else
            {
                inputs.Add($"{pad}color3f inputs:diffuseColor = {BuildColor(material.Color.Value)}");
            }

            if (material.EmissiveMap != null)
            {
                inputs.Add($"{pad}color3f inputs:emissiveColor.connect = </Materials/Material_{material.Id}/Texture_{material.EmissiveMap.Id}_emissive.outputs:rgb>");
                samplers.Add(BuildTexture(material.EmissiveMap, "emissive"));
            }
            else if (material.Emissive?.GetHex() > 0)
            {
                inputs.Add($"{pad}color3f inputs:emissiveColor = {BuildColor(material.Emissive.Value)}");
            }

            if (material.NormalMap != null)
            {
                inputs.Add($"{pad}normal3f inputs:normal.connect = </Materials/Material_{material.Id}/Texture_{material.NormalMap.Id}_normal.outputs:rgb>");
                samplers.Add(BuildTexture(material.NormalMap, "normal"));
            }

            if (material.AoMap != null)
            {
                inputs.Add($"{pad}float inputs:occlusion.connect = </Materials/Material_{material.Id}/Texture_{material.AoMap.Id}_occlusion.outputs:r>");
                samplers.Add(BuildTexture(material.AoMap, "occlusion"));
            }

            if (material.RoughnessMap != null && material.Roughness == 1)
            {
                inputs.Add($"{pad}float inputs:roughness.connect = </Materials/Material_{material.Id}/Texture_{material.RoughnessMap.Id}_roughness.outputs:g>");
                samplers.Add(BuildTexture(material.RoughnessMap, "roughness"));
            }
            else
            {
                inputs.Add($"{pad}float inputs:roughness = {material.Roughness}");
            }

            if (material.MetalnessMap != null && material.Metalness == 1)
            {
                inputs.Add($"{pad}float inputs:metallic.connect = </Materials/Material_{material.Id}/Texture_{material.MetalnessMap.Id}_metallic.outputs:b>");
                samplers.Add(BuildTexture(material.MetalnessMap, "metallic"));
            }
            else
            {
                inputs.Add($"{pad}float inputs:metallic = {material.Metalness}");
            }

            if (material.AlphaMap != null)
            {
                inputs.Add($"{pad}float inputs:opacity.connect = </Materials/Material_{material.Id}/Texture_{material.AlphaMap.Id}_opacity.outputs:r>");
                inputs.Add($"{pad}float inputs:opacityThreshold = 0.0001");
                samplers.Add(BuildTexture(material.AlphaMap, "opacity"));
            }
            else
            {
                inputs.Add($"{pad}float inputs:opacity = {material.Opacity}");
            }

            // if (material.IsMeshPhysicalMaterial)
            // {
            //     inputs.Add($"{pad}float inputs:clearcoat = {material.Clearcoat}");
            //     inputs.Add($"{pad}float inputs:clearcoatRoughness = {material.ClearcoatRoughness}");
            //     inputs.Add($"{pad}float inputs:ior = {material.Ior}");
            // }

            return $@"
	def Material ""Material_{material.Id}""
	{{
		def Shader ""PreviewSurface""
		{{
			uniform token info:id = ""UsdPreviewSurface""
{string.Join("\n", inputs)}
			int inputs:useSpecularWorkflow = 0
			token outputs:surface
		}}

		token outputs:surface.connect = </Materials/Material_{material.Id}/PreviewSurface.outputs:surface>

{string.Join("\n", samplers)}

	}}
";
        }

    }
}