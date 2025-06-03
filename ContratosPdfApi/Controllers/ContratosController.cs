using ContratosPdfApi.Models;
using ContratosPdfApi.Models.DTOs;
using ContratosPdfApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace ContratosPdfApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ContratosController : ControllerBase
    {
        private readonly IContratoService _contratoService;
        private readonly IArchivoService _archivoService;
        private readonly IPdfService _pdfService;
        private readonly ILogger<ContratosController> _logger;

        public ContratosController(
            IContratoService contratoService,
            IArchivoService archivoService,
            IPdfService pdfService,
            ILogger<ContratosController> logger)
        {
            _contratoService = contratoService;
            _archivoService = archivoService;
            _pdfService = pdfService;
            _logger = logger;
        }

        /// <summary>
        /// Crear nuevo contrato
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CrearContrato([FromBody] ContratoCreateDto contratoDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var contrato = await _contratoService.CrearContratoAsync(contratoDto);

                return CreatedAtAction(
                    nameof(ObtenerContrato),
                    new { id = contrato.Id },
                    new { success = true, message = "Contrato creado exitosamente", data = contrato }
                );
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning($"Error de validación al crear contrato: {ex.Message}");
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error interno al crear contrato");
                return StatusCode(500, new { success = false, message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Obtener contrato por ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> ObtenerContrato(int id)
        {
            try
            {
                var contrato = await _contratoService.ObtenerContratoPorIdAsync(id);
                
                if (contrato == null)
                {
                    return NotFound(new { success = false, message = "Contrato no encontrado" });
                }

                return Ok(new { success = true, data = contrato });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al obtener contrato con ID: {id}");
                return StatusCode(500, new { success = false, message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Listar contratos con paginación y filtros
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ListarContratos(
            [FromQuery] string? tipoContrato = null,
            [FromQuery] string? estado = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var contratos = await _contratoService.ListarContratosAsync(tipoContrato, estado, pageNumber, pageSize);

                return Ok(new
                {
                    success = true,
                    data = contratos,
                    pagination = new
                    {
                        pageNumber,
                        pageSize,
                        totalItems = contratos.Count
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar contratos");
                return StatusCode(500, new { success = false, message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Generar y guardar PDF del contrato
        /// </summary>
        [HttpPost("{id}/generar-pdf")]
        public async Task<IActionResult> GenerarPdfContrato(int id, [FromBody] PdfRequest pdfRequest)
        {
            try
            {
                // Verificar que el contrato existe
                var contrato = await _contratoService.ObtenerContratoPorIdAsync(id);
                if (contrato == null)
                {
                    return NotFound(new { success = false, message = "Contrato no encontrado" });
                }

                // Generar PDF usando el servicio existente
                var pdfBytes = _pdfService.GeneratePdfFromHtml(pdfRequest.HtmlContent);

                // Crear archivo temporal para subir
                var tempFileName = $"contrato_{id}_{DateTime.Now:yyyyMMddHHmmss}.pdf";
                var tempFilePath = Path.Combine(Path.GetTempPath(), tempFileName);
                
                await System.IO.File.WriteAllBytesAsync(tempFilePath, pdfBytes);

                // Crear IFormFile desde bytes
                using var stream = new MemoryStream(pdfBytes);
                var formFile = new FormFile(stream, 0, pdfBytes.Length, "file", tempFileName)
                {
                    Headers = new HeaderDictionary(),
                    ContentType = "application/pdf"
                };

                // Subir el PDF como archivo
                var archivoDto = new ArchivoUploadDto
                {
                    NombreOriginal = $"Contrato_{contrato.RucContratista}_{contrato.NombreContratista}.pdf",
                    TipoArchivo = "PDF_GENERADO",
                    UsuarioId = contrato.UsuarioCreadorId
                };

                var archivoPdf = await _archivoService.SubirArchivoAsync(formFile, archivoDto);

                // Actualizar el contrato con el PDF generado
                await _contratoService.ActualizarPdfContratoAsync(id, archivoPdf.Id);

                // Limpiar archivo temporal
                if (System.IO.File.Exists(tempFilePath))
                    System.IO.File.Delete(tempFilePath);

                return Ok(new
                {
                    success = true,
                    message = "PDF generado y guardado exitosamente",
                    data = new
                    {
                        archivoId = archivoPdf.Id,
                        nombreArchivo = archivoPdf.NombreArchivo,
                        rutaArchivo = archivoPdf.RutaArchivo
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al generar PDF para contrato ID: {id}");
                return StatusCode(500, new { success = false, message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Asociar archivo existente a contrato
        /// </summary>
        [HttpPost("{contratoId}/archivos/{archivoId}")]
        public async Task<IActionResult> AsociarArchivo(int contratoId, int archivoId)
        {
            try
            {
                await _contratoService.AsociarArchivoContratoAsync(contratoId, archivoId);

                return Ok(new
                {
                    success = true,
                    message = "Archivo asociado al contrato exitosamente"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al asociar archivo {archivoId} al contrato {contratoId}");
                return StatusCode(500, new { success = false, message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Obtener tipos de contrato disponibles
        /// </summary>
        [HttpGet("tipos")]
        public async Task<IActionResult> ObtenerTiposContrato()
        {
            try
            {
                var tipos = await _contratoService.ObtenerTiposContratoAsync();
                return Ok(new { success = true, data = tipos });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener tipos de contrato");
                return StatusCode(500, new { success = false, message = "Error interno del servidor" });
            }
        }
    }
}