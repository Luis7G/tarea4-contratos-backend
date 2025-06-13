namespace ContratosPdfApi.Models.DTOs
{
    public class ContratoArchivoAdjuntoDto
    {
        public int Id { get; set; }
        public int ContratoId { get; set; }
        public string TipoArchivoCodigo { get; set; } = string.Empty;
        public string TipoArchivoNombre { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public bool EsObligatorio { get; set; }
        public int ArchivoId { get; set; }
        public string NombreOriginal { get; set; } = string.Empty;
        public string NombreArchivo { get; set; } = string.Empty;
        public string RutaArchivo { get; set; } = string.Empty;
        public string TipoMIME { get; set; } = string.Empty;
        public long Tamaño { get; set; }
        public DateTime FechaSubida { get; set; }
    }

}