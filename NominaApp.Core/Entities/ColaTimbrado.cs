namespace NominaApp.Core.Entities;

public class ColaTimbrado
{
    public int Id { get; set; }
    public int EmpleadoId { get; set; }
    public int PeriodoNominaId { get; set; }
    public EstadoCola Estado { get; set; } = EstadoCola.Pendiente;
    public int Intentos { get; set; } = 0;
    public int MaxIntentos { get; set; } = 3;
    public DateTime? ProximoIntento { get; set; }
    public string? UltimoError { get; set; }
    public string? UUID { get; set; }
    public bool Diferido { get; set; } = false;
    public DateTime? FechaDiferido { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaCompletado { get; set; }

    public Empleado Empleado { get; set; } = null!;
    public PeriodoNomina PeriodoNomina { get; set; } = null!;
}

public enum EstadoCola
{
    Pendiente   = 1,
    Procesando  = 2,
    Completado  = 3,
    Error       = 4,
    Diferido    = 5,
    Cancelado   = 6
}