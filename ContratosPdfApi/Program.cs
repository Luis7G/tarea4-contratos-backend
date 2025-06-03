using ContratosPdfApi.Services;
using DinkToPdf;
using DinkToPdf.Contracts;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configurar CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:4200",
                "https://contrato-bienes-frontend.netlify.app",
                "https://*.netlify.app",
                "https://*.render.com"
              )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Configurar DinkToPdf con mejor manejo de errores
try
{
    var isProduction = builder.Environment.IsProduction();

    Console.WriteLine($"Configurando DinkToPdf - Ambiente: {builder.Environment.EnvironmentName}");
    Console.WriteLine($"Es producción: {isProduction}");
    Console.WriteLine($"OS Platform: {Environment.OSVersion.Platform}");

    if (isProduction || Environment.OSVersion.Platform == PlatformID.Unix)
    {
        Console.WriteLine("Configurando DinkToPdf para Linux/Producción");

        // Verificar variables de entorno
        var ldLibraryPath = Environment.GetEnvironmentVariable("LD_LIBRARY_PATH");
        Console.WriteLine($"LD_LIBRARY_PATH: {ldLibraryPath}");

        // Verificar si wkhtmltopdf está disponible
        try
        {
            var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "which",
                Arguments = "wkhtmltopdf",
                RedirectStandardOutput = true,
                UseShellExecute = false
            });

            if (process != null)
            {
                process.WaitForExit();
                var wkhtmltopdfPath = process.StandardOutput.ReadToEnd().Trim();
                Console.WriteLine($"wkhtmltopdf path: {wkhtmltopdfPath}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error verificando wkhtmltopdf path: {ex.Message}");
        }

        // Buscar librerías wkhtmltox
        var possiblePaths = new[]
        {
            "/usr/local/lib/libwkhtmltox.so",
            "/usr/lib/libwkhtmltox.so",
            "/lib/libwkhtmltox.so",
            "/usr/local/lib/libwkhtmltox.so.0",
            "/usr/lib/x86_64-linux-gnu/libwkhtmltox.so"
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                Console.WriteLine($"Librería encontrada en: {path}");
            }
        }

        // Configurar DinkToPdf
        builder.Services.AddSingleton(typeof(IConverter), new SynchronizedConverter(new PdfTools()));
        Console.WriteLine("DinkToPdf configurado para Linux");
    }
    else
    {
        // Configuración para Windows (desarrollo)
        var context = new CustomAssemblyLoadContext();
        var basePath = Directory.GetCurrentDirectory();
        var libraryPath = Path.Combine(basePath, "libwkhtmltox.dll");

        Console.WriteLine($"Configurando DinkToPdf para Windows - Path: {libraryPath}");
        Console.WriteLine($"¿Archivo existe?: {File.Exists(libraryPath)}");

        if (File.Exists(libraryPath))
        {
            context.LoadUnmanagedLibrary(libraryPath);
        }

        builder.Services.AddSingleton(typeof(IConverter), new SynchronizedConverter(new PdfTools()));
        Console.WriteLine("DinkToPdf configurado para Windows");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR configurando DinkToPdf: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");

    // Continuar sin DinkToPdf por ahora
    Console.WriteLine("Continuando sin DinkToPdf...");
}

// Registrar servicios
builder.Services.AddScoped<IPdfService, PdfService>();
builder.Services.AddHostedService<TempFileCleanupService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Contratos PDF API V1");
        c.RoutePrefix = "swagger";
    });
}

app.UseStaticFiles();
app.UseCors("AllowFrontend");
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTime.UtcNow,
    environment = app.Environment.EnvironmentName
}));

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
Console.WriteLine($"Servidor iniciado en puerto {port}");

app.Run($"http://0.0.0.0:{port}");