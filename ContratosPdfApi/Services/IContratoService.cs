using ContratosPdfApi.Models.DTOs;

namespace ContratosPdfApi.Services
{
    public interface IContratoService
    {
        Task<ContratoResponseDto> CrearContratoAsync(ContratoCreateDto contratoDto, string? sessionId = null);
        Task<ContratoResponseDto?> ObtenerContratoPorIdAsync(int id);
        Task<List<ContratoResponseDto>> ListarContratosAsync(string? tipoContrato = null, string? estado = null, int pageNumber = 1, int pageSize = 10);
        Task ActualizarPdfContratoAsync(int contratoId, int archivoPdfId);
        Task AsociarArchivoContratoAsync(int contratoId, int archivoId);
        Task<List<dynamic>> ObtenerTiposContratoAsync();
    }
}