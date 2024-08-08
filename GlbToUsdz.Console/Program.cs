Console.WriteLine($"Converting {args[0]} to {args[1]}");

var model = SharpGLTF.Schema2.ModelRoot.Load(args[0]);
var usdz = GlbToUsdzConverter.ConvertToUsdz(model);
File.WriteAllBytes(args[1], usdz);

Console.WriteLine("done");