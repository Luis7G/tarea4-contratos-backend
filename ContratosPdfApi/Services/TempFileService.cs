using System.Collections.Concurrent;
using ContratosPdfApi.Models.DTOs;
using System.Security.Cryptography;
using Dapper;
using Microsoft.Data.SqlClient;

namespace ContratosPdfApi.Services
{
    public class TempFileService : ITempFileService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly IArchivoAdjuntoService _archivoAdjuntoService;
        private readonly ILogger<TempFileService> _logger;
        private readonly string _connectionString; // ✅ AGREGAR ESTO

        // Cache en memoria para archivos temporales por sesión
        private static readonly ConcurrentDictionary<string, List<TempFileDto>> _tempFiles = new();

        public TempFileService(
            IWebHostEnvironment environment,
            IArchivoAdjuntoService archivoAdjuntoService,
            ILogger<TempFileService> logger,
            IConfiguration configuration) // ✅ AGREGAR ESTO
        {
            _environment = environment;
            _archivoAdjuntoService = archivoAdjuntoService;
            _logger = logger;
            _connectionString = configuration.GetConnectionString("DefaultConnection")!; // ✅ AGREGAR ESTO
        }

        public async Task<TempFileDto> SubirArchivoTemporalAsync(IFormFile archivo, string tipoArchivoCodigo, string sessionId, int? usuarioId)
        {
            try
            {
                var tempFolder = Path.Combine(_environment.WebRootPath, "storage", "temp", "sessions", sessionId);
                Directory.CreateDirectory(tempFolder);

                var tempFile = new TempFileDto
                {
                    SessionId = sessionId,
                    NombreOriginal = archivo.FileName,
                    TipoArchivoCodigo = tipoArchivoCodigo,
                    TipoMIME = archivo.ContentType,
                    Tamaño = archivo.Length,
                    UsuarioId = usuarioId,
                    FechaSubida = DateTime.UtcNow
                };

                // ✅ GENERAR NOMBRE ÚNICO TEMPORAL
                var fileName = $"temp_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..8]}{Path.GetExtension(archivo.FileName)}";
                tempFile.RutaTemporal = Path.Combine(tempFolder, fileName);

                using (var stream = new FileStream(tempFile.RutaTemporal, FileMode.Create))
                {
                    await archivo.CopyToAsync(stream);
                }

                // Agregar a cache en memoria
                _tempFiles.AddOrUpdate(
                    sessionId,
                    new List<TempFileDto> { tempFile },
                    (key, list) => { list.Add(tempFile); return list; }
                );

                _logger.LogInformation($"Archivo temporal subido: {archivo.FileName} para sesión {sessionId}");
                return tempFile;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al subir archivo temporal: {archivo.FileName}");
                throw;
            }
        }

        public async Task<List<TempFileDto>> ObtenerArchivosTemporalesAsync(string sessionId)
        {
            return _tempFiles.TryGetValue(sessionId, out var archivos) ? archivos : new List<TempFileDto>();
        }

