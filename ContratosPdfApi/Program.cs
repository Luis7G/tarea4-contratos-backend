using ContratosPdfApi.Services;
using DinkToPdf;
using DinkToPdf.Contracts;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// ═══════════════════════════════════════════════════════════
// CONFIGURACIÓN DE BASE DE DATOS - AUTOMÁTICA SEGÚN ENTORNO
// ═══════════════════════════════════════════════════════════

// Detectar si estamos en Render (tiene DATABASE_URL)
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
if (!string.IsNullOrEmpty(databaseUrl))
{
    Console.WriteLine("🐘 Detectado entorno Render - usando PostgreSQL");
    builder.Configuration["DatabaseProvider"] = "PostgreSQL";
    
    // Parsear DATABASE_URL de Render
    var uri = new Uri(databaseUrl);
    var connectionString = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.Trim('/')};Username={uri.UserInfo.Split(':')[0]};Password={uri.UserInfo.Split(':')[1]};SSL Mode=Require;Trust Server Certificate=true;";
    builder.Configuration["ConnectionStrings:PostgreSQLConnection"] = connectionString;
    
    Console.WriteLine($"✅ Conexión PostgreSQL configurada");
}
else
{
    Console.WriteLine("🏠 Entorno local - usando SQL Server");
    builder.Configuration["DatabaseProvider"] = "SqlServer";
}

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

// Registrar servicios de Dapper/SQL

builder.Services.AddScoped<IDatabaseHelper, DatabaseHelper>(); 
builder.Services.AddScoped<IArchivoService, ArchivoService>();
builder.Services.AddScoped<IContratoService, ContratoService>();
builder.Services.AddScoped<IPdfValidationService, PdfValidationService>();
builder.Services.AddScoped<IPdfService, PdfService>();
builder.Services.AddHostedService<TempFileCleanupService>();

// Configurar wwwroot para archivos estáticos
builder.Services.Configure<StaticFileOptions>(options =>
{
    options.FileProvider = new PhysicalFileProvider(
        Path.Combine(builder.Environment.WebRootPath, "Uploads"));
    options.RequestPath = "/uploads";
});

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

// Configurar archivos estáticos ANTES de UseCors
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(app.Environment.WebRootPath, "Uploads")),
    RequestPath = "/uploads"
});

// También puedes agregar configuración específica para assets
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "assets")),
    RequestPath = "/assets"
});

// AGREGAR: Configurar archivos estáticos para archivos temporales
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(app.Environment.WebRootPath, "temp")),
    RequestPath = "/temp"
});

app.UseCors("AllowFrontend");
app.UseAuthorization();
app.MapControllers();

// Crear directorios necesarios al inicio
var webRootPath = app.Environment.WebRootPath;
var directoriosNecesarios = new[]
{
    "Uploads/Contratos/Bienes/PDFs",
    "Uploads/Contratos/Bienes/TablaCantidades",
    "Uploads/Contratos/Bienes/Respaldos",
    "Uploads/Contratos/Servicios",
    "Uploads/Contratos/Obras",
    "Uploads/Contratos/Consultoria",
    "Uploads/Temp"
};

foreach (var directorio in directoriosNecesarios)
{
    var rutaCompleta = Path.Combine(webRootPath, directorio);
    Directory.CreateDirectory(rutaCompleta);
}


app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTime.UtcNow,
    environment = app.Environment.EnvironmentName
}));


var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
Console.WriteLine($"Servidor iniciado en puerto {port}");

app.Run($"http://0.0.0.0:{port}");