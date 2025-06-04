using System.Text.Json;
using ContratosPdfApi.Models;
using ContratosPdfApi.Models.DTOs;
using ContratosPdfApi.Services;
using Dapper;
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
        private readonly IDatabaseHelper _dbHelper;


        public ContratosController(
            IContratoService contratoService,
            IArchivoService archivoService,
            IPdfService pdfService,
            ILogger<ContratosController> logger
            ,
            IDatabaseHelper dbHelper)
        {
            _contratoService = contratoService;
            _archivoService = archivoService;
            _pdfService = pdfService;
            _logger = logger;
            _dbHelper = dbHelper;
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

        // AGREGAR este método al ContratosController.cs después de los métodos existentes:

        /// <summary>
        /// Subir PDF validado y crear contrato
        /// </summary>
        [HttpPost("SubirPdfValidado")]
        public async Task<IActionResult> SubirPdfValidado([FromForm] IFormFile file, [FromForm] string datosContrato, [FromForm] string validacionIntegridad)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { success = false, message = "No se proporcionó archivo" });
                }

                _logger.LogInformation($"📋 JSON recibido: {datosContrato}");

                // CONFIGURAR opciones de deserialización más permisivas
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,  // ← CLAVE: Ignorar mayúsculas/minúsculas
                    AllowTrailingCommas = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase  // ← Convertir camelCase a PascalCase
                };

                // Deserializar datos del contrato
                var datos = JsonSerializer.Deserialize<ContratoCreateDto>(datosContrato, options);
                if (datos == null)
                {
                    return BadRequest(new { success = false, message = "Datos del contrato inválidos" });
                }

                // LOG para verificar datos deserializados ANTES de validaciones
                _logger.LogInformation($"📋 Datos DESPUÉS de deserializar:");
                _logger.LogInformation($"   - NombreContratista: '{datos.NombreContratista}'");
                _logger.LogInformation($"   - RucContratista: '{datos.RucContratista}'");
                _logger.LogInformation($"   - MontoContrato: {datos.MontoContrato}");
                _logger.LogInformation($"   - FechaFirmaContrato: '{datos.FechaFirmaContrato}'");

                // VALIDAR que los datos importantes no estén vacíos
                if (string.IsNullOrWhiteSpace(datos.NombreContratista))
                {
                    return BadRequest(new { success = false, message = "El nombre del contratista es obligatorio" });
                }

                if (string.IsNullOrWhiteSpace(datos.RucContratista))
                {
                    return BadRequest(new { success = false, message = "El RUC del contratista es obligatorio" });
                }

                if (datos.MontoContrato <= 0)
                {
                    return BadRequest(new { success = false, message = "El monto debe ser mayor a 0" });
                }

                // Subir archivo
                var archivoDto = new ArchivoUploadDto
                {
                    NombreOriginal = file.FileName,
                    TipoArchivo = "PDF_FIRMADO",
                    UsuarioId = datos.UsuarioCreadorId ?? 1
                };

                var archivo = await _archivoService.SubirArchivoAsync(file, archivoDto);

                // Crear contrato con archivo asociado
                datos.ArchivosAsociados = new List<int> { archivo.Id };
                datos.UsuarioCreadorId = datos.UsuarioCreadorId ?? 1; // Asegurar que no sea null

                var contrato = await _contratoService.CrearContratoAsync(datos);

                _logger.LogInformation($"✅ PDF validado subido exitosamente: Contrato ID {contrato.Id}");

                return Ok(new
                {
                    success = true,
                    message = "PDF validado y contrato guardado exitosamente",
                    data = new
                    {
                        contrato = contrato,
                        archivo = archivo
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al subir PDF validado: {Error}", ex.Message);
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error interno del servidor",
                    details = ex.Message
                });
            }
        }

        [HttpGet("database-status")]
        public async Task<IActionResult> DatabaseStatus()
        {
            try
            {
                using var connection = _dbHelper.CreateConnection();

                // Verificar tablas existentes
                var tablas = await connection.QueryAsync<string>(
                    _dbHelper.IsPostgreSQL
                        ? "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'"
                        : "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'"
                );

                // Verificar tipos de contrato si la tabla existe
                List<dynamic>? tiposContrato = null;
                if (tablas.Contains("tiposcontrato") || tablas.Contains("TiposContrato"))
                {
                    tiposContrato = (await connection.QueryAsync<dynamic>("SELECT * FROM TiposContrato")).ToList();
                }

                // Verificar usuarios
                List<dynamic>? usuarios = null;
                if (tablas.Contains("usuarios") || tablas.Contains("Usuarios"))
                {
                    usuarios = (await connection.QueryAsync<dynamic>("SELECT * FROM Usuarios")).ToList();
                }

                return Ok(new
                {
                    DatabaseProvider = _dbHelper.IsPostgreSQL ? "PostgreSQL" : "SQL Server",
                    TablasExistentes = tablas.ToList(),
                    TiposContrato = tiposContrato,
                    Usuarios = usuarios,
                    DatabaseConnected = true
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = ex.Message,
                    DatabaseConnected = false
                });
            }
        }

    }
}