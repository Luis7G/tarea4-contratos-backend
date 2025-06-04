using ContratosPdfApi.Models.DTOs;

namespace ContratosPdfApi.Services
{
    public interface IArchivoService
    {
        Task<ArchivoResponseDto> SubirArchivoAsync(IFormFile archivo, ArchivoUploadDto archivoDto);
        Task<ArchivoResponseDto?> ObtenerArchivoPorIdAsync(int id);
        Task<string> GenerarHashSHA256Async(IFormFile archivo);
        string ObtenerRutaCarpetaPorTipo(string tipoContrato);
    }
}