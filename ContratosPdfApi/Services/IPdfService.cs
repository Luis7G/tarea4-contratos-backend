using ContratosPdfApi.Models;

namespace ContratosPdfApi.Services
{
    public interface IPdfService
    {
        byte[] GeneratePdfFromHtml(string htmlContent, dynamic? contratoData = null);
    }
}