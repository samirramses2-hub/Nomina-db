namespace NominaApp.API.DTOs;

public class GeneracionPeriodosRequestDto
{
    public int EmpresaId { get; set; }
    public int EjercicioFiscal { get; set; }
    public int TipoPeriodo { get; set; }
    public int DiasHabilesParaPago { get; set; } = 3;
    public bool SobreescribirExistentes { get; set; } = false;
}

public class PeriodoGeneradoDto
{
    public int NumeroPeriodo { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public string FechaInicio { get; set; } = string.Empty;
    public string FechaFin { get; set; } = string.Empty;
    public string FechaPago { get; set; } = string.Empty;
    public bool Generado { get; set; }
    public string? Mensaje { get; set; }
}