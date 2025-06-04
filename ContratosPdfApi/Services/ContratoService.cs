using ContratosPdfApi.Models.DTOs;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace ContratosPdfApi.Services
{
    public class ContratoService : IContratoService
    {
        private readonly string _connectionString;
        private readonly ILogger<ContratoService> _logger;

        public ContratoService(IConfiguration configuration, ILogger<ContratoService> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
            _logger = logger;
        }

        // MODIFICAR el método CrearContratoAsync en ContratoService.cs:

        // REEMPLAZAR la validación de fecha en ContratoService.cs:

        public async Task<ContratoResponseDto> CrearContratoAsync(ContratoCreateDto contratoDto)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);

                // Obtener ID del tipo de contrato
                var tipoContrato = await connection.QuerySingleOrDefaultAsync<dynamic>(
                    "SELECT Id FROM TiposContrato WHERE Codigo = @Codigo AND Activo = 1",
                    new { Codigo = contratoDto.TipoContratoCodigo }
                );

                if (tipoContrato == null)
                    throw new ArgumentException($"Tipo de contrato '{contratoDto.TipoContratoCodigo}' no encontrado");

                // VALIDAR Y CONVERTIR LA FECHA DESDE STRING
                DateTime fechaFirma;
                if (string.IsNullOrWhiteSpace(contratoDto.FechaFirmaContrato) ||
                    !DateTime.TryParse(contratoDto.FechaFirmaContrato, out fechaFirma) ||
                    fechaFirma < new DateTime(1753, 1, 1) ||
                    fechaFirma > new DateTime(9999, 12, 31))
                {
                    _logger.LogWarning($"Fecha inválida recibida: '{contratoDto.FechaFirmaContrato}', usando fecha actual");
                    fechaFirma = DateTime.Today;
                }

                _logger.LogInformation($"📅 Fecha procesada para DB: {fechaFirma:yyyy-MM-dd}");

                // Serializar datos específicos a JSON
                string? datosEspecificosJson = null;
                if (contratoDto.DatosEspecificos != null)
                {
                    datosEspecificosJson = JsonSerializer.Serialize(contratoDto.DatosEspecificos);
                }

                // LOG para debugging - ver qué datos llegan
                _logger.LogInformation($"📋 Datos del contrato: Nombre='{contratoDto.NombreContratista}', RUC='{contratoDto.RucContratista}', Monto={contratoDto.MontoContrato}");

                // Insertar contrato
                var contratoId = await connection.QuerySingleAsync<int>(
                    "SP_InsertarContrato",
                    new
                    {
                        TipoContratoId = (int)tipoContrato.Id,
                        NumeroContrato = contratoDto.NumeroContrato,
                        NombreContratista = contratoDto.NombreContratista?.Trim(),
                        RucContratista = contratoDto.RucContratista?.Trim(),
                        MontoContrato = contratoDto.MontoContrato,
                        FechaFirmaContrato = fechaFirma,
                        UsuarioCreadorId = contratoDto.UsuarioCreadorId ?? 1, // Default a 1 si es null
                        DatosEspecificos = datosEspecificosJson
                    },
                    commandType: System.Data.CommandType.StoredProcedure
                );

                // Asociar archivos si se proporcionaron
                if (contratoDto.ArchivosAsociados?.Any() == true)
                {
                    foreach (var archivoId in contratoDto.ArchivosAsociados)
                    {
                        await AsociarArchivoContratoAsync(contratoId, archivoId);
                    }
                }

                _logger.LogInformation($"✅ Contrato creado exitosamente: ID {contratoId}");

                return await ObtenerContratoPorIdAsync(contratoId)
                    ?? throw new Exception("Error al recuperar el contrato creado");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al crear contrato: {Error}", ex.Message);
                throw;
            }
        }

        public async Task<ContratoResponseDto?> ObtenerContratoPorIdAsync(int id)
        {
            using var connection = new SqlConnection(_connectionString);
            
            // Usar multiple result sets del SP
            using var multi = await connection.QueryMultipleAsync(
                "SP_ObtenerContratoPorId",
                new { ContratoId = id },
                commandType: System.Data.CommandType.StoredProcedure
            );

            var contrato = await multi.ReadSingleOrDefaultAsync<dynamic>();
            if (contrato == null) return null;

            var archivos = (await multi.ReadAsync<ArchivoResponseDto>()).ToList();
            var datosEspecificos = await multi.ReadSingleOrDefaultAsync<string>();

            return new ContratoResponseDto
            {
                Id = contrato.Id,
                TipoContratoCodigo = contrato.TipoContratoCodigo,
                TipoContratoNombre = contrato.TipoContratoNombre,
                NumeroContrato = contrato.NumeroContrato,
                NombreContratista = contrato.NombreContratista,
                RucContratista = contrato.RucContratista,
                MontoContrato = contrato.MontoContrato,
                FechaFirmaContrato = contrato.FechaFirmaContrato,
                Estado = contrato.Estado,
                FechaCreacion = contrato.FechaCreacion,
                UsuarioCreadorNombre = contrato.UsuarioCreadorNombre,
                Archivos = archivos,
                DatosEspecificos = !string.IsNullOrEmpty(datosEspecificos) 
                    ? JsonSerializer.Deserialize<object>(datosEspecificos) 
                    : null
            };
        }

        public async Task<List<ContratoResponseDto>> ListarContratosAsync(string? tipoContrato = null, string? estado = null, int pageNumber = 1, int pageSize = 10)
        {
            using var connection = new SqlConnection(_connectionString);
            
            // Obtener TipoContratoId si se proporciona el código
            int? tipoContratoId = null;
            if (!string.IsNullOrEmpty(tipoContrato))
            {
                var tipo = await connection.QuerySingleOrDefaultAsync<dynamic>(
                    "SELECT Id FROM TiposContrato WHERE Codigo = @Codigo AND Activo = 1",
                    new { Codigo = tipoContrato }
                );
                tipoContratoId = tipo?.Id;
            }

            using var multi = await connection.QueryMultipleAsync(
                "SP_ListarContratos",
                new
                {
                    TipoContratoId = tipoContratoId,
                    Estado = estado,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                },
                commandType: System.Data.CommandType.StoredProcedure
            );

            var contratos = await multi.ReadAsync<dynamic>();
            var total = await multi.ReadSingleAsync<int>();

            return contratos.Select(c => new ContratoResponseDto
            {
                Id = c.Id,
                TipoContratoCodigo = c.TipoContratoCodigo,
                TipoContratoNombre = c.TipoContratoNombre,
                NumeroContrato = c.NumeroContrato,
                NombreContratista = c.NombreContratista,
                RucContratista = c.RucContratista,
                MontoContrato = c.MontoContrato,
                FechaFirmaContrato = c.FechaFirmaContrato,
                Estado = c.Estado,
                FechaCreacion = c.FechaCreacion,
                UsuarioCreadorNombre = c.UsuarioCreadorNombre
            }).ToList();
        }

        public async Task ActualizarPdfContratoAsync(int contratoId, int archivoPdfId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.ExecuteAsync(
                "SP_ActualizarPdfContrato",
                new { ContratoId = contratoId, ArchivoPdfGeneradoId = archivoPdfId },
                commandType: System.Data.CommandType.StoredProcedure
            );
        }

        public async Task AsociarArchivoContratoAsync(int contratoId, int archivoId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.ExecuteAsync(
                "SP_AsociarArchivoContrato",
                new { ContratoId = contratoId, ArchivoId = archivoId },
                commandType: System.Data.CommandType.StoredProcedure
            );
        }

        public async Task<List<dynamic>> ObtenerTiposContratoAsync()
        {
            using var connection = new SqlConnection(_connectionString);
            var tipos = await connection.QueryAsync<dynamic>(
                "SP_ObtenerTiposContrato",
                commandType: System.Data.CommandType.StoredProcedure
            );
            return tipos.ToList();
        }
    }
}