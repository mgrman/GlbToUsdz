
using SharpGLTF.Schema2;
using System.Globalization;
using System.Numerics;
using System.Text;

// based on https://github.com/ft-lab/sample_usd/blob/main/readme.md

var model = SharpGLTF.Schema2.ModelRoot.Load("Duck.glb");

var rootNoted = model.DefaultScene.VisualChildren;

var sb = new StringBuilder();
sb.AppendLine("#usda 1.0");


void Process(IVisualNodeContainer visualNode)
{

    if(visualNode is Node node)
    {
        if (node.Mesh != null)
        {
            foreach(var primitive in node.Mesh.Primitives)
            {
                if(primitive.DrawPrimitiveType == PrimitiveType.TRIANGLES)
                {
                    Matrix4x4.Decompose(node.WorldMatrix, out var scale, out var rotation, out var translation);

                    var indices=primitive.GetIndices();

                    var vertices = primitive.GetVertices("POSITION");
                    var normal = primitive.GetVertices("NORMAL");
                    var uv_0 = primitive.GetVertices("TEXCOORD_0");

                    CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

                    sb.AppendLine($"");
                    sb.AppendLine($"def Xform \"root\"");
                    sb.AppendLine($"{{");
                    sb.AppendLine($"    double3 xformOp:translate = ({translation.X:F3}, {translation.Y:F3}, {translation.Z:F3})");
                    sb.AppendLine($"    uniform token[] xformOpOrder = [\"xformOp: translate\"]");
                    sb.AppendLine($"");
                    sb.AppendLine($"    def Mesh \"quad\"");
                    sb.AppendLine($"    {{");
                    sb.AppendLine($"        point3f[] points = [{string.Join(",", vertices.AsVector3Array().Select(p=>$"({p.X:F3}, {p.Y:F3}, {p.Z:F3})"))}]");//(0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0)]");
                    sb.AppendLine($"        int[] faceVertexIndices = [{string.Join(", ", indices)}]");
                    sb.AppendLine($"        int[] faceVertexCounts = [{string.Join(", ",Enumerable.Repeat(3, indices.Count/3))}]");
                    sb.AppendLine($"    }}");
                    sb.AppendLine($"}}");
                }
            }
            

        }
    }



    foreach (var child in visualNode.VisualChildren)
    {
        Process(child);
    }

}


Process(model.DefaultScene);

File.WriteAllText(@"C:\Users\marti\OneDrive\Desktop\New Text Document.txt", sb.ToString());

Console.WriteLine("done");