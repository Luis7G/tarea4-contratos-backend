using ContratosPdfApi.Models.DTOs;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Security.Cryptography;

namespace ContratosPdfApi.Services
{
    public class ArchivoService : IArchivoService
    {
        private readonly string _connectionString;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ArchivoService> _logger;

        public ArchivoService(
            IConfiguration configuration,
            IWebHostEnvironment environment,
            ILogger<ArchivoService> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
            _environment = environment;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<ArchivoResponseDto> SubirArchivoAsync(IFormFile archivo, ArchivoUploadDto archivoDto)
        {
            try
            {
                // Validaciones
                if (archivo == null || archivo.Length == 0)
                    throw new ArgumentException("Archivo no válido");

                var extensionesPermitidas = _configuration.GetSection("FileStorage:AllowedExtensions").Get<string[]>() ?? new[] { ".pdf" };
                var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();

                if (!extensionesPermitidas.Contains(extension))
                    throw new ArgumentException($"Extensión no permitida. Permitidas: {string.Join(", ", extensionesPermitidas)}");

                var maxFileSize = _configuration.GetValue<long>("FileStorage:MaxFileSize", 10485760); // 10MB default
                if (archivo.Length > maxFileSize)
                    throw new ArgumentException($"Archivo demasiado grande. Máximo: {maxFileSize / 1024 / 1024}MB");

                // Generar nombre único y ruta
                var timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var uniqueId = Guid.NewGuid().ToString("N")[..8];
                var nombreArchivo = $"{timeStamp}_{uniqueId}{extension}";

                // Determinar carpeta según tipo de archivo
                var carpeta = DeterminarCarpetaPorTipoArchivo(archivoDto.TipoArchivo);
                var rutaCompleta = Path.Combine(_environment.WebRootPath, carpeta);

                // Crear directorio si no existe
                Directory.CreateDirectory(rutaCompleta);

                var rutaArchivo = Path.Combine(rutaCompleta, nombreArchivo);
                var rutaRelativa = Path.Combine(carpeta, nombreArchivo).Replace("\\", "/");

                // Guardar archivo físicamente
                using (var stream = new FileStream(rutaArchivo, FileMode.Create))
                {
                    await archivo.CopyToAsync(stream);
                }

                // Generar hash SHA256
                var hash = await GenerarHashSHA256Async(archivo);

                // Guardar en base de datos usando stored procedure
                using var connection = new SqlConnection(_connectionString);
                var archivoId = await connection.QuerySingleAsync<int>(
                    "SP_InsertarArchivo",
                    new
                    {
                        NombreOriginal = archivo.FileName,
                        NombreArchivo = nombreArchivo,
                        RutaArchivo = rutaRelativa,
                        TipoMIME = archivo.ContentType,
                        Tamaño = archivo.Length,
                        TipoArchivo = archivoDto.TipoArchivo,
                        HashSHA256 = hash,
                        UsuarioId = archivoDto.UsuarioId
                    },
                    commandType: System.Data.CommandType.StoredProcedure
                );

                _logger.LogInformation($"Archivo subido exitosamente: {archivo.FileName} -> {nombreArchivo}");

                return new ArchivoResponseDto
                {
                    Id = archivoId,
                    NombreOriginal = archivo.FileName,
                    NombreArchivo = nombreArchivo,
                    RutaArchivo = rutaRelativa,
                    TipoMIME = archivo.ContentType,
                    Tamaño = archivo.Length,
                    TipoArchivo = archivoDto.TipoArchivo,
                    FechaSubida = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al subir archivo: {archivo?.FileName}");
                throw;
            }
        }

        public async Task<ArchivoResponseDto?> ObtenerArchivoPorIdAsync(int id)
        {
            using var connection = new SqlConnection(_connectionString);
            var archivo = await connection.QuerySingleOrDefaultAsync<ArchivoResponseDto>(
                "SP_ObtenerArchivoPorId",
                new { ArchivoId = id },
                commandType: System.Data.CommandType.StoredProcedure
            );

            return archivo;
        }

        public async Task<string> GenerarHashSHA256Async(IFormFile archivo)
        {
            using var sha256 = SHA256.Create();
            using var stream = archivo.OpenReadStream();
            var hashBytes = await sha256.ComputeHashAsync(stream);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        public string ObtenerRutaCarpetaPorTipo(string tipoContrato)
        {
            var carpetas = _configuration.GetSection("FileStorage:ContratosFolders").Get<Dictionary<string, string>>()
                ?? new Dictionary<string, string>();

            return carpetas.ContainsKey(tipoContrato.ToUpper())
                ? carpetas[tipoContrato.ToUpper()]
                : "Contratos/General";
        }

        private string DeterminarCarpetaPorTipoArchivo(string tipoArchivo)
        {
            // ✅ USAR ESTRUCTURA UNIFICADA
            var baseDirectory = "storage";

            return tipoArchivo.ToUpper() switch
            {
                "PDF_GENERADO" => Path.Combine(baseDirectory, "contratos", "bienes", "pdfs"),
                "TABLA_CANTIDADES" => Path.Combine(baseDirectory, "contratos", "bienes", "respaldos"),
                "RESPALDO_CONTRATANTE" => Path.Combine(baseDirectory, "contratos", "bienes", "respaldos"),
                "ADJUNTO_CONTRATO" => Path.Combine(baseDirectory, "contratos", "bienes", "adjuntos"),
                _ => Path.Combine(baseDirectory, "contratos", "bienes", "adjuntos")
            };
        }
    }
}