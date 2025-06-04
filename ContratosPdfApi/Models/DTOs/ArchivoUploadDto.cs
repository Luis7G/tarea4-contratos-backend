namespace ContratosPdfApi.Models.DTOs
{
    public class ArchivoUploadDto
    {
        public string NombreOriginal { get; set; } = string.Empty;
        public string TipoArchivo { get; set; } = string.Empty; // PDF_GENERADO, TABLA_CANTIDADES, etc.
        public int? UsuarioId { get; set; }
    }

}