namespace NominaApp.Core.Entities;

public class Empresa
{
    public int Id { get; set; }
    public string RazonSocial { get; set; } = string.Empty;
    public string RFC { get; set; } = string.Empty;
    public string RegimenFiscal { get; set; } = string.Empty;
    public string DomicilioFiscal { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public ICollection<Empleado> Empleados { get; set; } = new List<Empleado>();
}