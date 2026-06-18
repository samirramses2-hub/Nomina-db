namespace NominaApp.Core.Entities;

public class Empleado
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }

    public string Nombre { get; set; } = string.Empty;
    public string ApellidoPaterno { get; set; } = string.Empty;
    public string ApellidoMaterno { get; set; } = string.Empty;
    public string RFC { get; set; } = string.Empty;
    public string CURP { get; set; } = string.Empty;
    public string NSS { get; set; } = string.Empty;
    public string? Banco { get; set; }
    public string? CuentaBancaria { get; set; }
    public string? CLABE { get; set; }

    public decimal SalarioDiario { get; set; }
    public TipoContrato TipoContrato { get; set; }
    public TipoPeriodo TipoPeriodo { get; set; }

    public DateTime FechaIngreso { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public Empresa Empresa { get; set; } = null!;
    public ICollection<Incidencia> Incidencias { get; set; } = new List<Incidencia>();
    public string CodigoEmpleado { get; set; } = string.Empty;
    public int? DepartamentoId { get; set; }
    public string? Puesto { get; set; }
    public Departamento? Departamento { get; set; }
}

public enum TipoContrato
{
    TiempoIndefinido = 1,
    TiempoDeterminado = 2,
    Honorarios = 3
}

public enum TipoPeriodo
{
    Semanal = 1,
    Quincenal = 2,
    Mensual = 3

}