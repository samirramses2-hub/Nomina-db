using System;

namespace NominaApp.Core.Entities;

public enum EstadoSolicitudVacaciones
{
    Pendiente,
    Aprobada,
    Rechazada
}

public class SolicitudVacaciones
{
    public int Id { get; set; }
    public int EmpleadoId { get; set; }
    public Empleado Empleado { get; set; } = null!;

    public DateTime FechaSolicitud { get; set; }
    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
    
    public int DiasSolicitados { get; set; }
    public EstadoSolicitudVacaciones Estado { get; set; }
    public string? ComentariosEmpleado { get; set; }
    public string? ComentariosRRHH { get; set; }
}
