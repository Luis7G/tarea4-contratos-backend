using DinkToPdf;
using DinkToPdf.Contracts;
using ContratosPdfApi.Models;
using System.IO;

namespace ContratosPdfApi.Services
{
    public class PdfService : IPdfService
    {
        private readonly IConverter _converter;
        private readonly ILogger<PdfService> _logger;
        private readonly IWebHostEnvironment _environment;

        public PdfService(IConverter converter, ILogger<PdfService> logger, IWebHostEnvironment environment)
        {
            _converter = converter;
            _logger = logger;
            _environment = environment;
        }

        // Método para generar el HTML del header que se repetirá
        private string GenerateHeaderHtml(string baseUrl)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ 
            margin: 0; 
            padding: 3px; 
            font-family: Arial, sans-serif; 
            background: white;
        }}
        
        .contract-header-table {{
            width: 100%;
            border-collapse: collapse;
            border: 2px solid black !important;
            font-size: 8pt;
            background: white;
            margin: 0;
        }}
        
        .contract-header-table td {{
            padding: 4px 6px;
            border: 1px solid black !important;
            vertical-align: middle;
            background: white;
            color: black;
        }}
        
        .header-cell-title {{
            text-align: center;
            font-weight: bold;
            font-size: 10pt;
            line-height: 1.1;
        }}
        
        .header-cell-logo {{
            text-align: center;
            width: 18%;
        }}
        
        .logo-img {{
            max-width: 50px;
            height: auto;
            display: block;
            margin: 0 auto;
        }}
        
        .header-info {{
            font-size: 7pt;
        }}
    </style>
</head>
<body>
    <table class='contract-header-table'>
        <tr>
            <td rowspan='3' class='header-cell-logo'>
                <img src='{baseUrl}/assets/logo-hidalgo.png' alt='Logo' class='logo-img'>
            </td>
            <td rowspan='3' class='header-cell-title'>
                CONTRATO PARA LA ADQUISICIÓN DE BIENES
            </td>
            <td class='header-info' style='width: 17.5%;'>
                <strong>TIPO:</strong> Documento
            </td>
            <td class='header-info' style='width: 17.5%;'>
                <strong>VERSIÓN:</strong> 2
            </td>
        </tr>
        <tr>
            <td class='header-info'>
                <strong>PROCESO:</strong> GESTIÓN EJECUCIÓN Y CONTROL
            </td>
            <td class='header-info'>
                <strong>VIGENCIA:</strong> 01-abril-2025
            </td>
        </tr>
        <tr>
            <td class='header-info'>
                <strong>CÓDIGO:</strong> ECO-D-08
            </td>
            <td class='header-info'>
                <!-- Espacio adicional -->
            </td>
        </tr>
    </table>
