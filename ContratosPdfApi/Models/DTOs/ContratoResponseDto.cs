namespace ContratosPdfApi.Models.DTOs
{
    

    public class ContratoResponseDto
    {
        public int Id { get; set; }
        public string TipoContratoCodigo { get; set; } = string.Empty;
        public string TipoContratoNombre { get; set; } = string.Empty;
        public string? NumeroContrato { get; set; }
        public string NombreContratista { get; set; } = string.Empty;
        public string RucContratista { get; set; } = string.Empty;
        public decimal MontoContrato { get; set; }
        public DateTime FechaFirmaContrato { get; set; }
        public string Estado { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; }
        public string? UsuarioCreadorNombre { get; set; }
        
        public List<ArchivoResponseDto> Archivos { get; set; } = new List<ArchivoResponseDto>();
        public object? DatosEspecificos { get; set; }
        public int? UsuarioCreadorId { get; internal set; }
    }
}