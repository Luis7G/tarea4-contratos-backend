namespace ContratosPdfApi.Models.DTOs
{

    public class SubirArchivoAdjuntoDto
    {
        public int ContratoId { get; set; }
        public string TipoArchivoCodigo { get; set; } = string.Empty;
        public int? UsuarioId { get; set; }
    }
}