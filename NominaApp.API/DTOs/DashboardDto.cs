namespace NominaApp.API.DTOs;

public class DashboardDto
{
    public int TotalEmpleados { get; set; }
    public int TotalPeriodos { get; set; }
    public int CfdisTimbrados { get; set; }
    public decimal CostoTotalUltimoPeriodo { get; set; }
    public decimal NetoTotalUltimoPeriodo { get; set; }
    public decimal IMSSPatronalUltimoPeriodo { get; set; }
    public decimal IndiceAusentismo { get; set; }
    public decimal RotacionPersonal { get; set; }
    public List<CostoPorPeriodoDto> CostosPorPeriodo { get; set; } = new();
    public List<CostoPorDepartamentoDto> CostosPorDepartamento { get; set; } = new();
    public List<AlertaDto> Alertas { get; set; } = new();
}

public class CostoPorPeriodoDto
{
    public string Periodo { get; set; } = string.Empty;
    public decimal TotalPercepciones { get; set; }
    public decimal TotalNeto { get; set; }
    public decimal CostoEmpresa { get; set; }
    public int NumEmpleados { get; set; }
}

public class CostoPorDepartamentoDto
{
    public string Departamento { get; set; } = string.Empty;
    public decimal CostoTotal { get; set; }
    public int NumEmpleados { get; set; }
    public decimal PorcentajeDelTotal { get; set; }
}

public class AlertaDto
{
    public string Tipo { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public string Nivel { get; set; } = string.Empty; // info, warning, danger
}