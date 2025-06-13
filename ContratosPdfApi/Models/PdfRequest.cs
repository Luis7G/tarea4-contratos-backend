namespace ContratosPdfApi.Models
{
    public class PdfRequest
    {
        public string HtmlContent { get; set; } = string.Empty;
            public dynamic? ContratoData { get; set; } 
    }
}