# GlbToUsdz
 
Tiny proof of concept of converting GLB files into USDZ files.

Only very limited feature set is supported:
- Basic Scene approximation (converts scene graph to list of models using world matrix)
- Triangle meshes
  - Normals
- Diffuse Channel of Materials
  - Textures
- composite multiple GLTF models into one usdz file
- only usda files are created which are then wrapped into usdz archive


The usda files are manually created without any serialization library, as mentioned this is enough for this proof of concept.

The target is to be able to generate on the fly usdz files for a given gltf file. e.g. for use with QuickLook

See Console and Server projects in the repo for examples of usage.


[Demo](https://glbtousdz.azurewebsites.net/).