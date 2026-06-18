namespace NominaApp.API.DTOs;

public class ResultadoEmpleadoDto
{
    public int EmpleadoId { get; set; }
    public string CodigoEmpleado { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string RFC { get; set; } = string.Empty;
    public decimal TotalPercepciones { get; set; }
    public decimal TotalDeducciones { get; set; }
    public decimal NetoPagar { get; set; }
    public decimal CostoEmpresa { get; set; }
    public List<LineaDto> Percepciones { get; set; } = new();
    public List<LineaDto> Deducciones { get; set; } = new();
    public string DetalleISR { get; set; } = string.Empty;
    public string Estado { get; set; } = "pendiente";
    public string? UUID { get; set; }
    public string? Error { get; set; }
}

public class LineaDto
{
    public string Concepto { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public string Explicacion { get; set; } = string.Empty;
}

public class ProcesamientoMasivoResultDto
{
    public int PeriodoId { get; set; }
    public string Periodo { get; set; } = string.Empty;
    public string Empresa { get; set; } = string.Empty;
    public int TotalEmpleados { get; set; }
    public decimal TotalNeto { get; set; }
    public decimal TotalCostoEmpresa { get; set; }
    public List<ResultadoEmpleadoDto> Empleados { get; set; } = new();
}