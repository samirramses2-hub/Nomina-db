namespace NominaApp.Core.Entities;

public class CFDIRegistro
{
    public int Id { get; set; }
    public int EmpleadoId { get; set; }
    public int PeriodoNominaId { get; set; }
    public string UUID { get; set; } = string.Empty;
    public string RFCEmisor { get; set; } = string.Empty;
    public string RFCReceptor { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public EstadoCFDI Estado { get; set; } = EstadoCFDI.Vigente;
    public string? MotivoCancelacion { get; set; }
    public string? UUIDSustitucion { get; set; }
    public DateTime FechaTimbrado { get; set; } = DateTime.UtcNow;
    public DateTime? FechaCancelacion { get; set; }

    public DateTime? FechaFirma { get; set; }
    public string? IPFirma { get; set; }

    public Empleado Empleado { get; set; } = null!;
    public PeriodoNomina PeriodoNomina { get; set; } = null!;
}

public enum EstadoCFDI
{
    Vigente    = 1,
    Cancelado  = 2,
    EnProceso  = 3
}