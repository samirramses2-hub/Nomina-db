namespace NominaApp.API.DTOs;

public class CrearMovimientoIMSSDto
{
    public int EmpleadoId { get; set; }
    public int TipoMovimiento { get; set; }
    public DateTime FechaMovimiento { get; set; }
    public decimal SalarioDiarioIntegrado { get; set; }
    public string? Observaciones { get; set; }
}