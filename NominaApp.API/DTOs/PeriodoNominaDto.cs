namespace NominaApp.API.DTOs;

public class CrearPeriodoNominaDto
{
    public int EmpresaId { get; set; }
    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
    public int TipoPeriodo { get; set; }
    public int EjercicioFiscal { get; set; }
}