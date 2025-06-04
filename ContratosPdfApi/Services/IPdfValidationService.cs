using ContratosPdfApi.Models.DTOs;

namespace ContratosPdfApi.Services
{
    public interface IPdfValidationService
    {
        Task<ValidacionIntegridadResult> ValidarIntegridadPdfAsync(IFormFile archivoPdf);
        Task<List<ArchivoResponseDto>> BuscarPdfOriginalesAsync(IFormFile archivoPdf);
        Task<ComparacionPdfResult> CompararPdfsAsync(IFormFile pdfCargado, ArchivoResponseDto pdfOriginal);
        Task<ValidacionFiremaResult> ValidarFiremasDigitalesAsync(IFormFile archivoPdf);
    }

    // Todas las clases de resultado van aquí también
    public class ValidacionIntegridadResult
    {
        public bool EsValido { get; set; }
        public string Razon { get; set; } = string.Empty;
        public int? PdfOriginalId { get; set; }
        public bool TieneFiremasDigitales { get; set; }
        public List<string> FirmantesDetectados { get; set; } = new();
        public string HashCalculado { get; set; } = string.Empty;
    }

    public class ComparacionPdfResult
    {
        public bool SonCompatibles { get; set; }
        public bool TieneFiremasDigitales { get; set; }
        public List<string> Firmantes { get; set; } = new();
        public string DiferenciasDetectadas { get; set; } = string.Empty;
    }

    public class ValidacionFiremaResult
    {
        public bool TieneFirmasValidas { get; set; }
        public List<FirmaDigitalInfo> Firmas { get; set; } = new();
        public string MensajeError { get; set; } = string.Empty;
    }

    public class FirmaDigitalInfo
    {
        public string NombreFirmante { get; set; } = string.Empty;
        public DateTime FechaFirma { get; set; }
        public bool EsValida { get; set; }
        public string Certificado { get; set; } = string.Empty;
        public string Razon { get; set; } = string.Empty;
    }
}