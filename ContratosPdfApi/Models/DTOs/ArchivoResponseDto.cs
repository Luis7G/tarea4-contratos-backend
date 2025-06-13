namespace ContratosPdfApi.Models.DTOs
{

    public class ArchivoResponseDto
    {
        public int Id { get; set; }
        public string NombreOriginal { get; set; }
        public string NombreArchivo { get; set; }
        public string RutaArchivo { get; set; }
        public string TipoMIME { get; set; }
        public long Tamaño { get; set; }
        public string TipoArchivo { get; set; }
        public DateTime FechaSubida { get; set; }
        public string HashSHA256 { get; set; }
        public int? UsuarioId { get; internal set; }
    }
}