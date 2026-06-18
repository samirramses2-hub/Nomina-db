namespace NominaApp.Core.Entities;

public class UsuarioEmpresa
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public int EmpresaId { get; set; }
    public RolEmpresa Rol { get; set; } = RolEmpresa.Contador;
    public bool Activo { get; set; } = true;
    public DateTime FechaAsignacion { get; set; } = DateTime.UtcNow;

    public Usuario Usuario { get; set; } = null!;
    public Empresa Empresa { get; set; } = null!;
}

public enum RolEmpresa
{
    Administrador = 1,
    Contador      = 2,
    Capturista    = 3,
    SoloLectura   = 4
}