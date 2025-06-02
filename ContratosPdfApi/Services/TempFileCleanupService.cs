using Microsoft.Extensions.Hosting;

namespace ContratosPdfApi.Services
{
    public class TempFileCleanupService : BackgroundService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<TempFileCleanupService> _logger;

        public TempFileCleanupService(IWebHostEnvironment environment, ILogger<TempFileCleanupService> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🧹 Servicio de limpieza de archivos temporales iniciado");
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    CleanupOldTempFiles();
                    await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken); // Limpiar cada 30 min
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error en servicio de limpieza");
                }
            }
        }

        private void CleanupOldTempFiles()
        {
            var tempFolder = Path.Combine(_environment.WebRootPath, "temp");
            if (!Directory.Exists(tempFolder)) 
            {
                Directory.CreateDirectory(tempFolder);
                return;
            }

            var cutoffTime = DateTime.UtcNow.AddHours(-2); // Eliminar archivos de más de 2 horas
            var files = Directory.GetFiles(tempFolder, "temp_*");
            
            var deletedCount = 0;
            foreach (var file in files)
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTimeUtc < cutoffTime)
                    {
                        File.Delete(file);
                        deletedCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"No se pudo eliminar {Path.GetFileName(file)}: {ex.Message}");
                }
            }

            if (deletedCount > 0)
            {
                _logger.LogInformation($"🗑️ {deletedCount} archivos temporales eliminados");
            }
        }
    }
}