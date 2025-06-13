using ContratosPdfApi.Models.DTOs;
using Dapper;
using Microsoft.Data.SqlClient;
using Npgsql;
using System.Text.Json;

namespace ContratosPdfApi.Services
{
    public class ContratoService : IContratoService
    {
        private readonly string _connectionString;
        private readonly ITempFileService _tempFileService;

        private readonly ILogger<ContratoService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public ContratoService(IConfiguration configuration, ILogger<ContratoService> logger, ITempFileService tempFileService, IServiceProvider serviceProvider)
        {
            _tempFileService = tempFileService;
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;

            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public async Task<ContratoResponseDto> CrearContratoAsync(ContratoCreateDto contratoDto, string? sessionId = null)

        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                var tipoContrato = await connection.QuerySingleOrDefaultAsync<dynamic>(
                    "SELECT Id, Codigo FROM TiposContrato WHERE Codigo = @Codigo AND Activo = 1",
                    new { Codigo = contratoDto.TipoContrato },
                    transaction
                );

                if (tipoContrato == null)

                {
                    throw new ArgumentException($"Tipo de contrato no válido: {contratoDto.TipoContrato}");
                }

                // ✅ GENERAR NÚMERO DE CONTRATO
                var numeroContrato = await GenerarNumeroContratoAsync(connection, transaction, contratoDto.TipoContrato);

                // ✅ INSERTAR CONTRATO - SOLO CAMPOS QUE EXISTEN EN LA TABLA
                var contratoId = await connection.QuerySingleAsync<int>(@"
                    INSERT INTO Contratos (
                        TipoContratoId, NumeroContrato, NombreContratista, RucContratista, 
                        MontoContrato, FechaFirmaContrato, Estado, UsuarioCreadorId, FechaCreacion
                    ) OUTPUT INSERTED.Id VALUES (
                        @TipoContratoId, @NumeroContrato, @NombreContratista, @RucContratista,
                        @MontoContrato, @FechaFirmaContrato, 'ACTIVO', @UsuarioCreadorId, @FechaCreacion
                    )",
                    new
                    {
                        TipoContratoId = (int)tipoContrato.Id,
                        NumeroContrato = numeroContrato,
                        NombreContratista = contratoDto.RazonSocialContratista,
                        RucContratista = contratoDto.RucContratista,
                        MontoContrato = contratoDto.MontoTotal,
                        FechaFirmaContrato = contratoDto.FechaInicio,
                        UsuarioCreadorId = contratoDto.UsuarioId ?? 1,
                        FechaCreacion = DateTime.UtcNow
                    },
                    transaction
                );

                // ✅ INSERTAR DATOS ESPECÍFICOS CON TODOS LOS CAMPOS ADICIONALES
                var datosEspecificos = new
                {
                    // Datos originales
                    ObjetoContrato = contratoDto.ObjetoContrato,
                    FechaFin = contratoDto.FechaFin,

                    // Datos adicionales que no están en la tabla principal
                    RepresentanteContratante = contratoDto.RepresentanteContratante ?? "",
                    CargoRepresentante = contratoDto.CargoRepresentante ?? "",
                    RepresentanteContratista = contratoDto.RepresentanteContratista ?? "",
                    CedulaRepresentanteContratista = contratoDto.CedulaRepresentanteContratista ?? "",
                    DireccionContratista = contratoDto.DireccionContratista ?? "",
                    TelefonoContratista = contratoDto.TelefonoContratista ?? "",
                    EmailContratista = contratoDto.EmailContratista ?? "",

                    // Combinar con datos específicos existentes
                    DatosAdicionales = contratoDto.DatosEspecificos
                };

                await connection.ExecuteAsync(@"
                    INSERT INTO ContratoDetalles (ContratoId, DatosEspecificos, FechaCreacion)
                    VALUES (@ContratoId, @DatosEspecificos, @FechaCreacion)",
                    new
                    {
                        ContratoId = contratoId,
                        DatosEspecificos = JsonSerializer.Serialize(datosEspecificos),
                        FechaCreacion = DateTime.UtcNow
                    },
                    transaction
                );

                // ✅ COMMIT PARA QUE EL CONTRATO EXISTA ANTES DE ASOCIAR ARCHIVOS
                transaction.Commit();

                // ✅ ASOCIAR ARCHIVOS TEMPORALES SI HAY SESIÓN
                if (!string.IsNullOrEmpty(sessionId))
                {
                    using var scope = _serviceProvider.CreateScope();
                    var tempFileService = scope.ServiceProvider.GetRequiredService<ITempFileService>();
                    await tempFileService.AsociarArchivosTemporalesAContratoAsync(sessionId, contratoId);
                }

                // ✅ OBTENER CONTRATO CREADO
                var contratoCreado = await ObtenerContratoPorIdAsync(contratoId);

                _logger.LogInformation($"Contrato creado exitosamente: ID {contratoId}, Número {numeroContrato}");

                return contratoCreado ?? throw new InvalidOperationException("Error al recuperar el contrato creado");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, "Error al crear contrato");

