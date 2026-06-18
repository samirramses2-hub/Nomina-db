namespace NominaApp.Core.Entities;

public class MovimientoIMSS
{
    public int Id { get; set; }
    public int EmpleadoId { get; set; }
    public int EmpresaId { get; set; }
    public TipoMovimientoIMSS TipoMovimiento { get; set; }
    public DateTime FechaMovimiento { get; set; }
    public decimal SalarioDiarioIntegrado { get; set; }
    public string? Observaciones { get; set; }
    public EstadoMovimiento Estado { get; set; } = EstadoMovimiento.Pendiente;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public Empleado Empleado { get; set; } = null!;
    public Empresa Empresa { get; set; } = null!;
}

public enum TipoMovimientoIMSS
{
    Alta                = 1,
    Baja                = 2,
    ModificacionSalario = 3,
    ReingresoPorBaja    = 4,
    AusenciaSinGoce     = 5,
    RegresoAusencia     = 6,
}

public enum EstadoMovimiento
{
    Pendiente  = 1,
    Reportado  = 2,
    Cancelado  = 3
}