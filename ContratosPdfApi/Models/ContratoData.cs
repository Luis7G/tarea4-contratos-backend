using System;

namespace ContratosPdfApi.Models
{
    public class ContratoData
    {
        public string TipoRepresentanteContratante { get; set; }
        public string ApoderadoEspecialSeleccionado { get; set; }
        public string NombreProyectoSuperintendente { get; set; }
        public string NombreSuperintendente { get; set; }
        public string DocumentoRespaldoContratante { get; set; }
        public string NombreContratista { get; set; }
        public string RucContratista { get; set; }
        public string TipoRepresentanteContratista { get; set; }
        public string RepresentanteLegalContratista { get; set; }
        public string ClausulaPrimeraAntecedentes { get; set; }
        public string ClausulaSegundaGarantiasRendidas { get; set; }
        public string ClausulaCuartaDescripcionBienes { get; set; }
        public string ClausulaCuartaLugarEntrega { get; set; }
        public bool OfertaContemplaSoporteTecnico { get; set; }
        public bool CapacitacionRequierePersonalCertificado { get; set; }
        public string ClausulaCuartaLapsoSoporte { get; set; }
        public string ClausulaCuartaCapacitacionNumeroServidores { get; set; }
        public string ClausulaCuartaCapacitacionLugar { get; set; }
        public string ClausulaCuartaCapacitacionPersonalCertificado { get; set; }
        public decimal? ClausulaQuintaPrecioTotalLetrasNumeros { get; set; }
        public string ClausulaQuintaPrecioTotalLetras { get; set; }
        public string ClausulaQuintaImagenTablaCantidades { get; set; }
        public string ClausulaSextaFormaPagoOpcion { get; set; }
        public string ClausulaSextaFormaPagoTextoGeneral { get; set; }
        public string BeneficiarioBanco { get; set; }
        public string BeneficiarioNombre { get; set; }
        public string BeneficiarioDireccion { get; set; }
        public string BeneficiarioRuc { get; set; }
        public string BeneficiarioNumeroCuenta { get; set; }
        public string BeneficiarioTipoCuenta { get; set; }
        public string BeneficiarioCorreo { get; set; }
        public bool RequiereGarantiaTecnica { get; set; }
        public bool RequiereGarantiaBuenUsoAnticipo { get; set; }
        public string TipoGarantiaTecnica { get; set; }
        public string PlazoGarantiaTecnica { get; set; }
        public string ClausulaSeptimaGarantiasOpcion { get; set; }
        public string ClausulaSeptimaTextoGeneral { get; set; }
        public string ClausulaOctavaEstadoBienes { get; set; }
        public string ClausulaOctavaCapacitacion { get; set; }
        public string ClausulaOctavaPeriodoPlazo { get; set; }
        public string ClausulaOctavaInicioPlazo { get; set; }
        public string ClausulaDecimaPorcentajeMulta { get; set; }
        public string ContratanteCorreoComunicaciones { get; set; }
        public string ContratistaProvinciaComunicaciones { get; set; }
        public string ContratistaCantonComunicaciones { get; set; }
        public string ContratistaParroquiaComunicaciones { get; set; }
        public string ContratistaDireccionComunicaciones { get; set; }
        public string ContratistaNumeroComunicaciones { get; set; }
        public string ContratistaTelefonosComunicaciones { get; set; }
        public string ContratistaCorreoComunicaciones { get; set; }
        public string ClausulaVigesimaSegundaCiudadArbitraje { get; set; }
        public string FechaFirmaContratoDia { get; set; }
        public string FechaFirmaContratoMes { get; set; }
        public string FechaFirmaContratoAnio { get; set; }
        public string Anexo1ConAnticipoPorcentaje { get; set; }
        public string Anexo1ConAnticipoValorRestantePorcentaje { get; set; }
        public string Anexo1ConAnticipoValorRestanteUSD { get; set; }
        public string Anexo1ConAnticipoPeriodoFacturas { get; set; }
        public string Anexo1SinAnticipoVariosPagosPeriodo { get; set; }
        public string Anexo2Opcion1_1FondoGarantiaAlternativa { get; set; }
        public string Anexo2Opcion1_1PlazoGarantiaTecnica { get; set; }
        public string Anexo2Opcion1_2PlazoGarantiaTecnica { get; set; }
        public string Anexo2Opcion2_1PlazoGarantiaTecnica { get; set; }
        public string Anexo2Opcion2_2PlazoGarantiaTecnica { get; set; }
    }
}