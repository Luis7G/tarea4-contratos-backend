using ContratosPdfApi.Models.DTOs;
using ContratosPdfApi.Services;
using Dapper;
using Microsoft.Data.SqlClient;

namespace ContratosPdfApi.Services
{
    public class ArchivoAdjuntoService : IArchivoAdjuntoService
    {
        private readonly string _connectionString;
        private readonly IArchivoService _archivoService;
        private readonly ILogger<ArchivoAdjuntoService> _logger;

        public ArchivoAdjuntoService(
            IConfiguration configuration,
            IArchivoService archivoService,
            ILogger<ArchivoAdjuntoService> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
            _archivoService = archivoService;
            _logger = logger;
        }

        public async Task<List<TipoArchivoAdjuntoDto>> ObtenerTiposArchivosAdjuntosAsync(string? categoria = null)
        {
            using var connection = new SqlConnection(_connectionString);
            var tipos = await connection.QueryAsync<TipoArchivoAdjuntoDto>(
                "SP_ObtenerTiposArchivosAdjuntos",
                new { Categoria = categoria },
                commandType: System.Data.CommandType.StoredProcedure
            );
            return tipos.ToList();
        }

        public async Task<ContratoArchivoAdjuntoDto> SubirArchivoAdjuntoAsync(IFormFile archivo, SubirArchivoAdjuntoDto datos)
        {
            try
            {
                // 1. Subir archivo usando el servicio existente
                var archivoDto = new ArchivoUploadDto
                {
                    NombreOriginal = archivo.FileName,
                    TipoArchivo = $"ADJUNTO_{datos.TipoArchivoCodigo}",
                    UsuarioId = datos.UsuarioId
                };

                var archivoSubido = await _archivoService.SubirArchivoAsync(archivo, archivoDto);

                // 2. Obtener el ID del tipo de archivo adjunto
                using var connection = new SqlConnection(_connectionString);
                var tipoArchivo = await connection.QuerySingleOrDefaultAsync<TipoArchivoAdjuntoDto>(
                    "SELECT Id, Codigo, Nombre, Categoria, EsObligatorio FROM TiposArchivosAdjuntos WHERE Codigo = @Codigo AND Activo = 1",
                    new { Codigo = datos.TipoArchivoCodigo }
                );

                if (tipoArchivo == null)
                    throw new ArgumentException($"Tipo de archivo adjunto '{datos.TipoArchivoCodigo}' no encontrado");

                // 3. Asociar archivo al contrato
                var contratoArchivoId = await connection.QuerySingleAsync<int>(
                    "SP_AsociarArchivoAdjuntoContrato",
                    new
                    {
                        ContratoId = datos.ContratoId,
                        TipoArchivoAdjuntoId = tipoArchivo.Id,
                        ArchivoId = archivoSubido.Id,
                        UsuarioId = datos.UsuarioId
                    },
                    commandType: System.Data.CommandType.StoredProcedure
                );

                return new ContratoArchivoAdjuntoDto
                {
                    Id = contratoArchivoId,
                    ContratoId = datos.ContratoId,
                    TipoArchivoCodigo = tipoArchivo.Codigo,
                    TipoArchivoNombre = tipoArchivo.Nombre,
                    Categoria = tipoArchivo.Categoria,
                    EsObligatorio = tipoArchivo.EsObligatorio,
                    ArchivoId = archivoSubido.Id,
                    NombreOriginal = archivoSubido.NombreOriginal,
                    NombreArchivo = archivoSubido.NombreArchivo,
                    RutaArchivo = archivoSubido.RutaArchivo,
                    TipoMIME = archivoSubido.TipoMIME,
                    Tamaño = archivoSubido.Tamaño,
                    FechaSubida = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al subir archivo adjunto: {archivo.FileName}");
                throw;
            }
        }

        public async Task<List<ContratoArchivoAdjuntoDto>> ObtenerArchivosAdjuntosPorContratoAsync(int contratoId)
        {
            using var connection = new SqlConnection(_connectionString);
            var archivos = await connection.QueryAsync<ContratoArchivoAdjuntoDto>(
                "SP_ObtenerArchivosAdjuntosPorContrato",
                new { ContratoId = contratoId },
                commandType: System.Data.CommandType.StoredProcedure
            );
            return archivos.ToList();
        }

        public async Task EliminarArchivoAdjuntoAsync(int contratoArchivoAdjuntoId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.ExecuteAsync(
                "DELETE FROM ContratoArchivosAdjuntos WHERE Id = @Id",
                new { Id = contratoArchivoAdjuntoId }
            );
        }
    }
}