namespace NominaApp.Core.Entities;

public class Departamento
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? CodigoDepartamento { get; set; }
    public int? JefeId { get; set; }
    public int? DepartamentoPadreId { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public Empresa Empresa { get; set; } = null!;
    public Empleado? Jefe { get; set; }
    public Departamento? DepartamentoPadre { get; set; }
    public ICollection<Empleado> Empleados { get; set; } = new List<Empleado>();
}