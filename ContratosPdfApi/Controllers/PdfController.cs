using Microsoft.AspNetCore.Mvc;
using ContratosPdfApi.Models;
using ContratosPdfApi.Services;

namespace ContratosPdfApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PdfController : ControllerBase
    {
        private readonly IPdfService _pdfService;
        private readonly ILogger<PdfController> _logger;
        private readonly IWebHostEnvironment _environment;

        public PdfController(IPdfService pdfService, ILogger<PdfController> logger, IWebHostEnvironment environment)
        {
            _pdfService = pdfService;
            _logger = logger;
            _environment = environment;
        }

        [HttpPost("generate")]
        public async Task<IActionResult> GeneratePdf([FromBody] PdfRequest request)
        {
            try
            {
                _logger.LogInformation("=== INICIO ENDPOINT GeneratePdf ===");
                _logger.LogInformation($"Request recibido en: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
                _logger.LogInformation($"Content-Type: {Request.ContentType}");
                _logger.LogInformation($"User-Agent: {Request.Headers.UserAgent}");

                if (request == null)
                {
                    _logger.LogWarning("Request es null");
                    return BadRequest(new { error = "Request no puede ser null" });
                }

                if (string.IsNullOrEmpty(request.HtmlContent))
                {
                    _logger.LogWarning("HtmlContent está vacío");
                    return BadRequest(new { error = "HtmlContent es requerido" });
                }

                _logger.LogInformation($"HTML Content length: {request.HtmlContent.Length}");
                _logger.LogInformation($"HTML Content preview: {request.HtmlContent.Substring(0, Math.Min(200, request.HtmlContent.Length))}...");

                // Verificar que el servicio esté disponible
                if (_pdfService == null)
                {
                    _logger.LogError("PdfService es null");
                    return StatusCode(500, new { error = "Servicio PDF no disponible" });
                }

                // Generar PDF con timeout
                byte[] pdfBytes;
                using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2))) // Timeout de 2 minutos
                {
                    try
                    {
                        pdfBytes = await Task.Run(() => _pdfService.GeneratePdfFromHtml(request.HtmlContent), cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogError("Timeout generando PDF");
                        return StatusCode(500, new { error = "Timeout generando PDF" });
                    }
                }

                if (pdfBytes == null || pdfBytes.Length == 0)
                {
                    _logger.LogError("PDF generado está vacío");
                    return StatusCode(500, new { error = "Error generando PDF - resultado vacío" });
                }

                _logger.LogInformation($"PDF generado exitosamente. Tamaño: {pdfBytes.Length} bytes");
                _logger.LogInformation("=== FIN EXITOSO GeneratePdf ===");

                return File(pdfBytes, "application/pdf", "contrato.pdf");
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Error de validación: {Message}", ex.Message);
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Error de operación: {Message}", ex.Message);
                return StatusCode(500, new { error = $"Error de configuración: {ex.Message}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado generando PDF: {Message}", ex.Message);
                _logger.LogError("Stack trace completo: {StackTrace}", ex.StackTrace);

                return StatusCode(500, new
                {
                    error = "Error interno del servidor",
                    details = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        [HttpPost("upload-temp-image")]
        public async Task<IActionResult> UploadTempImage(IFormFile image)
        {
            try
            {
                _logger.LogInformation("=== INICIO UPLOAD TEMP IMAGE ===");

                if (image == null || image.Length == 0)
                {
                    _logger.LogWarning("No se recibió archivo de imagen");
                    return BadRequest(new { error = "No se recibió archivo de imagen" });
                }

                if (image.Length > 5 * 1024 * 1024) // 5MB máximo
                {
                    _logger.LogWarning($"Archivo muy grande: {image.Length} bytes");
                    return BadRequest(new { error = "Archivo muy grande (máximo 5MB)" });
                }

                // Validar tipo de archivo
                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/bmp" };
                if (!allowedTypes.Contains(image.ContentType.ToLower()))
                {
                    _logger.LogWarning($"Tipo de archivo no válido: {image.ContentType}");
                    return BadRequest(new { error = "Tipo de archivo no válido. Solo se permiten: JPG, PNG, GIF, BMP" });
                }

                // Crear directorio temp en wwwroot
                var wwwrootPath = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
                var tempFolder = Path.Combine(wwwrootPath, "temp");

                if (!Directory.Exists(tempFolder))
                {
                    Directory.CreateDirectory(tempFolder);
                    _logger.LogInformation($"Directorio temp creado: {tempFolder}");
                }

                // Nombre único con timestamp
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var uniqueId = Guid.NewGuid().ToString("N")[..8];
                var extension = Path.GetExtension(image.FileName);
                var fileName = $"temp_{timestamp}_{uniqueId}{extension}";
                var filePath = Path.Combine(tempFolder, fileName);

                // Guardar archivo
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await image.CopyToAsync(stream);
                }

                // URL para acceder a la imagen
                var baseUrl = _environment.IsDevelopment()
                    ? $"{Request.Scheme}://{Request.Host}"
                    : Environment.GetEnvironmentVariable("RENDER_EXTERNAL_URL") ?? "https://contratos-pdf-api.onrender.com";

                var imageUrl = $"{baseUrl}/temp/{fileName}";

                _logger.LogInformation($"Imagen temporal guardada: {fileName} ({image.Length / 1024}KB)");
                _logger.LogInformation($"URL de imagen: {imageUrl}");

                // Programar eliminación automática en 2 horas
                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromHours(2));
                    try
                    {
                        if (System.IO.File.Exists(filePath))
                        {
                            System.IO.File.Delete(filePath);
                            _logger.LogInformation($"Imagen temporal auto-eliminada: {fileName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"No se pudo auto-eliminar imagen temporal: {ex.Message}");
                    }
                });

                return Ok(new
                {
                    success = true,
                    imageUrl = imageUrl,
                    fileName = fileName,
                    size = image.Length,
                    contentType = image.ContentType,
                    expiresIn = "2 horas"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subiendo imagen temporal: {Message}", ex.Message);
                return StatusCode(500, new { error = "Error interno del servidor", details = ex.Message });
            }
        }

        [HttpDelete("cleanup-temp-image/{fileName}")]
        public IActionResult CleanupTempImage(string fileName)
        {
            try
            {
                _logger.LogInformation($"Limpiando imagen temporal: {fileName}");

                // Validar que el nombre del archivo sea válido
                if (string.IsNullOrEmpty(fileName) || !fileName.StartsWith("temp_") || fileName.Contains(".."))
                {
                    _logger.LogWarning($"Nombre de archivo no válido: {fileName}");
                    return BadRequest(new { error = "Nombre de archivo no válido" });
                }

                var wwwrootPath = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
                var tempFolder = Path.Combine(wwwrootPath, "temp");
                var filePath = Path.Combine(tempFolder, fileName);

                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                    _logger.LogInformation($"Imagen temporal eliminada manualmente: {fileName}");
                    return Ok(new { success = true, message = "Imagen eliminada correctamente", fileName = fileName });
                }

                _logger.LogWarning($"Imagen no encontrada: {fileName}");
                return NotFound(new { error = "Imagen no encontrada", fileName = fileName });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error eliminando imagen temporal: {Message}", ex.Message);
                return StatusCode(500, new { error = "Error interno del servidor", details = ex.Message });
            }
        }

        

        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                service = "PdfController"
            });
        }

        [HttpPost("test-simple")]
        public IActionResult TestSimple()
        {
            try
            {
                _logger.LogInformation("Test simple iniciado");

                var simpleHtml = "<html><body><h1>Test PDF</h1><p>Este es un test simple.</p></body></html>";
                var pdfBytes = _pdfService.GeneratePdfFromHtml(simpleHtml);

                _logger.LogInformation($"Test simple completado. PDF size: {pdfBytes.Length}");
                return File(pdfBytes, "application/pdf", "test.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en test simple");
                return StatusCode(500, new { error = ex.Message, details = ex.StackTrace });
            }
        }
    }
}