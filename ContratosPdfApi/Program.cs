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
                "https://your-frontend-domain.com", // Reemplaza con tu dominio de frontend
                "https://*.render.com" // Para otros servicios en Render
              )
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Configurar DinkToPdf - ACTUALIZADO PARA LINUX
try
{
    var context = new CustomAssemblyLoadContext();
    var isProduction = builder.Environment.IsProduction();
    
    if (isProduction)
    {
        // En producción (Linux), usar la librería del sistema
        builder.Services.AddSingleton(typeof(IConverter), new SynchronizedConverter(new PdfTools()));
        Console.WriteLine("DinkToPdf configurado para producción (Linux)");
    }
    else
    {
        // En desarrollo (Windows), usar la DLL local
        var basePath = Directory.GetCurrentDirectory();
        var libraryPath = Path.Combine(basePath, "libwkhtmltox.dll");

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
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error configurando DinkToPdf: {ex.Message}");
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
app.MapGet("/health", () => Results.Ok(new { 
    status = "healthy", 
    timestamp = DateTime.UtcNow,
    environment = app.Environment.EnvironmentName 
}));

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
Console.WriteLine($"Servidor iniciado en puerto {port}");
Console.WriteLine($"Ambiente: {app.Environment.EnvironmentName}");
Console.WriteLine("Swagger disponible en /swagger");

app.Run($"http://0.0.0.0:{port}");