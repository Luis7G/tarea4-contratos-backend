using ContratosPdfApi.Models.DTOs;

namespace ContratosPdfApi.Services
{
    public interface IArchivoAdjuntoService
    {
        Task<List<TipoArchivoAdjuntoDto>> ObtenerTiposArchivosAdjuntosAsync(string? categoria = null);
        Task<ContratoArchivoAdjuntoDto> SubirArchivoAdjuntoAsync(IFormFile archivo, SubirArchivoAdjuntoDto datos);
        Task<List<ContratoArchivoAdjuntoDto>> ObtenerArchivosAdjuntosPorContratoAsync(int contratoId);
        Task EliminarArchivoAdjuntoAsync(int contratoArchivoAdjuntoId);
    }
}