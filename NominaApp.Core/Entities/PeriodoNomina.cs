namespace NominaApp.Core.Entities;

public class PeriodoNomina
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }

    public int NumeroPeriodo { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
    public DateTime FechaPago { get; set; }
    public TipoPeriodo TipoPeriodo { get; set; }
    public int EjercicioFiscal { get; set; }
    public TipoPeriodoEspecial TipoEspecial { get; set; } = TipoPeriodoEspecial.Normal;
    public EstadoPeriodo Estado { get; set; } = EstadoPeriodo.Abierto;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public Empresa Empresa { get; set; } = null!;
    public ICollection<Incidencia> Incidencias { get; set; } = new List<Incidencia>();
}

public enum EstadoPeriodo
{
    Abierto    = 1,
    Calculado  = 2,
    Cerrado    = 3
}

public enum TipoPeriodoEspecial
{
    Normal           = 0,
    Aguinaldo        = 1,
    PTU              = 2,
    PrimaVacacional  = 3,
    Finiquito        = 4
}