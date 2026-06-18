namespace NominaApp.API.DTOs;

public class CrearIncidenciaDto
{
    public int EmpleadoId { get; set; }
    public int PeriodoNominaId { get; set; }
    public int Tipo { get; set; }
    public decimal Cantidad { get; set; }
    public string? Observaciones { get; set; }
}