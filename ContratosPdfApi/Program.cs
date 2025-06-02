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
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Configurar DinkToPdf
try
{
    var context = new CustomAssemblyLoadContext();
    var basePath = Directory.GetCurrentDirectory();
    var libraryPath = Path.Combine(basePath, "libwkhtmltox.dll");

    Console.WriteLine($"Cargando DLL desde: {libraryPath}");
    Console.WriteLine($"¿Archivo existe?: {File.Exists(libraryPath)}");

    if (File.Exists(libraryPath))
    {
        context.LoadUnmanagedLibrary(libraryPath);
        builder.Services.AddSingleton(typeof(IConverter), new SynchronizedConverter(new PdfTools()));
        Console.WriteLine("DinkToPdf configurado exitosamente");
    }
    else
    {
        Console.WriteLine("ERROR: libwkhtmltox.dll no encontrado");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error configurando DinkToPdf: {ex.Message}");
}

// Registrar servicios
builder.Services.AddScoped<IPdfService, PdfService>();

// ⭐ AGREGAR: Servicio de limpieza automática de archivos temporales
builder.Services.AddHostedService<TempFileCleanupService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Servir archivos estáticos (incluyendo carpeta temp)
app.UseStaticFiles();

app.UseCors("AllowAngular");
app.UseAuthorization();
app.MapControllers();

Console.WriteLine("Servidor iniciado en http://localhost:5221");
Console.WriteLine("Swagger disponible en http://localhost:5221/swagger");
Console.WriteLine("Assets disponibles en http://localhost:5221/assets/");
Console.WriteLine("Archivos temporales se eliminan automáticamente cada 30 min");

app.Run();