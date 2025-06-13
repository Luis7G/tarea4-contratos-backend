namespace ContratosPdfApi.Models.DTOs
{

    public class TempFileDto
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string SessionId { get; set; } = string.Empty;
        public string NombreOriginal { get; set; } = string.Empty;
        public string RutaTemporal { get; set; } = string.Empty;
        public string TipoArchivoCodigo { get; set; } = string.Empty;
        public string TipoMIME { get; set; } = string.Empty;
        public long Tamaño { get; set; }
        public DateTime FechaSubida { get; set; }
        public int? UsuarioId { get; set; }
    }
}