        public async Task AsociarArchivosTemporalesAContratoAsync(string sessionId, int contratoId)
        {
            if (!_tempFiles.TryGetValue(sessionId, out var archivos) || !archivos.Any())
            {
                _logger.LogInformation($"No hay archivos temporales para sesión {sessionId}");
                return;
            }

            _logger.LogInformation($"Asociando {archivos.Count} archivos temporales al contrato {contratoId}");

            var carpetaPermanente = Path.Combine(
                _environment.WebRootPath,
                "storage",
                "contratos",
                "bienes",
                "adjuntos",
                $"contrato_{contratoId}"
            );

            if (!Directory.Exists(carpetaPermanente))
            {
                Directory.CreateDirectory(carpetaPermanente);
                _logger.LogInformation($"Carpeta creada: {carpetaPermanente}");
            }

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            foreach (var tempFile in archivos)
            {
                try
                {
                    // ✅ OBTENER ID DEL TIPO DE ARCHIVO ADJUNTO CORRECTO
                    var tipoArchivoAdjunto = await connection.QuerySingleOrDefaultAsync<dynamic>(
                        "SELECT Id, EsObligatorio FROM TiposArchivosAdjuntos WHERE Codigo = @Codigo AND Activo = 1",
                        new { Codigo = tempFile.TipoArchivoCodigo }
                    );

                    if (tipoArchivoAdjunto == null)
                    {
                        _logger.LogWarning($"Tipo de archivo adjunto no encontrado: {tempFile.TipoArchivoCodigo}");
                        continue;
                    }

                    // ✅ GENERAR NOMBRE PERMANENTE
                    var extension = Path.GetExtension(tempFile.NombreOriginal);
                    var nombrePermanente = $"{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..8]}{extension}";
                    var rutaPermanente = Path.Combine(carpetaPermanente, nombrePermanente);

                    // ✅ VERIFICAR Y MOVER ARCHIVO
                    if (!File.Exists(tempFile.RutaTemporal))
                    {
                        _logger.LogWarning($"Archivo temporal no encontrado: {tempFile.RutaTemporal}");
                        continue;
                    }

                    File.Move(tempFile.RutaTemporal, rutaPermanente);
                    _logger.LogInformation($"Archivo movido: {tempFile.NombreOriginal} -> {rutaPermanente}");

                    // ✅ CALCULAR HASH
                    string hashSHA256;
                    using (var fileStream = File.OpenRead(rutaPermanente))
                    using (var sha256 = SHA256.Create())
                    {
                        var hashBytes = sha256.ComputeHash(fileStream);
                        hashSHA256 = Convert.ToHexString(hashBytes).ToLower();
                    }

                    // ✅ GUARDAR PRIMERO EN TABLA Archivos, LUEGO EN ContratoArchivosAdjuntos
                    var archivoId = await connection.QuerySingleAsync<int>(@"
    INSERT INTO Archivos (
        NombreOriginal, NombreArchivo, RutaArchivo, TipoMIME, 
        Tamaño, TipoArchivo, HashSHA256, UsuarioId, FechaSubida
    ) OUTPUT INSERTED.Id VALUES (
        @NombreOriginal, @NombreArchivo, @RutaArchivo, @TipoMIME,
        @Tamaño, @TipoArchivo, @HashSHA256, @UsuarioId, @FechaSubida
    )",
                        new
                        {
                            NombreOriginal = tempFile.NombreOriginal,
                            NombreArchivo = nombrePermanente,
                            RutaArchivo = $"storage/contratos/bienes/adjuntos/contrato_{contratoId}/{nombrePermanente}",
                            TipoMIME = tempFile.TipoMIME,
                            Tamaño = new FileInfo(rutaPermanente).Length,
                            TipoArchivo = "ADJUNTO_CONTRATO",
                            HashSHA256 = hashSHA256,
                            UsuarioId = tempFile.UsuarioId ?? 1,
                            FechaSubida = DateTime.UtcNow
                        }
                    );

                    // ✅ AHORA SÍ INSERTAR EN ContratoArchivosAdjuntos CON ArchivoId
                    await connection.ExecuteAsync(@"
    INSERT INTO ContratoArchivosAdjuntos (
        ContratoId, TipoArchivoAdjuntoId, ArchivoId, NombreOriginal, NombreArchivo, 
        RutaArchivo, TipoMIME, Tamaño, EsObligatorio, FechaSubida
    ) VALUES (
        @ContratoId, @TipoArchivoAdjuntoId, @ArchivoId, @NombreOriginal, @NombreArchivo,
        @RutaArchivo, @TipoMIME, @Tamaño, @EsObligatorio, @FechaSubida
    )",
                        new
                        {
                            ContratoId = contratoId,
                            TipoArchivoAdjuntoId = (int)tipoArchivoAdjunto.Id,
                            ArchivoId = archivoId, // ✅ AGREGAR ESTO
                            NombreOriginal = tempFile.NombreOriginal,
                            NombreArchivo = nombrePermanente,
                            RutaArchivo = $"storage/contratos/bienes/adjuntos/contrato_{contratoId}/{nombrePermanente}",
                            TipoMIME = tempFile.TipoMIME,
                            Tamaño = new FileInfo(rutaPermanente).Length,
                            EsObligatorio = (bool)tipoArchivoAdjunto.EsObligatorio,
                            FechaSubida = DateTime.UtcNow
                        }
                    );

                    _logger.LogInformation($"Archivo {tempFile.NombreOriginal} guardado en ContratoArchivosAdjuntos con tipo {tempFile.TipoArchivoCodigo}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error al asociar archivo temporal {tempFile.NombreOriginal} al contrato {contratoId}");
                }
            }

            // ✅ LIMPIAR ARCHIVOS TEMPORALES
            await LimpiarArchivosTemporalesAsync(sessionId);
            _logger.LogInformation($"Archivos temporales de sesión {sessionId} limpiados exitosamente");
        }

        public async Task LimpiarArchivosTemporalesAsync(string sessionId)
        {
            if (_tempFiles.TryRemove(sessionId, out var archivos))
            {
                foreach (var archivo in archivos)
                {
                    try
                    {
                        if (File.Exists(archivo.RutaTemporal))
                        {
                            File.Delete(archivo.RutaTemporal);
                            _logger.LogDebug($"Archivo temporal eliminado: {archivo.RutaTemporal}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"No se pudo eliminar archivo temporal: {archivo.RutaTemporal}");
                    }
                }
                _logger.LogInformation($"Archivos temporales limpiados para sesión {sessionId}");
            }
        }

        public void LimpiarArchivosExpirados()
        {
            var ahora = DateTime.UtcNow;
            var tiempoExpiracion = TimeSpan.FromHours(2); // 2 horas de expiración

            var sessionesExpiradas = _tempFiles
                .Where(kvp => kvp.Value.Any(archivo => ahora - archivo.FechaSubida > tiempoExpiracion))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var sessionId in sessionesExpiradas)
            {
                _logger.LogInformation($"Limpiando archivos expirados para sesión: {sessionId}");
                _ = Task.Run(() => LimpiarArchivosTemporalesAsync(sessionId));
            }
        }
    }
}