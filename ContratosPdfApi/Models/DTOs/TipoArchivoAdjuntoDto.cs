namespace ContratosPdfApi.Models.DTOs
{
    public class TipoArchivoAdjuntoDto
    {
        public int Id { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public bool EsObligatorio { get; set; }
    }
    
}