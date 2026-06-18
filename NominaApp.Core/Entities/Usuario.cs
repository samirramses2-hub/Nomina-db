namespace NominaApp.Core.Entities;

public class Usuario
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public RolUsuario Rol { get; set; } = RolUsuario.Capturista;
    public int? EmpresaId { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    // Campos para registro y verificación
    public string? CodigoVerificacion { get; set; }
    public bool EmailVerificado { get; set; } = true; // Por defecto true para no afectar registros viejos

    public Empresa? Empresa { get; set; }
    public int? EmpleadoId { get; set; }
    public Empleado? EmpleadoRef { get; set; }
}

public enum RolUsuario
{
    Administrador = 1,
    Contador      = 2,
    RRHH          = 3,
    Capturista    = 4,
    SoloLectura   = 5,
    Empleado      = 6
}