                throw;
            }
        }

        private async Task<string> GenerarNumeroContratoAsync(SqlConnection connection, SqlTransaction transaction, string tipoContrato)
        {
            try
            {
                var anioActual = DateTime.Now.Year;

                var ultimoNumero = await connection.QuerySingleOrDefaultAsync<int?>(
                    @"SELECT MAX(CAST(RIGHT(NumeroContrato, 4) AS INT)) 
                        FROM Contratos c
                        INNER JOIN TiposContrato tc ON c.TipoContratoId = tc.Id
                        WHERE tc.Codigo = @TipoContrato 
                        AND YEAR(c.FechaCreacion) = @Anio
                        AND NumeroContrato LIKE @Patron",
                    new
                    {
                        TipoContrato = tipoContrato,
                        Anio = anioActual,
                        Patron = $"{tipoContrato}-{anioActual}-%"
                    },
                    transaction
                );

                var siguienteNumero = (ultimoNumero ?? 0) + 1;
                return $"{tipoContrato}-{anioActual}-{siguienteNumero:D4}";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error generando número de contrato, usando formato alternativo");
                return $"{tipoContrato}-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
            }
        }

        public async Task<ContratoResponseDto?> ObtenerContratoPorIdAsync(int id)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);

                // Consulta directa en lugar del SP que puede no existir correctamente
                var query = @"
                    SELECT 
                        c.*,
                        tc.Codigo as TipoContratoCodigo,
                        tc.Nombre as TipoContratoNombre,
                        u.NombreCompleto as UsuarioCreadorNombre
                    FROM Contratos c
                    INNER JOIN TiposContrato tc ON c.TipoContratoId = tc.Id
                    LEFT JOIN Usuarios u ON c.UsuarioCreadorId = u.Id
                    WHERE c.Id = @Id";

                var contrato = await connection.QuerySingleOrDefaultAsync<dynamic>(query, new { Id = id });
                if (contrato == null) return null;

                // Obtener archivos asociados
                var archivos = await connection.QueryAsync<ArchivoResponseDto>(@"
                    SELECT a.* FROM Archivos a
                    INNER JOIN ContratoArchivos ca ON a.Id = ca.ArchivoId
                    WHERE ca.ContratoId = @ContratoId",
                    new { ContratoId = id });

                // Obtener datos específicos
                var datosEspecificosJson = await connection.QuerySingleOrDefaultAsync<string>(
                    "SELECT DatosEspecificos FROM ContratoDetalles WHERE ContratoId = @ContratoId",
                    new { ContratoId = id });

                // Parsear datos específicos para extraer campos adicionales
                object? datosEspecificos = null;
                string representanteContratante = "";
                string cargoRepresentante = "";
                string representanteContratista = "";
                string cedulaRepresentanteContratista = "";
                string direccionContratista = "";
                string telefonoContratista = "";
                string emailContratista = "";

                if (!string.IsNullOrEmpty(datosEspecificosJson))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(datosEspecificosJson);
                        var root = doc.RootElement;

                        representanteContratante = root.TryGetProperty("RepresentanteContratante", out var rep1) ? rep1.GetString() ?? "" : "";
                        cargoRepresentante = root.TryGetProperty("CargoRepresentante", out var cargo) ? cargo.GetString() ?? "" : "";
                        representanteContratista = root.TryGetProperty("RepresentanteContratista", out var rep2) ? rep2.GetString() ?? "" : "";
                        cedulaRepresentanteContratista = root.TryGetProperty("CedulaRepresentanteContratista", out var cedula) ? cedula.GetString() ?? "" : "";
                        direccionContratista = root.TryGetProperty("DireccionContratista", out var dir) ? dir.GetString() ?? "" : "";
                        telefonoContratista = root.TryGetProperty("TelefonoContratista", out var tel) ? tel.GetString() ?? "" : "";
                        emailContratista = root.TryGetProperty("EmailContratista", out var email) ? email.GetString() ?? "" : "";

                        datosEspecificos = JsonSerializer.Deserialize<object>(datosEspecificosJson);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error parseando datos específicos del contrato {ContratoId}", id);
                    }
                }

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
                    UsuarioCreadorId = contrato.UsuarioCreadorId,

                    // Datos adicionales extraídos de DatosEspecificos
                    RepresentanteContratante = representanteContratante,
                    CargoRepresentante = cargoRepresentante,
                    RepresentanteContratista = representanteContratista,
                    CedulaRepresentanteContratista = cedulaRepresentanteContratista,
                    DireccionContratista = direccionContratista,
                    TelefonoContratista = telefonoContratista,
                    EmailContratista = emailContratista,

                    Archivos = archivos.ToList(),
                    DatosEspecificos = datosEspecificos
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al obtener contrato por ID: {id}");
                throw;

            }
        }

        public async Task<List<ContratoResponseDto>> ListarContratosAsync(string? tipoContrato = null, string? estado = null, int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);

                var whereClause = "WHERE 1=1";
                var parameters = new DynamicParameters();

                if (!string.IsNullOrEmpty(tipoContrato))
                {
                    whereClause += " AND tc.Codigo = @TipoContrato";
                    parameters.Add("TipoContrato", tipoContrato);
                }

                if (!string.IsNullOrEmpty(estado))
                {
                    whereClause += " AND c.Estado = @Estado";
                    parameters.Add("Estado", estado);
                }

                var offset = (pageNumber - 1) * pageSize;
                parameters.Add("Offset", offset);
                parameters.Add("PageSize", pageSize);

                var query = $@"
                    SELECT 
                        c.*,
                        tc.Codigo as TipoContratoCodigo,
                        tc.Nombre as TipoContratoNombre,
                        u.NombreCompleto as UsuarioCreadorNombre
                    FROM Contratos c
                    INNER JOIN TiposContrato tc ON c.TipoContratoId = tc.Id
                    LEFT JOIN Usuarios u ON c.UsuarioCreadorId = u.Id
                    {whereClause}
                    ORDER BY c.FechaCreacion DESC
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                var contratos = await connection.QueryAsync<dynamic>(query, parameters);

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
                    UsuarioCreadorNombre = c.UsuarioCreadorNombre,
                    UsuarioCreadorId = c.UsuarioCreadorId,
                    Archivos = new List<ArchivoResponseDto>()
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar contratos");
                throw;
            }
        }

        public async Task ActualizarPdfContratoAsync(int contratoId, int archivoPdfId)
        {
            using var connection = new SqlConnection(_connectionString);

            // Actualizar el campo ArchivoPdfGeneradoId en la tabla Contratos
            await connection.ExecuteAsync(@"
                UPDATE Contratos 
                SET ArchivoPdfGeneradoId = @ArchivoPdfId, FechaActualizacion = @FechaActualizacion
                WHERE Id = @ContratoId",
                new
                {
                    ContratoId = contratoId,
                    ArchivoPdfId = archivoPdfId,
                    FechaActualizacion = DateTime.UtcNow
                }
            );

            // También asociar en ContratoArchivos
            await connection.ExecuteAsync(@"
                IF NOT EXISTS (SELECT 1 FROM ContratoArchivos WHERE ContratoId = @ContratoId AND ArchivoId = @ArchivoId)
                BEGIN
                    INSERT INTO ContratoArchivos (ContratoId, ArchivoId, FechaAsociacion)
                    VALUES (@ContratoId, @ArchivoId, @FechaAsociacion)
                END",
                new
                {
                    ContratoId = contratoId,
                    ArchivoId = archivoPdfId,
                    FechaAsociacion = DateTime.UtcNow
                }
            );

            _logger.LogInformation($"PDF actualizado para contrato {contratoId}: archivo {archivoPdfId}");

        }
        // BUSCAR el método ActualizarPdfContratoAsync y REEMPLAZARLO:

        private async Task ActualizarPdfContratoAsync(int contratoId, int archivoPdfId)
        {
            using var connection = new SqlConnection(_connectionString);

            await connection.ExecuteAsync(@"
                IF NOT EXISTS (SELECT 1 FROM ContratoArchivos WHERE ContratoId = @ContratoId AND ArchivoId = @ArchivoId)
                BEGIN
                    INSERT INTO ContratoArchivos (ContratoId, ArchivoId, FechaAsociacion)
                    VALUES (@ContratoId, @ArchivoId, @FechaAsociacion)
                END",
                new
                {
                    ContratoId = contratoId,
                    ArchivoId = archivoId,
                    FechaAsociacion = DateTime.UtcNow
                }
            );

        }



        public async Task<List<dynamic>> ObtenerTiposContratoAsync()
        {
            //using var connection = new SqlConnection(_connectionString);
            using var connection = _dbHelper.CreateConnection();

            var tipos = await connection.QueryAsync<dynamic>(
                "SELECT * FROM TiposContrato WHERE Activo = 1 ORDER BY Nombre"
            );
            return tipos.ToList();
        }


        public (string nombreContratante, string cargoContratante, string nombreContratista, string cargoContratista)
    ObtenerDatosFirmas(dynamic contratoData)
        {
            try
            {
                // Datos del contratante (siempre Gerente General por defecto)
                var nombreContratante = "Diego Fernando Zárate Valdivieso";
                var cargoContratante = "Gerente General";

                // Datos del contratista (dinámicos)
                var nombreContratista = contratoData?.representanteContratista?.ToString() ?? "[NOMBRE_CONTRATISTA]";
                var cargoContratista = contratoData?.tipoRepresentanteContratista?.ToString() switch
                {
                    "persona_natural" => "Contratista",
                    "persona_juridica" => "Representante Legal",
                    _ => "[CARGO_REPRESENTANTE]"
                };

                return (nombreContratante, cargoContratante, nombreContratista, cargoContratista);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error obteniendo datos de firmas, usando valores por defecto");
                return ("Diego Fernando Zárate Valdivieso", "Gerente General", "[NOMBRE_CONTRATISTA]", "[CARGO_REPRESENTANTE]");
            }

        }
    }
}
