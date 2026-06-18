namespace NominaApp.Core.Entities;

public class DeclaracionPTU
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public int EjercicioFiscal { get; set; }
    public decimal UtilidadFiscal { get; set; }
    public decimal MontoRepartir { get; set; }
    public DateTime FechaDeclaracion { get; set; }
    public DateTime FechaPago { get; set; }
    public EstadoPTU Estado { get; set; } = EstadoPTU.Calculada;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public Empresa Empresa { get; set; } = null!;
}

public enum EstadoPTU
{
    Calculada = 1,
    Pagada    = 2
}