namespace ContratosPdfApi.Models.DTOs
{

    public class ArchivoResponseDto
    {
        public int Id { get; set; }
        public string NombreOriginal { get; set; } = string.Empty;
        public string NombreArchivo { get; set; } = string.Empty;
        public string RutaArchivo { get; set; } = string.Empty;
        public string TipoMIME { get; set; } = string.Empty;
        public long Tamaño { get; set; }
        public string TipoArchivo { get; set; } = string.Empty;
        public DateTime FechaSubida { get; set; }
    }
}