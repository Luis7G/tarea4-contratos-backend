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

        public PdfController(IPdfService pdfService, ILogger<PdfController> logger)
        {
            _pdfService = pdfService;
            _logger = logger;
        }

        [HttpPost("generate")]
        public IActionResult GeneratePdf([FromBody] PdfRequest request)
        {
            try
            {
                _logger.LogInformation("Iniciando generación de PDF");

                if (request == null || string.IsNullOrEmpty(request.HtmlContent))
                {
                    _logger.LogWarning("Request es null o HtmlContent está vacío");
                    return BadRequest("HtmlContent es requerido");
                }

                _logger.LogInformation($"HTML Content length: {request.HtmlContent.Length}");

                var pdfBytes = _pdfService.GeneratePdfFromHtml(request.HtmlContent);

                if (pdfBytes == null || pdfBytes.Length == 0)
                {
                    _logger.LogError("PDF generado está vacío");
                    return StatusCode(500, "Error generando PDF");
                }

                _logger.LogInformation($"PDF generado exitosamente. Tamaño: {pdfBytes.Length} bytes");

                return File(pdfBytes, "application/pdf", "contrato.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generando PDF: {Message}", ex.Message);
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpPost("generate-from-data")]
        public IActionResult GeneratePdfFromData([FromBody] ContratoData contratoData)
        {
            try
            {
                if (contratoData == null)
                {
                    return BadRequest("ContratoData is required.");
                }

                var htmlContent = _pdfService.GenerateContractHtml(contratoData);
                var pdfBytes = _pdfService.GeneratePdfFromHtml(htmlContent);

                if (pdfBytes == null || pdfBytes.Length == 0)
                {
                    return StatusCode(500, "Error generating PDF.");
                }

                return File(pdfBytes, "application/pdf", $"contrato_{contratoData.NombreContratista}.pdf");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error generating PDF: {ex.Message}");
            }
        }
    }
}