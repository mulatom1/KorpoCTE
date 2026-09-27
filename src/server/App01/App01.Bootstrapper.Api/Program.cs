using System.Reflection;

using App01.Bootstrapper.Api;
using App01.Modules.Flashcards;
using App01.Modules.Lotto;
using App01.Modules.Portal;
using App01.Shared.Abstractions;
using App01.Shared.Application;
using App01.Shared.Application.Middlewares;
using App01.Shared.Infrastructure;

using FluentValidation;

using Microsoft.AspNetCore.StaticFiles;

using Serilog;


var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((context, configuration) => configuration.ReadFrom.Configuration(context.Configuration));


var isTestEnvironment = builder.Environment.IsEnvironment("Test");


builder.Services.AddSharedAbstractionsServices();
builder.Services.AddSharedApplicationServices();
builder.Services.AddSharedInfrastructureServices(builder.Configuration, builder.Environment);

builder.Services.AddModulePortalServices();
builder.Services.AddModuleLottoServices();
builder.Services.AddModuleFlashcardsServices();

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddServerSwagger();

builder.Services.AddServerAuthentication(builder.Configuration);

builder.Services.AddAuthorization();

builder.Services.AddServerCors();


// Only add hosted services in non-test environments
if (!isTestEnvironment)
{
    builder.Services.AddModulePortalWorkers();
    builder.Services.AddModuleLottoWorkers();
}


var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();


if (builder.Configuration.GetValue("Swagger:Enabled", false))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (builder.Configuration.GetValue("Env", "DEV") == "PROD")
{
    app.UseHttpsRedirection();
}

//app.UseStaticFiles();

var contentTypeProvider = new FileExtensionContentTypeProvider();

contentTypeProvider.Mappings[".m3u8"] = "application/vnd.apple.mpegurl";
contentTypeProvider.Mappings[".ts"] = "video/mp2t";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = contentTypeProvider
});


app.UseSerilogRequestLogging(options =>
{
    options.GetLevel = (httpContext, elapsed, ex) => Serilog.Events.LogEventLevel.Debug;
});

app.UseRouting();

app.UseCors();

app.UseAuthentication();

app.UseAuthorization();


app.UseModulePortalEndpoints();
app.UseModuleLottoEndpoints();
app.UseModuleFlashcardsEndpoints();

// SPA fallback – zwraca index.html dla wszystkich tras nieznanych serwerowi
// (obsługa client-side routingu React)
app.MapFallbackToFile("index.html");

try
{
    Log.Debug("Application starting!");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application start-up failed");
}
finally
{
    await Log.CloseAndFlushAsync();
}

public partial class Program { }