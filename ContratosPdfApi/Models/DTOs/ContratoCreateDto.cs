namespace ContratosPdfApi.Models.DTOs
{
    public class ContratoCreateDto
    {
        public string TipoContratoCodigo { get; set; } = "BIENES"; // Por defecto
        public string? NumeroContrato { get; set; }
        public string NombreContratista { get; set; } = string.Empty;
        public string RucContratista { get; set; } = string.Empty;
        public decimal MontoContrato { get; set; }
        public string FechaFirmaContrato { get; set; } = string.Empty; // ← CAMBIAR A STRING
        public int? UsuarioCreadorId { get; set; }

        // Datos específicos del contrato de bienes (JSON)
        public object? DatosEspecificos { get; set; }

        // IDs de archivos ya subidos
        public List<int> ArchivosAsociados { get; set; } = new List<int>();
    }


}