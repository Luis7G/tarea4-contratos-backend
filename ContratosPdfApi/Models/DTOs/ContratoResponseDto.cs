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
        public int? UsuarioCreadorId { get; set; }
        
        // Propiedades adicionales para compatibilidad con el frontend
        public string TipoContrato => TipoContratoCodigo;
        public string RazonSocialContratista => NombreContratista;
        public decimal MontoTotal => MontoContrato;
        public DateTime FechaInicio => FechaFirmaContrato;
        public DateTime FechaFin => FechaFirmaContrato;
        
        // Datos adicionales del contrato
        public string RepresentanteContratante { get; set; } = string.Empty;
        public string CargoRepresentante { get; set; } = string.Empty;
        public string RepresentanteContratista { get; set; } = string.Empty;
        public string CedulaRepresentanteContratista { get; set; } = string.Empty;
        public string DireccionContratista { get; set; } = string.Empty;
        public string TelefonoContratista { get; set; } = string.Empty;
        public string EmailContratista { get; set; } = string.Empty;
        public int? UsuarioId => UsuarioCreadorId; // CORREGIR: era UsuarioCreatorId
        
        public List<ArchivoResponseDto> Archivos { get; set; } = new List<ArchivoResponseDto>();
        public object? DatosEspecificos { get; set; }
    }
}