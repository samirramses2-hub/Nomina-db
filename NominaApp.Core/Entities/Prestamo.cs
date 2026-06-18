namespace NominaApp.Core.Entities;

public class Prestamo
{
    public int Id { get; set; }
    public int EmpleadoId { get; set; }
    public int EmpresaId { get; set; }
    public decimal MontoTotal { get; set; }
    public decimal MontoRestante { get; set; }
    public decimal PagoQuincenal { get; set; }
    public int NumeroPagos { get; set; }
    public int PagosRealizados { get; set; } = 0;
    public decimal TasaInteres { get; set; } = 0;
    public DateTime FechaOtorgamiento { get; set; }
    public DateTime? FechaLiquidacion { get; set; }
    public EstadoPrestamo Estado { get; set; } = EstadoPrestamo.Activo;
    public string? Concepto { get; set; }
    public string? Autorizador { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public Empleado Empleado { get; set; } = null!;
    public Empresa Empresa { get; set; } = null!;
    public ICollection<PagoPrestamo> Pagos { get; set; } = new List<PagoPrestamo>();
}

public class PagoPrestamo
{
    public int Id { get; set; }
    public int PrestamoId { get; set; }
    public int? PeriodoNominaId { get; set; }
    public decimal MontoPago { get; set; }
    public decimal MontoInteres { get; set; } = 0;
    public decimal MontoCapital { get; set; }
    public decimal SaldoRestante { get; set; }
    public int NumeroPago { get; set; }
    public DateTime FechaPago { get; set; }
    public string? Observaciones { get; set; }

    public Prestamo Prestamo { get; set; } = null!;
}

public enum EstadoPrestamo
{
    Activo     = 1,
    Liquidado  = 2,
    Cancelado  = 3,
    Suspendido = 4
}