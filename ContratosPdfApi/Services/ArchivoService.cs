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
             IDatabaseHelper dbHelper,
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

                // Determinar carpeta según tipo de archivo
                var carpetaDestino = DeterminarCarpetaPorTipoArchivo(archivoDto.TipoArchivo);
                var rutaCompleta = Path.Combine(_environment.WebRootPath, carpetaDestino, nombreArchivo);

                // Crear directorio si no existe
                var directorio = Path.GetDirectoryName(rutaCompleta);
                if (!Directory.Exists(directorio))
                    Directory.CreateDirectory(directorio!);

                // Calcular hash SHA256 del archivo
                var hashCalculado = await CalcularHashSHA256Async(archivo);

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

                if (_dbHelper.IsPostgreSQL)
                {
                    // PostgreSQL: Ejecutar función directamente
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
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(hashBytes)[..8].ToLowerInvariant();
        }

        private string DeterminarCarpetaPorTipoArchivo(string tipoArchivo)
        {
            var baseDirectory = "Uploads";

            return tipoArchivo.ToUpper() switch
            {
                "PDF_GENERADO" => Path.Combine(baseDirectory, "Contratos/Bienes/PDFs"),
                "PDF_FIRMADO" => Path.Combine(baseDirectory, "Contratos/Bienes/PDFs"),
                "TABLA_CANTIDADES" => Path.Combine(baseDirectory, "Contratos/Bienes/TablaCantidades"),
                "RESPALDO_CONTRATANTE" => Path.Combine(baseDirectory, "Contratos/Bienes/Respaldos"),
                _ => Path.Combine(baseDirectory, "Contratos/Bienes/Otros")
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