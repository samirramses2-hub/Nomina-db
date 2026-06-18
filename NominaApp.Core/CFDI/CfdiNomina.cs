namespace NominaApp.Core.CFDI;

public class CfdiNominaRequest
{
    public string RfcEmisor { get; set; } = string.Empty;
    public string NombreEmisor { get; set; } = string.Empty;
    public string RegimenFiscalEmisor { get; set; } = string.Empty;
    public string RfcReceptor { get; set; } = string.Empty;
    public string NombreReceptor { get; set; } = string.Empty;
    public string CurpReceptor { get; set; } = string.Empty;
    public string NumSeguridadSocial { get; set; } = string.Empty;
    public DateTime FechaInicioRelLaboral { get; set; }
    public string PeriodicidadPago { get; set; } = string.Empty; // 04 = quincenal
    public string TipoContrato { get; set; } = string.Empty;     // 01 = indefinido
    public string TipoRegimen { get; set; } = string.Empty;      // 02 = sueldos
    public int NumEmpleado { get; set; }
    public DateTime FechaPago { get; set; }
    public DateTime FechaInicialPago { get; set; }
    public DateTime FechaFinalPago { get; set; }
    public int NumDiasPagados { get; set; }
    public decimal SalarioDiarioIntegrado { get; set; }
    public decimal SalarioBaseCotApor { get; set; }

    public List<CfdiPercepcion> Percepciones { get; set; } = new();
    public List<CfdiDeduccion> Deducciones { get; set; } = new();
}

public class CfdiPercepcion
{
    public string TipoPercepcion { get; set; } = string.Empty; // 001=sueldos, 019=horas extra, 010=bonos
    public string Clave { get; set; } = string.Empty;
    public string Concepto { get; set; } = string.Empty;
    public decimal ImporteGravado { get; set; }
    public decimal ImporteExento { get; set; }
}

public class CfdiDeduccion
{
    public string TipoDeduccion { get; set; } = string.Empty; // 001=IMSS, 002=ISR, 006=descuento
    public string Clave { get; set; } = string.Empty;
    public string Concepto { get; set; } = string.Empty;
    public decimal Importe { get; set; }
}