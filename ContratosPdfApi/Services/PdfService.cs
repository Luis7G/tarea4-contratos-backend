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

        public byte[] GeneratePdfFromHtml(string htmlContent)
        {
            try
            {
                _logger.LogInformation("Iniciando conversión HTML a PDF");

                // Determinar la URL base según el ambiente
                var baseUrl = _environment.IsDevelopment()
                    ? "http://localhost:5221"
                    : Environment.GetEnvironmentVariable("RENDER_EXTERNAL_URL") ?? "https://your-app-name.onrender.com";

                // HTML completo con header incluido en el contenido principal
                var styledHtml = $@"
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
            padding: 20px;
            text-align: justify;
            word-wrap: break-word;
            overflow-wrap: break-word;
            hyphens: none;
            -webkit-hyphens: none;
            -moz-hyphens: none;
            background-color: white;
            color: black;
        }}
        
        /* Header styles for first page */
        .contract-header {{
            width: 100%;
            border-collapse: collapse;
            border: 2px solid black !important;
            margin-bottom: 20px;
            page-break-inside: avoid;
            page-break-after: avoid;
            background-color: white;
        }}
        
        .contract-header td {{
            padding: 10px;
            border: 1px solid black !important;
            vertical-align: middle;
            font-size: 10pt;
            background-color: white;
            color: black;
        }}
        
        .header-cell-title {{
            text-align: center;
            font-weight: bold;
            font-size: 14pt;
        }}
        
        .header-cell-logo {{
            text-align: center;
            width: 20%;
        }}
        
        .logo-img {{
            max-width: 80px;
            height: auto;
        }}
        
        /* Hide the repeated header from Angular HTML */
        .page-header-repeat {{
            display: none !important;
        }}
        
        .contract-paragraph {{
            margin-bottom: 1.5em;
            text-align: justify;
            page-break-inside: avoid;
            orphans: 3;
            widows: 3;
            padding-right: 5px; /* Espacio extra para evitar cortes */
            line-height: 1.8;
            word-spacing: 0.1em;
            letter-spacing: 0.02em;
        }}
        
        /* Mejorar párrafos con texto justificado */
        p {{
            margin: 0 0 1.2em 0;
            text-align: justify;
            line-height: 1.8;
            padding-right: 8px; /* Espacio extra al final */
            word-spacing: 0.1em;
            text-indent: 0;
            hyphens: none;
            -webkit-hyphens: none;
            -moz-hyphens: none;
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
            padding-right: 8px; /* Espacio extra para listas */
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
        
        .firmas-container {{
            margin-top: 60px;
            page-break-inside: avoid;
            padding-right: 10px;
        }}
        
        /* Page break utilities */
        .page-break {{
            page-break-before: always;
        }}
        
        /* Específico para evitar cortes de texto */
        .clausula {{
            padding-right: 10px;
            margin-bottom: 1.5em;
        }}
        
        /* Mejorar tablas */
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
        
        /* Fix para elementos inline */
        span, em, i {{
            word-wrap: break-word;
            letter-spacing: 0.01em;
        }}
    </style>
</head>
<body>
    <!-- Header solo en primera página -->
    <table class='contract-header'>
        <tr>
            <td rowspan='3' class='header-cell-logo'>
                <img src='{baseUrl}/assets/logo-hidalgo.png' alt='Logo' class='logo-img'>
            </td>
            <td rowspan='3' class='header-cell-title'>
                CONTRATO PARA LA ADQUISICIÓN DE BIENES
            </td>
            <td style='width: 17.5%; font-size: 8pt;'>
                <strong>TIPO:</strong> Documento
            </td>
            <td style='width: 17.5%; font-size: 8pt;'>
                <strong>VERSIÓN:</strong> 2
            </td>
        </tr>
        <tr>
            <td style='font-size: 8pt;'>
                <strong>PROCESO:</strong> GESTIÓN EJECUCIÓN Y CONTROL
            </td>
            <td style='font-size: 8pt;'>
                <strong>VIGENCIA:</strong> 01-abril-2025
            </td>
        </tr>
        <tr>
            <td style='font-size: 8pt;'>
                <strong>CÓDIGO:</strong> ECO-D-08
            </td>
            <td style='font-size: 8pt;'>
                <!-- Espacio para más info si necesario -->
            </td>
        </tr>
    </table>
    
    <!-- Contenido del contrato -->
    <div style='padding-right: 10px;'>
        {htmlContent}
    </div>
</body>
</html>";

                var doc = new HtmlToPdfDocument()
                {
                    GlobalSettings = {
                        ColorMode = ColorMode.Color,
                        Orientation = Orientation.Portrait,
                        PaperSize = PaperKind.A4,
                        Margins = new MarginSettings {
                            Top = 20,
                            Bottom = 20,
                            Left = 20,
                            Right = 25, // Margen derecho un poco más amplio
                            Unit = Unit.Millimeters
                        },
                        DPI = 96 // DPI estándar para mejor renderizado
                    },
                    Objects = {
                        new ObjectSettings {
                            PagesCount = true,
                            HtmlContent = styledHtml,
                            WebSettings = {
                                DefaultEncoding = "utf-8",
                                LoadImages = true,
                                PrintMediaType = true, // Usar estilos de impresión
                                UserStyleSheet = "" // Sin estilos externos que puedan interferir
                            },
                            FooterSettings = {
                                FontSize = 10,
                                Right = "[page]",
                                Line = false,
                                Spacing = 8 // Más espacio para el footer
                            }
                        }
                    }
                };

                var result = _converter.Convert(doc);
                _logger.LogInformation($"PDF convertido exitosamente. Tamaño: {result.Length} bytes");

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en conversión PDF: {Message}", ex.Message);
                throw;
            }
        }

        public string GenerateContractHtml(ContratoData contratoData)
        {
            _logger.LogWarning("GenerateContractHtml no está implementado completamente.");
            return $"<h1>Contrato para {contratoData.NombreContratista}</h1><p>Detalles del contrato...</p>";
        }
    }
}