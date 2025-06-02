using ContratosPdfApi.Models;

namespace ContratosPdfApi.Services
{
    public interface IPdfService
    {
        byte[] GeneratePdfFromHtml(string htmlContent);
        string GenerateContractHtml(ContratoData contratoData);
    }
}