using ContratosPdfApi.Models.DTOs;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Security.Cryptography;
using Npgsql;

namespace ContratosPdfApi.Services
{
    public class ArchivoService : IArchivoService
    {
        // private readonly string _connectionString;
        private readonly IDatabaseHelper _dbHelper;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ArchivoService> _logger;

        public ArchivoService(
            IConfiguration configuration,
            IWebHostEnvironment environment,
            ILogger<ArchivoService> logger)
        {
            // _connectionString = configuration.GetConnectionString("DefaultConnection")!;
            _dbHelper = dbHelper;
            _environment = environment;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<ArchivoResponseDto> SubirArchivoAsync(IFormFile archivo, ArchivoUploadDto archivoDto)
        {
            try
            {
                // Validaciones (igual que antes)
                if (archivo == null || archivo.Length == 0)
                    throw new ArgumentException("El archivo no puede estar vacío");

                // Validar tipo de archivo
                var extensionesPermitidas = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".xlsx", ".docx" };
                var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();

                if (!extensionesPermitidas.Contains(extension))
                    throw new ArgumentException($"Tipo de archivo no permitido: {extension}");

                // Validar tamaño (10MB máximo)
                var tamañoMaximo = _configuration.GetValue<long>("FileStorage:MaxFileSize", 10485760);
                if (archivo.Length > tamañoMaximo)
                    throw new ArgumentException($"El archivo excede el tamaño máximo permitido ({tamañoMaximo / 1024 / 1024}MB)");

                // Generar nombre único para el archivo
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var hashCorto = GenerarHashCorto(archivo.FileName + DateTime.Now.Ticks);
                var extension_limpia = Path.GetExtension(archivo.FileName);
                var nombreArchivo = $"{timestamp}_{hashCorto}{extension_limpia}";


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


                // Guardar archivo físico
                using (var stream = new FileStream(rutaCompleta, FileMode.Create))
                {
                    await archivo.CopyToAsync(stream);
                }

                _logger.LogInformation($"Archivo guardado físicamente: {rutaCompleta}");

                // ═══════════════════════════════════════════════════════════
                // GUARDAR EN BASE DE DATOS - COMPATIBLE CON AMBOS PROVEEDORES
                // ═══════════════════════════════════════════════════════════

                using var connection = _dbHelper.CreateConnection();
                var insertSql = _dbHelper.GetInsertArchiveSql();

                int archivoId;

                // REEMPLAZAR el bloque de inserción del archivo:

                if (_dbHelper.IsPostgreSQL)
                {
                    // PostgreSQL: Ejecutar query directo
                    archivoId = await connection.QuerySingleAsync<int>(insertSql, new
                    {
                        NombreOriginal = archivo.FileName,
                        NombreArchivo = nombreArchivo,
                        RutaArchivo = Path.Combine(carpetaDestino, nombreArchivo).Replace("\\", "/"),
                        TipoMIME = archivo.ContentType ?? "application/octet-stream",
                        Tamaño = archivo.Length,
                        TipoArchivo = archivoDto.TipoArchivo,
                        HashSHA256 = hashCalculado,
                        UsuarioId = archivoDto.UsuarioId
                    });
                }
                else
                {
                    // SQL Server: Ejecutar stored procedure
                    archivoId = await connection.QuerySingleAsync<int>(insertSql, new
                    {
                        NombreOriginal = archivo.FileName,
                        NombreArchivo = nombreArchivo,
                        RutaArchivo = Path.Combine(carpetaDestino, nombreArchivo).Replace("\\", "/"),
                        TipoMIME = archivo.ContentType ?? "application/octet-stream",
                        Tamaño = archivo.Length,
                        TipoArchivo = archivoDto.TipoArchivo,
                        HashSHA256 = hashCalculado,
                        UsuarioId = archivoDto.UsuarioId
                    }, commandType: System.Data.CommandType.StoredProcedure);
                }

                _logger.LogInformation($"Archivo subido exitosamente: {archivo.FileName} -> {nombreArchivo}");

                return new ArchivoResponseDto
                {
                    Id = archivoId,
                    NombreOriginal = archivo.FileName,
                    NombreArchivo = nombreArchivo,
                    RutaArchivo = Path.Combine(carpetaDestino, nombreArchivo).Replace("\\", "/"),
                    TipoMIME = archivo.ContentType ?? "application/octet-stream",
                    Tamaño = archivo.Length,
                    TipoArchivo = archivoDto.TipoArchivo,
                    HashSHA256 = hashCalculado,
                    FechaSubida = DateTime.UtcNow,
                    UsuarioId = archivoDto.UsuarioId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al subir archivo");
                throw;
            }
        }

        // Métodos privados (igual que antes)
        private async Task<string> CalcularHashSHA256Async(IFormFile archivo)
        {
            using var sha256 = SHA256.Create();
            using var stream = archivo.OpenReadStream();
            var hashBytes = await sha256.ComputeHashAsync(stream);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        private string GenerarHashCorto(string input)
        {

            var carpetas = _configuration.GetSection("FileStorage:ContratosFolders").Get<Dictionary<string, string>>()
                ?? new Dictionary<string, string>();

            return carpetas.ContainsKey(tipoContrato.ToUpper())
                ? carpetas[tipoContrato.ToUpper()]
                : "Contratos/General";

        }

        private string DeterminarCarpetaPorTipoArchivo(string tipoArchivo)
        {

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

        // public async Task<ArchivoResponseDto?> ObtenerArchivoPorIdAsync(int id)
        // {
        //     using var connection = new SqlConnection(_connectionString);
        //     var archivo = await connection.QuerySingleOrDefaultAsync<ArchivoResponseDto>(
        //         "SP_ObtenerArchivoPorId",
        //         new { ArchivoId = id },
        //         commandType: System.Data.CommandType.StoredProcedure
        //     );

        //     return archivo;
        // }

        public async Task<ArchivoResponseDto?> ObtenerArchivoPorIdAsync(int id)
        {
            using var connection = _dbHelper.CreateConnection();

            // Query SQL estándar que funciona en ambas BD
            var archivo = await connection.QuerySingleOrDefaultAsync<ArchivoResponseDto>(
                @"SELECT Id, NombreOriginal, NombreArchivo, RutaArchivo, TipoMIME, 
                 Tamaño, TipoArchivo, FechaSubida, HashSHA256 
          FROM Archivos 
          WHERE Id = @Id",
                new { Id = id }
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
    }
}