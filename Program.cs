// See https://aka.ms/new-console-template for more information
using THREE;
using THREEToUsdz;
using static System.Formats.Asn1.AsnWriter;

Console.WriteLine("Hello, World!");

var scene = new Scene();



var planeGeometry = new PlaneBufferGeometry(60, 40, 1, 1);
MeshLambertMaterial planeMaterial = new MeshLambertMaterial() { Color = Color.Hex(0xffffff) };

var plane = new Mesh(planeGeometry, planeMaterial);
plane.ReceiveShadow = true;

plane.Rotation.X = (float)(-0.5 * System.Math.PI);
plane.Position.X = 0;
plane.Position.Y = 0;
plane.Position.Z = 0;

scene.Add(plane);



var vertices = new List<Vector3>(){
                  new Vector3(1, 3, 1),
                  new Vector3(1, 3, -1),
                  new Vector3(1, -1, 1),
                  new Vector3(1, -1, -1),
                  new Vector3(-1, 3, -1),
                  new Vector3(-1, 3, 1),
                  new Vector3(-1, -1, -1),
                  new Vector3(-1, -1, 1)
             };

var faces = new List<Face3>() {
                new Face3(0, 2, 1),
                new Face3(2, 3, 1),
                new Face3(4, 6, 5),
                new Face3(6, 7, 5),
                new Face3(4, 5, 1),
                new Face3(5, 0, 1),
                new Face3(7, 6, 2),
                new Face3(6, 3, 2),
                new Face3(5, 7, 0),
                new Face3(7, 2, 0),
                new Face3(1, 3, 4),
                new Face3(3, 6, 4),

            };

var controlPoint = new List<Vector3>(vertices);

var geom = new Geometry();
geom.Vertices = vertices;
geom.Faces = faces;

geom.ComputeFaceNormals();


var materials = new List<Material>(){
                    new MeshBasicMaterial() { Color = Color.ColorName(ColorKeywords.black),Wireframe=true},
                    new MeshLambertMaterial() { Opacity=0.6f,Color = new Color().SetHex(0x44ff44),Transparent=true }

                };

var mesh = SceneUtils.CreateMultiMaterialObject(geom, materials);

mesh.Traverse(o =>
{
    o.CastShadow = true;
});
scene.Add(mesh);




var bytes = await USDZExporter.ParseAsync(scene);


File.WriteAllBytes(@"C:\Users\marti\OneDrive\Desktop\test.usdz", bytes);

Console.WriteLine(bytes.Length);