using System.ComponentModel.DataAnnotations;

namespace ContratosPdfApi.Models.DTOs
{
    public class ContratoCreateDto
    {
        [Required(ErrorMessage = "El tipo de contrato es obligatorio")]
        public string TipoContrato { get; set; } = string.Empty;

        public string NumeroContrato { get; set; } = string.Empty; // Opcional, se genera automáticamente

        public string ObjetoContrato { get; set; } = string.Empty;

        [Required(ErrorMessage = "La razón social del contratista es obligatoria")]
        public string RazonSocialContratista { get; set; } = string.Empty;

        [Required(ErrorMessage = "El RUC del contratista es obligatorio")]
        public string RucContratista { get; set; } = string.Empty;

        [Range(0.01, double.MaxValue, ErrorMessage = "El monto total debe ser mayor a 0")]
        public decimal MontoTotal { get; set; }

        [Required(ErrorMessage = "La fecha de inicio es obligatoria")]
        public DateTime FechaInicio { get; set; }

        [Required(ErrorMessage = "La fecha de fin es obligatoria")]
        public DateTime FechaFin { get; set; }

        public string RepresentanteContratante { get; set; } = string.Empty;
        public string CargoRepresentante { get; set; } = string.Empty;
        public string RepresentanteContratista { get; set; } = string.Empty;
        public string CedulaRepresentanteContratista { get; set; } = string.Empty;
        public string DireccionContratista { get; set; } = string.Empty;
        public string TelefonoContratista { get; set; } = string.Empty;
        public string EmailContratista { get; set; } = string.Empty;

        public int? UsuarioId { get; set; }

        public object? DatosEspecificos { get; set; }

        public List<int> ArchivosAsociados { get; set; } = new List<int>();
    }
}