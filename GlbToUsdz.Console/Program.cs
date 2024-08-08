Console.WriteLine($"Converting {args[0]} to {args[1]}");

var model = SharpGLTF.Schema2.ModelRoot.Load(args[0]);
var usda = GlbToUsdzConverter.ConvertToUsda(model);
File.WriteAllText(args[1], usda);

Console.WriteLine("done");