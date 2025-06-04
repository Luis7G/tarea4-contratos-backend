using ContratosPdfApi.Models.DTOs;
using ContratosPdfApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace ContratosPdfApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ArchivosController : ControllerBase
    {
        private readonly IArchivoService _archivoService;
        private readonly IPdfValidationService _pdfValidationService;

        private readonly ILogger<ArchivosController> _logger;

        public ArchivosController(IArchivoService archivoService, IPdfValidationService pdfValidationService, ILogger<ArchivosController> logger)
        {
            _archivoService = archivoService;
            _pdfValidationService = pdfValidationService;
            _logger = logger;
        }

        /// <summary>
        /// Subir archivo (similar al endpoint que tenías antes)
        /// </summary>
        [HttpPost("Subir")]
        public async Task<IActionResult> SubirArchivo(
            [FromForm] IFormFile file,
            [FromForm] string tipoArchivo = "GENERAL",
            [FromForm] int? usuarioId = null)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { message = "No se proporcionó ningún archivo" });
                }

                var archivoDto = new ArchivoUploadDto
                {
                    NombreOriginal = file.FileName,
                    TipoArchivo = tipoArchivo,
                    UsuarioId = usuarioId
                };

                var resultado = await _archivoService.SubirArchivoAsync(file, archivoDto);

                return Ok(new
                {
                    success = true,
                    message = "Archivo subido exitosamente",
                    data = resultado
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning($"Error de validación al subir archivo: {ex.Message}");
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error interno al subir archivo");
                return StatusCode(500, new { success = false, message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Obtener información de archivo por ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> ObtenerArchivo(int id)
        {
            try
            {
                var archivo = await _archivoService.ObtenerArchivoPorIdAsync(id);

                if (archivo == null)
                {
                    return NotFound(new { success = false, message = "Archivo no encontrado" });
                }

                return Ok(new { success = true, data = archivo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al obtener archivo con ID: {id}");
                return StatusCode(500, new { success = false, message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Descargar archivo
        /// </summary>
        [HttpGet("descargar/{id}")]
        public async Task<IActionResult> DescargarArchivo(int id)
        {
            try
            {
                var archivo = await _archivoService.ObtenerArchivoPorIdAsync(id);

                if (archivo == null)
                {
                    return NotFound(new { success = false, message = "Archivo no encontrado" });
                }

                var rutaCompleta = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", archivo.RutaArchivo);

                if (!System.IO.File.Exists(rutaCompleta))
                {
                    return NotFound(new { success = false, message = "Archivo físico no encontrado" });
                }

                var bytes = await System.IO.File.ReadAllBytesAsync(rutaCompleta);
                return File(bytes, archivo.TipoMIME, archivo.NombreOriginal);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al descargar archivo con ID: {id}");
                return StatusCode(500, new { success = false, message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Validar integridad del PDF ANTES de pedir datos
        /// </summary>
        [HttpPost("ValidarIntegridad")]
        public async Task<IActionResult> ValidarIntegridadPdf(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { esValido = false, razon = "No se proporcionó archivo" });
                }

                var validacion = await _pdfValidationService.ValidarIntegridadPdfAsync(file);

                return Ok(validacion);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al validar integridad del PDF");
                return StatusCode(500, new
                {
                    esValido = false,
                    razon = "Error interno del servidor"
                });
            }
        }
    }
}