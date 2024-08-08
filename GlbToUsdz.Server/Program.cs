using GlbToUsdz.Server.Components;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.MapGet("/usdz/{file}", async (HttpContext context, [FromRoute]string file) =>
{
    var fileProvider = context.RequestServices.GetService<IFileProvider>();

    var glbPath = Path.Combine(app.Environment.WebRootPath, file);

    var model = SharpGLTF.Schema2.ModelRoot.Load(glbPath);

    var usdz=GlbToUsdzConverter.ConvertToUsdz(model);

    context.Response.Headers.ContentDisposition= $"attachment; filename=\"{Path.GetFileNameWithoutExtension(file)}.usdz\"";
    context.Response.Headers.ContentType = "model/vnd.usdz+zip";
    await context.Response.Body.WriteAsync(usdz);

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
