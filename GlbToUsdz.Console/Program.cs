using System.Numerics;

Console.WriteLine($"Converting {args[0]} to {args[1]}");

var model = SharpGLTF.Schema2.ModelRoot.Load(args[0]);

var usdz = new GlbToUsdz.Core.GlbToUsdzBuilder();
usdz.AddModel(model, Matrix4x4.Identity);
await usdz.WriteUsdzAsync(File.OpenWrite(args[1]));

Console.WriteLine("done");