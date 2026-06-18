namespace NominaApp.Core.Entities;

public class Asistencia
{
    public int Id { get; set; }
    public int EmpleadoId { get; set; }
    public DateTime Fecha { get; set; }
    public TimeSpan? HoraEntrada { get; set; }
    public TimeSpan? HoraSalida { get; set; }
    public TimeSpan? HoraEntradaEsperada { get; set; } = new TimeSpan(9, 0, 0);
    public TimeSpan? HoraSalidaEsperada { get; set; } = new TimeSpan(18, 0, 0);
    public EstadoAsistencia Estado { get; set; } = EstadoAsistencia.Pendiente;
    public decimal MinutosRetardo { get; set; } = 0;
    public decimal HorasTrabajadas { get; set; } = 0;
    public decimal HorasExtra { get; set; } = 0;
    public string? Observaciones { get; set; }
    public string? MetodoRegistro { get; set; } = "Manual";
    public string? CodigoQR { get; set; }
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    public Empleado Empleado { get; set; } = null!;
}

public enum EstadoAsistencia
{
    Pendiente    = 1,
    Presente     = 2,
    Retardo      = 3,
    FaltaJust    = 4,
    FaltaInjust  = 5,
    HomeOffice   = 6,
    Vacaciones   = 7,
    Incapacidad  = 8
}

public class HorarioEmpleado
{
    public int Id { get; set; }
    public int EmpleadoId { get; set; }
    public DayOfWeek DiaSemana { get; set; }
    public TimeSpan HoraEntrada { get; set; }
    public TimeSpan HoraSalida { get; set; }
    public bool Activo { get; set; } = true;

    public Empleado Empleado { get; set; } = null!;
}