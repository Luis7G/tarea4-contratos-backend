using ContratosPdfApi.Models.DTOs;
using ContratosPdfApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace ContratosPdfApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ArchivosAdjuntosController : ControllerBase
    {
        private readonly IArchivoAdjuntoService _archivoAdjuntoService;
        private readonly ILogger<ArchivosAdjuntosController> _logger;
        private readonly ITempFileService _tempFileService;


        public ArchivosAdjuntosController(
            IArchivoAdjuntoService archivoAdjuntoService,
            ILogger<ArchivosAdjuntosController> logger,
            ITempFileService tempFileService)
        {
            _archivoAdjuntoService = archivoAdjuntoService;
            _logger = logger;
            _tempFileService = tempFileService;
        }



        [HttpPost("subir")]
        public async Task<IActionResult> SubirArchivoAdjunto(
            [FromForm] IFormFile archivo,
            [FromForm] int contratoId,
            [FromForm] string tipoArchivoCodigo,
            [FromForm] int? usuarioId = null)
        {
            try
            {
                if (archivo == null || archivo.Length == 0)
                {
                    return BadRequest(new { success = false, message = "No se proporcionó archivo" });
                }

                var datos = new SubirArchivoAdjuntoDto
                {
                    ContratoId = contratoId,
                    TipoArchivoCodigo = tipoArchivoCodigo,
                    UsuarioId = usuarioId
                };

                var resultado = await _archivoAdjuntoService.SubirArchivoAdjuntoAsync(archivo, datos);

                return Ok(new
                {
                    success = true,
                    message = "Archivo adjunto subido exitosamente",
                    data = resultado
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al subir archivo adjunto");
                return StatusCode(500, new { success = false, message = "Error interno del servidor" });
            }
        }

        [HttpGet("contrato/{contratoId}")]
        public async Task<IActionResult> ObtenerArchivosAdjuntosPorContrato(int contratoId)
        {
            try
            {
                var archivos = await _archivoAdjuntoService.ObtenerArchivosAdjuntosPorContratoAsync(contratoId);
                return Ok(new { success = true, data = archivos });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al obtener archivos adjuntos del contrato {contratoId}");
                return StatusCode(500, new { success = false, message = "Error interno del servidor" });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> EliminarArchivoAdjunto(int id)
        {
            try
            {
                await _archivoAdjuntoService.EliminarArchivoAdjuntoAsync(id);
                return Ok(new { success = true, message = "Archivo adjunto eliminado exitosamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al eliminar archivo adjunto {id}");
                return StatusCode(500, new { success = false, message = "Error interno del servidor" });
            }
        }

        [HttpPost("subir-temporal")]
        public async Task<IActionResult> SubirArchivoTemporal(
            [FromForm] IFormFile archivo,
            [FromForm] string tipoArchivoCodigo,
            [FromForm] string sessionId,
            [FromForm] int? usuarioId = null)
        {
            try
            {
                if (archivo == null || archivo.Length == 0)
                    return BadRequest(new { success = false, message = "No se proporcionó archivo" });

                if (string.IsNullOrEmpty(sessionId))
                    return BadRequest(new { success = false, message = "SessionId requerido" });

                if (string.IsNullOrEmpty(tipoArchivoCodigo))
                    return BadRequest(new { success = false, message = "Tipo de archivo requerido" });

                // Validar tamaño del archivo (5MB máximo)
                if (archivo.Length > 5 * 1024 * 1024)
                    return BadRequest(new { success = false, message = "El archivo es muy grande (máximo 5MB)" });

                var resultado = await _tempFileService.SubirArchivoTemporalAsync(archivo, tipoArchivoCodigo, sessionId, usuarioId);

                return Ok(new
                {
                    success = true,
                    message = "Archivo temporal subido exitosamente",
                    data = resultado
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al subir archivo temporal");
                return StatusCode(500, new { success = false, message = "Error interno del servidor" });
            }
        }

        [HttpGet("temporales/{sessionId}")]
        public async Task<IActionResult> ObtenerArchivosTemporales(string sessionId)
        {
            try
            {
                if (string.IsNullOrEmpty(sessionId))
                    return BadRequest(new { success = false, message = "SessionId requerido" });

                var archivos = await _tempFileService.ObtenerArchivosTemporalesAsync(sessionId);
                return Ok(new { success = true, data = archivos });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener archivos temporales");
                return StatusCode(500, new { success = false, message = "Error interno del servidor" });
            }
        }

        [HttpDelete("temporales/{sessionId}")]
        public async Task<IActionResult> LimpiarArchivosTemporales(string sessionId)
        {
            try
            {
                if (string.IsNullOrEmpty(sessionId))
                    return BadRequest(new { success = false, message = "SessionId requerido" });

                await _tempFileService.LimpiarArchivosTemporalesAsync(sessionId);
                return Ok(new { success = true, message = "Archivos temporales eliminados" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al limpiar archivos temporales");
                return StatusCode(500, new { success = false, message = "Error interno del servidor" });
            }
        }

        [HttpGet("tipos")]
        public async Task<IActionResult> ObtenerTiposArchivosAdjuntos()
        {
            try
            {
                var tipos = await _archivoAdjuntoService.ObtenerTiposArchivosAdjuntosAsync();
                return Ok(new { success = true, data = tipos });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener tipos de archivos adjuntos");
                return StatusCode(500, new { success = false, message = "Error interno del servidor" });
            }
        }
    }
}