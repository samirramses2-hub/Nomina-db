namespace NominaApp.Core.Entities;

public class Incidencia
{
    public int Id { get; set; }
    public int EmpleadoId { get; set; }
    public int PeriodoNominaId { get; set; }

    public TipoIncidencia Tipo { get; set; }
    public decimal Cantidad { get; set; }  // días, horas o monto según el tipo
    public string? Observaciones { get; set; }

    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    public Empleado Empleado { get; set; } = null!;
    public PeriodoNomina PeriodoNomina { get; set; } = null!;
}

public enum TipoIncidencia
{
    FaltaJustificada      = 1,
    FaltaInjustificada    = 2,
    Vacaciones            = 3,
    HoraExtraSimple       = 4,
    HoraExtraDoble        = 5,
    HoraExtraTriple       = 6,
    Bono                  = 7,
    PrimaDominical        = 8,
    IncapacidadIMSS       = 9,   // Tipo 1 — enfermedad general
    IncapacidadRiesgo     = 10,  // Tipo 2 — riesgo de trabajo
    IncapacidadMaternidad = 11,  // Tipo 3 — maternidad
    LicenciaConGoce       = 12,  // Permiso pagado
    LicenciaSinGoce       = 13,  // Permiso no pagado
    PrimaVacacional       = 14,  // 25% sobre días de vacaciones
    Aguinaldo             = 15,  // Proporcional al periodo
    DescuentoInfonavit    = 16,  // Descuento por crédito Infonavit
}