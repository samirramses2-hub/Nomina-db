namespace NominaApp.Core.Entities;

public class HistorialSalarial
{
    public int Id { get; set; }
    public int EmpleadoId { get; set; }
    public decimal SalarioDiario { get; set; }
    public DateTime FechaVigencia { get; set; }
    public DateTime? FechaFin { get; set; }
    public string? Motivo { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    public Empleado Empleado { get; set; } = null!;
}