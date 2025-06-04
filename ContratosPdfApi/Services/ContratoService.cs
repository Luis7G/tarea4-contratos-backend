using ContratosPdfApi.Models.DTOs;
using Dapper;
using Microsoft.Data.SqlClient;
using Npgsql;
using System.Text.Json;

namespace ContratosPdfApi.Services
{
    public class ContratoService : IContratoService
    {
        // private readonly string _connectionString;
        private readonly IDatabaseHelper _dbHelper;
        private readonly ILogger<ContratoService> _logger;

        public ContratoService(IDatabaseHelper dbHelper, ILogger<ContratoService> logger)
        {
            _dbHelper = dbHelper;
            _logger = logger;
        }

        // MODIFICAR el método CrearContratoAsync en ContratoService.cs:

        // REEMPLAZAR la validación de fecha en ContratoService.cs:

        public async Task<ContratoResponseDto> CrearContratoAsync(ContratoCreateDto contratoDto)
        {
            try
            {
                using var connection = _dbHelper.CreateConnection();

                // CORREGIDO: Obtener ID del tipo de contrato
                var tipoContratoId = await connection.QuerySingleOrDefaultAsync<int?>(
                    "SELECT Id FROM TiposContrato WHERE Codigo = @Codigo AND Activo = true",
                    new { Codigo = contratoDto.TipoContratoCodigo }
                );

                if (tipoContratoId == null)
                    throw new ArgumentException($"Tipo de contrato '{contratoDto.TipoContratoCodigo}' no encontrado");

                _logger.LogInformation($"✅ Tipo de contrato encontrado: ID {tipoContratoId} para código '{contratoDto.TipoContratoCodigo}'");

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

                _logger.LogInformation($"📋 Datos del contrato: Nombre='{contratoDto.NombreContratista}', RUC='{contratoDto.RucContratista}', Monto={contratoDto.MontoContrato}");

                // INSERTAR CONTRATO - COMPATIBLE CON AMBOS PROVEEDORES
                var insertSql = _dbHelper.GetInsertContratoSql();
                int contratoId;

                var parametros = new
                {
                    TipoContratoId = tipoContratoId.Value,  // ← CORREGIDO: usar .Value
                    NumeroContrato = contratoDto.NumeroContrato,
                    NombreContratista = contratoDto.NombreContratista?.Trim(),
                    RucContratista = contratoDto.RucContratista?.Trim(),
                    MontoContrato = contratoDto.MontoContrato,
                    FechaFirmaContrato = fechaFirma,
                    UsuarioCreadorId = contratoDto.UsuarioCreadorId ?? 1,
                    DatosEspecificos = datosEspecificosJson
                };

                _logger.LogInformation($"🔄 Ejecutando SQL: {insertSql}");
                _logger.LogInformation($"📊 Parámetros: TipoContratoId={parametros.TipoContratoId}, Nombre='{parametros.NombreContratista}', RUC='{parametros.RucContratista}', Monto={parametros.MontoContrato}");

                if (_dbHelper.IsPostgreSQL)
                {
                    // PostgreSQL: Ejecutar query directo
                    contratoId = await connection.QuerySingleAsync<int>(insertSql, parametros);

                    // Insertar datos específicos si existen (separado)
                    if (!string.IsNullOrEmpty(datosEspecificosJson))
                    {
                        await connection.ExecuteAsync(
                            "INSERT INTO ContratoDetalles (ContratoId, DatosEspecificos) VALUES (@ContratoId, @DatosEspecificos::jsonb)",
                            new { ContratoId = contratoId, DatosEspecificos = datosEspecificosJson }
                        );
                    }
                }
                else
                {
                    // SQL Server: Ejecutar stored procedure
                    contratoId = await connection.QuerySingleAsync<int>(insertSql, parametros, commandType: System.Data.CommandType.StoredProcedure);
                }

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

        // Métodos privados (actualizados para usar _dbHelper)
        private async Task AsociarArchivoContratoAsync(int contratoId, int archivoId)
        {
            try
            {
                using var connection = _dbHelper.CreateConnection();
                await connection.ExecuteAsync(
                    "INSERT INTO ContratoArchivos (ContratoId, ArchivoId) VALUES (@ContratoId, @ArchivoId)",
                    new { ContratoId = contratoId, ArchivoId = archivoId }
                );

                _logger.LogInformation($"📎 Archivo {archivoId} asociado al contrato {contratoId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error asociando archivo {ArchivoId} al contrato {ContratoId}: {Error}", archivoId, contratoId, ex.Message);
                throw;
            }
        }

        public async Task<ContratoResponseDto?> ObtenerContratoPorIdAsync(int id)
        {
            try
            {
                using var connection = _dbHelper.CreateConnection();

                var contrato = await connection.QuerySingleOrDefaultAsync<ContratoResponseDto>(
                    @"SELECT c.Id, c.NumeroContrato, c.NombreContratista, c.RucContratista,
                     c.MontoContrato, c.FechaFirmaContrato, c.Estado, c.FechaCreacion,
                     c.UsuarioCreadorId, tc.Codigo as TipoContratoCodigo, tc.Nombre as TipoContratoNombre
              FROM Contratos c 
              INNER JOIN TiposContrato tc ON c.TipoContratoId = tc.Id 
              WHERE c.Id = @Id",
                    new { Id = id }
                );

                if (contrato != null)
                {
                    // Obtener archivos asociados
                    var archivos = await connection.QueryAsync<ArchivoResponseDto>(
                        @"SELECT a.Id, a.NombreOriginal, a.NombreArchivo, a.RutaArchivo, a.TipoMIME,
                         a.Tamaño, a.TipoArchivo, a.FechaSubida, a.HashSHA256, a.UsuarioId
                  FROM Archivos a 
                  INNER JOIN ContratoArchivos ca ON a.Id = ca.ArchivoId 
                  WHERE ca.ContratoId = @ContratoId",
                        new { ContratoId = id }
                    );
                    contrato.Archivos = archivos.ToList();
                }

                return contrato;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error obteniendo contrato por ID {Id}: {Error}", id, ex.Message);
                return null;
            }
        }

        public async Task<List<ContratoResponseDto>> ListarContratosAsync(string? tipoContrato = null, string? estado = null, int pageNumber = 1, int pageSize = 10)
        {
            using var connection = _dbHelper.CreateConnection();

            var whereConditions = new List<string>();
            var parameters = new DynamicParameters();

            if (!string.IsNullOrEmpty(tipoContrato))
            {
                whereConditions.Add("tc.Codigo = @TipoContrato");
                parameters.Add("TipoContrato", tipoContrato);
            }

            if (!string.IsNullOrEmpty(estado))
            {
                whereConditions.Add("c.Estado = @Estado");
                parameters.Add("Estado", estado);
            }

            var whereClause = whereConditions.Any() ? "WHERE " + string.Join(" AND ", whereConditions) : "";

            var offset = (pageNumber - 1) * pageSize;
            parameters.Add("Offset", offset);
            parameters.Add("PageSize", pageSize);

            var sql = $@"
        SELECT c.Id, c.NumeroContrato, c.NombreContratista, c.RucContratista,
               c.MontoContrato, c.FechaFirmaContrato, c.Estado, c.FechaCreacion,
               c.UsuarioCreadorId, tc.Codigo as TipoContratoCodigo, tc.Nombre as TipoContratoNombre
        FROM Contratos c 
        INNER JOIN TiposContrato tc ON c.TipoContratoId = tc.Id 
        {whereClause}
        ORDER BY c.FechaCreacion DESC
        " + (_dbHelper.IsPostgreSQL ? "OFFSET @Offset LIMIT @PageSize" : "OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY");

            var contratos = await connection.QueryAsync<ContratoResponseDto>(sql, parameters);
            return contratos.ToList();
        }

        public async Task ActualizarPdfContratoAsync(int contratoId, int archivoPdfId)
        {
            //using var connection = new SqlConnection(_connectionString);
            using var connection = _dbHelper.CreateConnection();

            await connection.ExecuteAsync(
                "SP_ActualizarPdfContrato",
                new { ContratoId = contratoId, ArchivoPdfGeneradoId = archivoPdfId },
                commandType: System.Data.CommandType.StoredProcedure
            );
        }



        public async Task<List<dynamic>> ObtenerTiposContratoAsync()
        {
            //using var connection = new SqlConnection(_connectionString);
            using var connection = _dbHelper.CreateConnection();

            var tipos = await connection.QueryAsync<dynamic>(
                "SP_ObtenerTiposContrato",
                commandType: System.Data.CommandType.StoredProcedure
            );
            return tipos.ToList();
        }

        Task IContratoService.AsociarArchivoContratoAsync(int contratoId, int archivoId)
        {
            return AsociarArchivoContratoAsync(contratoId, archivoId);
        }
    }
}