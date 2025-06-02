using Microsoft.AspNetCore.Mvc;

namespace ContratosPdfApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ImageController : ControllerBase
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ImageController> _logger;

        public ImageController(IWebHostEnvironment environment, ILogger<ImageController> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        [HttpPost("upload-temp")]
        public async Task<IActionResult> UploadTempImage(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest("No se recibió archivo");

                if (file.Length > 3 * 1024 * 1024) // 3MB máximo
                    return BadRequest("Archivo muy grande (máximo 3MB)");

                // Validar tipo de archivo
                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif" };
                if (!allowedTypes.Contains(file.ContentType.ToLower()))
                    return BadRequest("Tipo de archivo no válido. Solo se permiten: JPG, PNG, GIF");

                var tempFolder = Path.Combine(_environment.WebRootPath, "temp");
                if (!Directory.Exists(tempFolder))
                    Directory.CreateDirectory(tempFolder);

                // Nombre único con timestamp para auto-limpieza
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var uniqueId = Guid.NewGuid().ToString("N")[..8]; // Solo 8 caracteres
                var extension = Path.GetExtension(file.FileName);
                var fileName = $"temp_{timestamp}_{uniqueId}{extension}";
                var filePath = Path.Combine(tempFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Programar eliminación automática en 1 hora
                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromHours(1));
                    try
                    {
                        if (System.IO.File.Exists(filePath)) // Fix: Use System.IO.File instead of ControllerBase.File
                        {
                            System.IO.File.Delete(filePath); // Fix: Use System.IO.File instead of ControllerBase.File
                            _logger.LogInformation($"Imagen temporal auto-eliminada: {fileName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"No se pudo auto-eliminar imagen temporal: {ex.Message}");
                    }
                });

                var imageUrl = $"http://localhost:5221/temp/{fileName}";
                _logger.LogInformation($"Imagen temporal subida: {fileName} ({file.Length / 1024}KB)");

                return Ok(new
                {
                    imageUrl = imageUrl,
                    fileName = fileName,
                    size = file.Length,
                    contentType = file.ContentType,
                    expiresIn = "1 hora"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subiendo imagen temporal");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        [HttpDelete("cleanup/{fileName}")]
        public IActionResult CleanupTempImage(string fileName)
        {
            try
            {
                // Validar que el nombre del archivo sea válido
                if (!fileName.StartsWith("temp_") || fileName.Contains(".."))
                    return BadRequest("Nombre de archivo no válido");

                var tempFolder = Path.Combine(_environment.WebRootPath, "temp");
                var filePath = Path.Combine(tempFolder, fileName);

                if (System.IO.File.Exists(filePath)) // Fix: Use System.IO.File instead of ControllerBase.File
                {
                    System.IO.File.Delete(filePath); // Fix: Use System.IO.File instead of ControllerBase.File
                    _logger.LogInformation($"Imagen temporal eliminada manualmente: {fileName}");
                    return Ok(new { message = "Imagen eliminada correctamente", fileName = fileName });
                }

                return NotFound(new { message = "Imagen no encontrada", fileName = fileName });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error eliminando imagen temporal");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        [HttpGet("temp-info")]
        public IActionResult GetTempInfo()
        {
            try
            {
                var tempFolder = Path.Combine(_environment.WebRootPath, "temp");
                if (!Directory.Exists(tempFolder))
                    return Ok(new { totalFiles = 0, totalSizeMB = 0 });

                var files = Directory.GetFiles(tempFolder, "temp_*");
                var totalSize = files.Sum(f => new FileInfo(f).Length);

                return Ok(new
                {
                    totalFiles = files.Length,
                    totalSizeMB = Math.Round(totalSize / 1024.0 / 1024.0, 2),
                    files = files.Select(f => new
                    {
                        name = Path.GetFileName(f),
                        sizeMB = Math.Round(new FileInfo(f).Length / 1024.0 / 1024.0, 2),
                        created = new FileInfo(f).CreationTime
                    }).ToArray()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo info de archivos temporales");
                return StatusCode(500, "Error interno del servidor");
            }
        }
    }
}