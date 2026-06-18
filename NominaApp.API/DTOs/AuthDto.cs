namespace NominaApp.API.DTOs;

public class LoginDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RegisterDto
{
    public string Nombre { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int Rol { get; set; } = 4;
    public int? EmpresaId { get; set; }
    public int? EmpleadoId { get; set; }
}

public class RecuperarPasswordDto
{
    public string Email { get; set; } = string.Empty;
}

public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public int? EmpresaId { get; set; }
    public int? EmpleadoId { get; set; }
    public DateTime Expiracion { get; set; }
}

public class RegistroClienteDto
{
    public string NombreEmpresa { get; set; } = string.Empty;
    public string NombreUsuario { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class VerificarEmailDto
{
    public string Email { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
}