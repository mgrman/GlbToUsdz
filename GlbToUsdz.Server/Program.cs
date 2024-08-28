using GlbToUsdz.Core.Utils;
using GlbToUsdz.Server.Components;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using System.Numerics;

var builder = WebApplication.CreateBuilder(args);

// Add detection services container and device resolver service.
builder.Services.AddDetection();
builder.Services.AddHttpContextAccessor();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseDetection();
app.MapGet("/fromGlb/{file}", async (HttpContext context, [FromRoute] string file) =>
{
    var fileProvider = context.RequestServices.GetService<IFileProvider>();

    var glbPath = Path.Combine(app.Environment.WebRootPath, Path.ChangeExtension(file, ".glb"));

    var model = SharpGLTF.Schema2.ModelRoot.Load(glbPath);

    var usdz = new GlbToUsdz.Core.GlbToUsdzBuilder();

    var rootTranform = Matrix4x4.Identity;
    var modelTransform = Matrix4x4.Identity;
    switch (file)
    {
        case "F3L75R12W1H3-B27.usdz":
            rootTranform = Matrix4x4.CreateRotationX(180 * NumericsUtils.DegToRad);
            modelTransform = Matrix4x4.CreateScale(0.1f, 0.1f, 0.1f);
            break;
        case "Duck.usdz":
            rootTranform = Matrix4x4.CreateRotationY(-90 * NumericsUtils.DegToRad);
            break;
        case "GearboxAssy.usdz":
            rootTranform = Matrix4x4.CreateTranslation(1.59f, -0.15f, 0);
            break;
    }

    usdz.RootTransform = rootTranform;

    usdz.AddModel(model, modelTransform);

    context.Response.Headers.ContentType = "model/usd";

    // sadly for some reason iOS does not like it when the file is written via BodyWriter. And writing to Body stream directly is not possible due to ZipArchive not being fully Async yet.
    using var ms = new MemoryStream();
    await usdz.WriteUsdzAsync(ms);
    ms.Seek(0, SeekOrigin.Begin);

    await ms.CopyToAsync(context.Response.Body);
});

var provider = new FileExtensionContentTypeProvider();
provider.Mappings[".usdz"] = "model/vnd.usdz+zip";
provider.Mappings[".glb"] = "model/gltf-binary";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider
});
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
