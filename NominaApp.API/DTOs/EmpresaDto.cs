namespace NominaApp.API.DTOs;

public class CrearEmpleadoDto
{
    public int EmpresaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string ApellidoPaterno { get; set; } = string.Empty;
    public string ApellidoMaterno { get; set; } = string.Empty;
    public string RFC { get; set; } = string.Empty;
    public string CURP { get; set; } = string.Empty;
    public string NSS { get; set; } = string.Empty;
    public decimal SalarioDiario { get; set; }
    public int TipoContrato { get; set; }
    public int TipoPeriodo { get; set; }
    public DateTime FechaIngreso { get; set; }
    public string? Banco { get; set; }
    public string? CuentaBancaria { get; set; }
    public string? CLABE { get; set; }
    public string? CodigoEmpleado { get; set; }
    public int? DepartamentoId { get; set; }
    public string? Puesto { get; set; }
}
