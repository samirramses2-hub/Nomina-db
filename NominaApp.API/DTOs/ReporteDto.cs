namespace NominaApp.API.DTOs;

public class ReporteNominaDto
{
    public int PeriodoId { get; set; }
    public string PeriodoDescripcion { get; set; } = string.Empty;
    public string EmpresaRazonSocial { get; set; } = string.Empty;
    public int TotalEmpleados { get; set; }
    public decimal TotalPercepciones { get; set; }
    public decimal TotalDeducciones { get; set; }
    public decimal TotalNeto { get; set; }
    public decimal TotalIMSSObrero { get; set; }
    public decimal TotalIMSSPatronal { get; set; }
    public decimal CostoTotalEmpresa { get; set; }
    public List<ReporteEmpleadoDto> Empleados { get; set; } = new();
}

public class ReporteEmpleadoDto
{
    public string NombreCompleto { get; set; } = string.Empty;
    public string RFC { get; set; } = string.Empty;
    public decimal SalarioDiario { get; set; }
    public decimal Percepciones { get; set; }
    public decimal Deducciones { get; set; }
    public decimal Neto { get; set; }
    public decimal IMSSObrero { get; set; }
    public decimal IMSSPatronal { get; set; }
    public decimal CostoEmpleado { get; set; }
}