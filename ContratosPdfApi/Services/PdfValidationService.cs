using ContratosPdfApi.Models.DTOs;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Security.Cryptography;

namespace ContratosPdfApi.Services
{
    public class PdfValidationService : IPdfValidationService
    {
        private readonly string _connectionString;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<PdfValidationService> _logger;

        public PdfValidationService(
            IConfiguration configuration,
            IWebHostEnvironment environment,
            ILogger<PdfValidationService> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
            _environment = environment;
            _logger = logger;
        }

        public async Task<ValidacionIntegridadResult> ValidarIntegridadPdfAsync(IFormFile archivoPdf)
        {
            try
            {
                _logger.LogInformation($"Validando integridad del PDF: {archivoPdf.FileName}");

                // 1. Validaciones básicas
                if (archivoPdf.ContentType != "application/pdf")
                {
                    return new ValidacionIntegridadResult
                    {
                        EsValido = false,
                        Razon = "El archivo no es un PDF válido"
                    };
                }

                // 2. Calcular hash del archivo cargado
                var hashCargado = await CalcularHashAsync(archivoPdf);

                // 3. Buscar PDFs originales similares
                var posiblesOriginales = await BuscarPdfOriginalesAsync(archivoPdf);

                if (!posiblesOriginales.Any())
                {
                    return new ValidacionIntegridadResult
                    {
                        EsValido = false,
                        Razon = "No se encontró PDF original para comparar. ¿Generó el PDF primero?",
                        HashCalculado = hashCargado
                    };
                }

                // 4. Comparar con cada posible original
                foreach (var original in posiblesOriginales)
                {
                    var comparacion = await CompararPdfsAsync(archivoPdf, original);

                    if (comparacion.SonCompatibles)
                    {
                        var validacionFirmas = await ValidarFiremasDigitalesAsync(archivoPdf);

                        return new ValidacionIntegridadResult
                        {
                            EsValido = true,
                            PdfOriginalId = original.Id,
                            TieneFiremasDigitales = validacionFirmas.TieneFirmasValidas,
                            FirmantesDetectados = validacionFirmas.Firmas.Select(f => f.NombreFirmante).ToList(),
                            HashCalculado = hashCargado
                        };
                    }
                }

                return new ValidacionIntegridadResult
                {
                    EsValido = false,
                    Razon = "El PDF ha sido modificado o no coincide con ningún original",
                    HashCalculado = hashCargado
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validando integridad del PDF");
                return new ValidacionIntegridadResult
                {
                    EsValido = false,
                    Razon = $"Error interno: {ex.Message}"
                };
            }
        }

        public async Task<List<ArchivoResponseDto>> BuscarPdfOriginalesAsync(IFormFile archivoPdf)
        {
            try
            {
                var nombreSinExtension = Path.GetFileNameWithoutExtension(archivoPdf.FileName);
                var tamañoApproximado = archivoPdf.Length;

                using var connection = new SqlConnection(_connectionString);
                
                // Buscar PDFs similares por nombre, tamaño y tipo
                var query = @"
                    SELECT TOP 10 * FROM Archivos 
                    WHERE TipoArchivo IN ('PDF_GENERADO', 'PDF_ORIGINAL')
                    AND (
                        NombreOriginal LIKE @NombreBusqueda 
                        OR Tamaño BETWEEN @TamañoMin AND @TamañoMax
                    )
                    ORDER BY FechaSubida DESC";

                var archivos = await connection.QueryAsync<ArchivoResponseDto>(query, new
                {
                    NombreBusqueda = $"%{nombreSinExtension}%",
                    TamañoMin = tamañoApproximado * 0.8, // ±20% tolerancia
                    TamañoMax = tamañoApproximado * 1.2
                });

                return archivos.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error buscando PDFs originales");
                return new List<ArchivoResponseDto>();
            }
        }

        public async Task<ComparacionPdfResult> CompararPdfsAsync(IFormFile pdfCargado, ArchivoResponseDto pdfOriginal)
        {
            try
            {
                // 1. Comparar hashes primero (más rápido)
                var hashCargado = await CalcularHashAsync(pdfCargado);
                
                if (!string.IsNullOrEmpty(pdfOriginal.HashSHA256) && 
                    hashCargado.Equals(pdfOriginal.HashSHA256, StringComparison.OrdinalIgnoreCase))
                {
                    return new ComparacionPdfResult
                    {
                        SonCompatibles = true,
                        TieneFiremasDigitales = false // Es idéntico, no tiene firmas
                    };
                }

                // 2. Si los hashes son diferentes, verificar si es por firmas digitales
                var validacionFirmas = await ValidarFiremasDigitalesAsync(pdfCargado);
                
                if (validacionFirmas.TieneFirmasValidas)
                {
                    // TODO: Aquí implementar comparación byte por byte del contenido SIN las firmas
                    // Por ahora, asumir que si tiene firmas válidas, es compatible
                    return new ComparacionPdfResult
                    {
                        SonCompatibles = true,
                        TieneFiremasDigitales = true,
                        Firmantes = validacionFirmas.Firmas.Select(f => f.NombreFirmante).ToList()
                    };
                }

                // 3. Si no tiene firmas y los hashes son diferentes, ha sido modificado
                return new ComparacionPdfResult
                {
                    SonCompatibles = false,
                    DiferenciasDetectadas = "El contenido del PDF ha sido modificado"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error comparando PDFs");
                return new ComparacionPdfResult
                {
                    SonCompatibles = false,
                    DiferenciasDetectadas = $"Error en comparación: {ex.Message}"
                };
            }
        }

        public async Task<ValidacionFiremaResult> ValidarFiremasDigitalesAsync(IFormFile archivoPdf)
        {
            try
            {
                // TODO: Implementar con iText7 cuando esté listo
                // Por ahora, simulación básica

                using var stream = archivoPdf.OpenReadStream();
                var buffer = new byte[1024];
                await stream.ReadAsync(buffer, 0, buffer.Length);
                
                // Buscar indicadores básicos de firma digital en el PDF
                var content = System.Text.Encoding.Latin1.GetString(buffer);
                var tieneFirma = content.Contains("/ByteRange") || 
                                content.Contains("/Contents") || 
                                content.Contains("/Type/Sig");

                if (tieneFirma)
                {
                    return new ValidacionFiremaResult
                    {
                        TieneFirmasValidas = true,
                        Firmas = new List<FirmaDigitalInfo>
                        {
                            new FirmaDigitalInfo
                            {
                                NombreFirmante = "Firmante Detectado",
                                FechaFirma = DateTime.Now,
                                EsValida = true,
                                Razon = "Firma digital detectada (validación básica)"
                            }
                        }
                    };
                }

                return new ValidacionFiremaResult
                {
                    TieneFirmasValidas = false,
                    MensajeError = "No se detectaron firmas digitales"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validando firmas digitales");
                return new ValidacionFiremaResult
                {
                    TieneFirmasValidas = false,
                    MensajeError = $"Error validando firmas: {ex.Message}"
                };
            }
        }

        private async Task<string> CalcularHashAsync(IFormFile archivo)
        {
            using var sha256 = SHA256.Create();
            using var stream = archivo.OpenReadStream();
            var hashBytes = await sha256.ComputeHashAsync(stream);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }
}