</body>
</html>";
        }

        // Método para guardar el header HTML en un archivo temporal
        private string SaveHeaderToTempFile(string headerHtml)
        {
            try
            {
                // ✅ USAR CARPETA UNIFICADA
                var tempPath = Path.Combine(_environment.WebRootPath, "storage", "temp", "images");
                if (!Directory.Exists(tempPath))
                {
                    Directory.CreateDirectory(tempPath);
                }

                var fileName = $"header_{Guid.NewGuid()}.html";
                var filePath = Path.Combine(tempPath, fileName);

                File.WriteAllText(filePath, headerHtml, System.Text.Encoding.UTF8);

                var baseUrl = _environment.IsDevelopment()
                    ? "http://localhost:8080"
                    : Environment.GetEnvironmentVariable("RENDER_EXTERNAL_URL") ?? "https://contratos-pdf-api.onrender.com";

                return $"{baseUrl}/storage/temp/images/{fileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creando archivo temporal para header");
                return string.Empty;
            }
        }

        private void CleanupTempHeaderFile(string headerUrl)
        {
            try
            {
                if (!string.IsNullOrEmpty(headerUrl) && headerUrl.Contains("/storage/temp/images/"))
                {
                    var fileName = Path.GetFileName(new Uri(headerUrl).LocalPath);
                    var filePath = Path.Combine(_environment.WebRootPath, "storage", "temp", "images", fileName);

                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        _logger.LogDebug($"Archivo temporal del header eliminado: {fileName}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error limpiando archivo temporal del header");
            }
        }

        public byte[] GeneratePdfFromHtml(string htmlContent)
        {
            string headerUrl = string.Empty;

            try
            {
                _logger.LogInformation("Iniciando conversión HTML a PDF con header repetido usando HtmUrl");

                // Determinar la URL base según el ambiente
                var baseUrl = _environment.IsDevelopment()
                    ? "http://localhost:8080"
                    : Environment.GetEnvironmentVariable("RENDER_EXTERNAL_URL") ?? "https://contratos-pdf-api.onrender.com";

                var cleanedHtml = LimpiarYAgregarSeccionFirmas(htmlContent);


                // HTML limpio solo para el contenido (SIN header)
                var contentHtml = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1'>
    <style>
        /* Fix para renderizado de texto */
        * {{
            -webkit-font-smoothing: antialiased;
            -moz-osx-font-smoothing: grayscale;
            text-rendering: optimizeLegibility;
        }}
        
        body {{ 
            font-family: 'Times New Roman', serif; 
            font-size: 12pt; 
            line-height: 1.8; 
            margin: 0; 
            padding: 15px 20px;
            text-align: justify;
            word-wrap: break-word;
            overflow-wrap: break-word;
            hyphens: none;
            background-color: white;
            color: black;
        }}
        
        /* OCULTAR cualquier header que venga del HTML del Angular */
        .page-header-repeat,
        .contract-header-table,
        .page-header {{
            display: none !important;
        }}
        
        .contract-paragraph {{
            margin-bottom: 1.5em;
            text-align: justify;
            page-break-inside: avoid;
            orphans: 3;
            widows: 3;
            padding-right: 5px;
            line-height: 1.8;
            word-spacing: 0.1em;
            letter-spacing: 0.02em;
        }}
        
        /* Mejorar párrafos con texto justificado */
        p {{
            margin: 0 0 1.2em 0;
            text-align: justify;
            line-height: 1.8;
            padding-right: 8px;
            word-spacing: 0.1em;
            text-indent: 0;
            hyphens: none;
        }}
        
        .tabla-cantidades-img {{
            width: 100% !important;
            max-width: 100% !important;
            height: auto !important;
            display: block !important;
            margin: 20px auto !important;
            border: 1px solid #ccc;
            page-break-inside: avoid;
        }}
        
        .tabla-cantidades-container {{
            width: 100%;
            text-align: center;
            margin: 20px 0;
            page-break-inside: avoid;
        }}
        
        ul {{
            margin: 15px 0;
            padding-left: 35px;
            list-style-type: disc;
        }}
        
        li {{
            margin: 8px 0;
            text-align: justify;
            line-height: 1.8;
            padding-right: 8px;
            word-spacing: 0.1em;
        }}
        
        strong, b {{
            font-weight: bold;
            letter-spacing: 0.01em;
        }}
        
        h1, h2, h3, h4, h5, h6 {{
            page-break-after: avoid;
            margin: 1.5em 0 0.8em 0;
            line-height: 1.4;
            text-align: left;
        }}
        
        /* ✅ ESTILOS PARA SECCIÓN DE FIRMAS */
        .firmas-container {{
            margin-top: 60px;
            page-break-inside: avoid;
            padding: 20px 0;
        }}
        
        .firmas-grid {{
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 60px;
            margin-top: 40px;
        }}
        
        .firma-seccion {{
            text-align: center;
            font-family: 'Times New Roman', serif;
            font-size: 12pt;
        }}
        
        .firma-linea {{
            border-top: 2px solid black;
            margin-bottom: 8px;
            width: 100%;
        }}
        
        .firma-nombre {{
            font-weight: bold;
            margin-bottom: 4px;
            line-height: 1.2;
        }}
        
        .firma-cargo {{
            font-size: 11pt;
            margin-bottom: 2px;
            line-height: 1.2;
        }}
        
        .firma-empresa {{
            font-size: 11pt;
            font-weight: bold;
            line-height: 1.2;
        }}
        
        .page-break {{
            page-break-before: always;
        }}
        
        .clausula {{
            padding-right: 10px;
            margin-bottom: 1.5em;
        }}
        
        table {{
            border-collapse: collapse;
            width: 100%;
            margin: 15px 0;
        }}
        
        td {{
            padding: 8px;
            vertical-align: top;
            word-wrap: break-word;
        }}
        
        span, em, i {{
            word-wrap: break-word;
            letter-spacing: 0.01em;
        }}
    </style>
</head>
<body>
    {cleanedHtml}
</body>
</html>";

                // Resto del código igual...
                var headerHtml = GenerateHeaderHtml(baseUrl);
                headerUrl = SaveHeaderToTempFile(headerHtml);

                var doc = new HtmlToPdfDocument()
                {
                    GlobalSettings = {
                ColorMode = ColorMode.Color,
                Orientation = Orientation.Portrait,
                PaperSize = PaperKind.A4,
                Margins = new MarginSettings {
                    Top = 35,
                    Bottom = 20,
                    Left = 20,
                    Right = 25,
                    Unit = Unit.Millimeters
                },
                DPI = 96
            },
                    Objects = {
                new ObjectSettings {
                    PagesCount = true,
                    HtmlContent = contentHtml,
                    WebSettings = {
                        DefaultEncoding = "utf-8",
                        LoadImages = true,
                        PrintMediaType = true
                    },
                    HeaderSettings = {
                        FontSize = 9,
                        Line = false,
                        Spacing = 5,
                        HtmUrl = headerUrl
                    },
                    FooterSettings = {
                        FontSize = 10,
                        Right = "Página [page] de [toPage]",
                        Line = false,
                        Spacing = 8
                    }
                }
            }
                };

                var result = _converter.Convert(doc);
                _logger.LogInformation($"PDF con header repetido convertido exitosamente. Tamaño: {result.Length} bytes");

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en conversión PDF con header repetido: {Message}", ex.Message);
                throw;
            }
            finally
            {
                // Limpiar archivo temporal del header
                CleanupTempHeaderFile(headerUrl);
            }
        }


        private string LimpiarYAgregarSeccionFirmas(string htmlContent, dynamic? contratoData = null)
        {
            try
            {
                // Remover cualquier sección de firmas existente del frontend
                var cleanedHtml = htmlContent;

                // Buscar y remover divs de firmas existentes
                var firmasPatterns = new[]
                {
            @"<div[^>]*firmas[^>]*>.*?</div>",
            @"<div[^>]*grid-template-columns[^>]*>.*?</div>",
            @"<!-- Espacio amplio para firmas -->.*?</div>"
        };

                foreach (var pattern in firmasPatterns)
                {
                    cleanedHtml = System.Text.RegularExpressions.Regex.Replace(
                        cleanedHtml, pattern, "",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase |
                        System.Text.RegularExpressions.RegexOptions.Singleline);
                }

                // ✅ EXTRAER DATOS DE FIRMAS DINÁMICAMENTE
                string nombreContratante = "Diego Fernando Zárate Valdivieso";
                string cargoContratante = "Gerente General";
                string nombreContratista = "[NOMBRE_CONTRATISTA]";
                string cargoContratista = "[CARGO_REPRESENTANTE]";
                string empresaContratista = "[EMPRESA_CONTRATISTA]";

                if (contratoData != null)
                {
                    try
                    {
                        // Extraer nombre del contratista
                        if (contratoData.nombreContratista != null)
                            nombreContratista = contratoData.nombreContratista.ToString();
                        else if (contratoData.representanteLegalContratista != null)
                            nombreContratista = contratoData.representanteLegalContratista.ToString();

                        // Extraer empresa/razón social
                        if (contratoData.nombreContratista != null)
                            empresaContratista = contratoData.nombreContratista.ToString();

                        // Determinar cargo según tipo de representante
                        if (contratoData.tipoRepresentanteContratista != null)
                        {
                            cargoContratista = contratoData.tipoRepresentanteContratista.ToString() switch
                            {
                                "persona_natural" => "Contratista",
                                "persona_juridica" => "Representante Legal",
                                "representante_legal" => "Representante Legal",
                                "gerente_general" => "Gerente General",
                                "apoderado_especial" => "Apoderado Especial",
                                _ => "Contratista"
                            };
                        }

                        _logger.LogInformation($"Datos de firmas extraídos: Contratista={nombreContratista}, Cargo={cargoContratista}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error extrayendo datos de firmas, usando valores por defecto");
                    }
                }

                // ✅ GENERAR SECCIÓN DE FIRMAS CON DATOS DINÁMICOS
                var seccionFirmas = $@"
<div class='firmas-container'>
    <div class='firmas-grid'>
        <div class='firma-seccion'>
            <div class='firma-linea'></div>
            <div class='firma-nombre'>{nombreContratante}</div>
            <div class='firma-cargo'>{cargoContratante}</div>
            <div class='firma-empresa'>HIDALGO E HIDALGO S.A.</div>
            <div class='firma-cargo'>LA CONTRATANTE</div>
        </div>
        
        <div class='firma-seccion'>
            <div class='firma-linea'></div>
            <div class='firma-nombre'>{nombreContratista}</div>
            <div class='firma-cargo'>{cargoContratista}</div>
            <div class='firma-empresa'>{empresaContratista}</div>
            <div class='firma-cargo'>EL CONTRATISTA</div>
        </div>
    </div>
</div>";

                // Agregar la sección de firmas al final del contenido
                cleanedHtml += seccionFirmas;

                _logger.LogInformation("Sección de firmas agregada al HTML del contrato");
                return cleanedHtml;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error limpiando HTML y agregando firmas, usando HTML original");
                return htmlContent;
            }
        }
    }
}