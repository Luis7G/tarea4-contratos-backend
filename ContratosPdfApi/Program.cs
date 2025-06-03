using ContratosPdfApi.Services;
using DinkToPdf;
using DinkToPdf.Contracts;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configurar CORS - ACTUALIZADO PARA PRODUCCIÓN
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:4200", // Para desarrollo local
                "https://contrato-bienes-frontend.netlify.app", // CORREGIDO: sin barra diagonal al final
                "https://*.netlify.app", // Para subdominios de Netlify
                "https://*.render.com" // Para otros servicios en Render
              )
              .AllowAnyHeader()
              .AllowAnyMethod()
                            .AllowCredentials(); // Agregar esto para mejorar compatibilidad

    });
});

// Configurar DinkToPdf - MEJORADO PARA LINUX/PRODUCCIÓN
try
{
    var isProduction = builder.Environment.IsProduction();

    if (isProduction || Environment.OSVersion.Platform == PlatformID.Unix)
    {
        // En producción (Linux), usar wkhtmltopdf del sistema
        Console.WriteLine("Configurando DinkToPdf para Linux/Producción");

        // Verificar que wkhtmltopdf esté disponible
        try
        {
            var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "wkhtmltopdf",
                Arguments = "--version",
                RedirectStandardOutput = true,
                UseShellExecute = false
            });

            if (process != null)
            {
                process.WaitForExit();
                var output = process.StandardOutput.ReadToEnd();
                Console.WriteLine($"wkhtmltopdf encontrado: {output}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error verificando wkhtmltopdf: {ex.Message}");
        }

        // Para Linux, DinkToPdf usa wkhtmltopdf del sistema automáticamente
        builder.Services.AddSingleton(typeof(IConverter), new SynchronizedConverter(new PdfTools()));
    }
    else
    {
        // En desarrollo (Windows), usar la DLL local
        var context = new CustomAssemblyLoadContext();
        var basePath = Directory.GetCurrentDirectory();
        var libraryPath = Path.Combine(basePath, "libwkhtmltox.dll");

        Console.WriteLine($"Configurando DinkToPdf para Windows");
        Console.WriteLine($"Cargando DLL desde: {libraryPath}");
        Console.WriteLine($"¿Archivo existe?: {File.Exists(libraryPath)}");

        if (File.Exists(libraryPath))
        {
            context.LoadUnmanagedLibrary(libraryPath);
            builder.Services.AddSingleton(typeof(IConverter), new SynchronizedConverter(new PdfTools()));
            Console.WriteLine("DinkToPdf configurado exitosamente para desarrollo");
        }
        else
        {
            Console.WriteLine("ERROR: libwkhtmltox.dll no encontrado");
            // Fallback: intentar usar PdfTools sin cargar DLL
            builder.Services.AddSingleton(typeof(IConverter), new SynchronizedConverter(new PdfTools()));
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error configurando DinkToPdf: {ex.Message}");
    // Fallback básico
    try
    {
        builder.Services.AddSingleton(typeof(IConverter), new SynchronizedConverter(new PdfTools()));
        Console.WriteLine("Configuración fallback de DinkToPdf aplicada");
    }
    catch (Exception fallbackEx)
    {
        Console.WriteLine($"Error en configuración fallback: {fallbackEx.Message}");
    }
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
    // En producción también mostrar Swagger para testing
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Contratos PDF API V1");
        c.RoutePrefix = "swagger";
    });
}

// Servir archivos estáticos
app.UseStaticFiles();

app.UseCors("AllowFrontend");
app.UseAuthorization();
app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTime.UtcNow,
    environment = app.Environment.EnvironmentName
}));

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
Console.WriteLine($"Servidor iniciado en puerto {port}");
Console.WriteLine($"Ambiente: {app.Environment.EnvironmentName}");
Console.WriteLine("Swagger disponible en /swagger");

app.Run($"http://0.0.0.0:{port}");