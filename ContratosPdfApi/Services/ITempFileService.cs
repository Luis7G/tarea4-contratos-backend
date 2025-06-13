using ContratosPdfApi.Models.DTOs;

namespace ContratosPdfApi.Services
{
    public interface ITempFileService
    {
        Task<TempFileDto> SubirArchivoTemporalAsync(IFormFile archivo, string tipoArchivoCodigo, string sessionId, int? usuarioId);
        Task<List<TempFileDto>> ObtenerArchivosTemporalesAsync(string sessionId);
        Task AsociarArchivosTemporalesAContratoAsync(string sessionId, int contratoId);
        Task LimpiarArchivosTemporalesAsync(string sessionId);
        void LimpiarArchivosExpirados();
    }
}