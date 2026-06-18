using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NominaApp.Core.Entities;
using NominaApp.Infrastructure.Data;
using NominaApp.API.DTOs;
using NominaApp.Infrastructure.Servicios;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly NominaDbContext _context;
    private readonly IConfiguration _config;
    private readonly EmailService _emailService;

    public AuthController(NominaDbContext context, IConfiguration config, EmailService emailService)
    {
        _context = context;
        _config  = config;
        _emailService = emailService;
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginDto dto)
    {
        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Email == dto.Email && u.Activo);

        if (usuario is null || !BCrypt.Net.BCrypt.Verify(dto.Password, usuario.PasswordHash))
            return Unauthorized("Email o contraseña incorrectos.");

        if (!usuario.EmailVerificado)
            return Unauthorized("unverified_email");

        var token = GenerarToken(usuario);
        return Ok(new AuthResponseDto
        {
            Token      = token,
            Nombre     = usuario.Nombre,
            Email      = usuario.Email,
            Rol        = usuario.Rol.ToString(),
            EmpresaId  = usuario.EmpresaId,
            Expiracion = DateTime.UtcNow.AddHours(int.Parse(_config["Jwt:ExpirationHours"]!)),
            EmpleadoId = usuario.EmpleadoId,
        });
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register(RegisterDto dto)
    {
        if (await _context.Usuarios.AnyAsync(u => u.Email == dto.Email))
            return BadRequest("El email ya está registrado.");

        // Añade esta validación antes de crear el objeto 'usuario'

        if (dto.EmpresaId.HasValue)
{
    var empresaExiste = await _context.Empresas.AnyAsync(e => e.Id == dto.EmpresaId);
    if (!empresaExiste)
        return BadRequest("La empresa especificada no existe.");
}

        var usuario = new Usuario
        {
            Nombre        = dto.Nombre,
            Email         = dto.Email,
            PasswordHash  = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Rol           = (RolUsuario)dto.Rol,
            EmpresaId     = dto.EmpresaId,
            EmpleadoId    = dto.EmpleadoId,
            Activo        = true,
            FechaCreacion = DateTime.UtcNow
        };

        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();

        var token = GenerarToken(usuario);
        return Ok(new AuthResponseDto
        {
            Token      = token,
            Nombre     = usuario.Nombre,
            Email      = usuario.Email,
            Rol        = usuario.Rol.ToString(),
            EmpresaId  = usuario.EmpresaId,
            Expiracion = DateTime.UtcNow.AddHours(int.Parse(_config["Jwt:ExpirationHours"]!)),
            EmpleadoId = usuario.EmpleadoId,
        });

    }

    [HttpGet("usuarios")]
    public async Task<ActionResult<IEnumerable<object>>> GetUsuarios()
    {
        return Ok(await _context.Usuarios
            .Include(u => u.Empresa)
            .Where(u => u.Activo)
            .Select(u => new
            {
                u.Id, u.Nombre, u.Email,
                rol     = u.Rol.ToString(),
                empresa = u.Empresa != null ? u.Empresa.RazonSocial : "Todas"
            })
            .ToListAsync());
    }

    private string GenerarToken(Usuario usuario)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new Claim(ClaimTypes.Name,           usuario.Nombre),
            new Claim(ClaimTypes.Email,          usuario.Email),
            new Claim(ClaimTypes.Role,           usuario.Rol.ToString()),
            new Claim("EmpresaId",               usuario.EmpresaId?.ToString() ?? ""),
            new Claim("EmpleadoId", usuario.EmpleadoId?.ToString() ?? ""),
        };

        var token = new JwtSecurityToken(
            issuer:             _config["Jwt:Issuer"],
            audience:           _config["Jwt:Audience"],
            claims:             claims,
            expires:            DateTime.UtcNow.AddHours(int.Parse(_config["Jwt:ExpirationHours"]!)),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [HttpPost("recuperar-password")]
    public async Task<ActionResult> RecuperarPassword(RecuperarPasswordDto dto)
    {
        var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == dto.Email);
        if (usuario is null)
            return Ok(new { message = "Si el correo existe, se enviarán las instrucciones." }); // Anti-enum

        // Aquí se enviaría un correo real. Por ahora, reseteamos la contraseña a "Temporal123!" y devolvemos eso.
        // En producción, solo se enviaría el correo.
        string tempPass = "Temporal123!";
        usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(tempPass);
        await _context.SaveChangesAsync();

        return Ok(new { 
            message = "Contraseña temporal generada con éxito.", 
            tempPassword = tempPass // MODO DEMO: Se expone para pruebas
        });
    }

    [HttpPost("registro-cliente")]
    public async Task<ActionResult> RegistroCliente(RegistroClienteDto dto)
    {
        if (await _context.Usuarios.AnyAsync(u => u.Email == dto.Email))
            return BadRequest("El email ya está registrado.");

        // Crear nueva empresa genérica/indicada
        var empresa = new Empresa
        {
            RazonSocial = string.IsNullOrWhiteSpace(dto.NombreEmpresa) ? "Mi Empresa" : dto.NombreEmpresa,
            RFC = "XAXX010101000",
            RegimenFiscal = "601"
        };
        _context.Empresas.Add(empresa);
        await _context.SaveChangesAsync(); // Para obtener el Id

        // Generar código de 6 dígitos
        var random = new Random();
        string codigo = random.Next(100000, 999999).ToString();

        var usuario = new Usuario
        {
            Nombre = dto.NombreUsuario,
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Rol = RolUsuario.Administrador,
            EmpresaId = empresa.Id,
            Activo = true,
            EmailVerificado = false,
            CodigoVerificacion = codigo,
            FechaCreacion = DateTime.UtcNow
        };
        _context.Usuarios.Add(usuario);

        // Vincular en Multiempresa
        var usuarioEmpresa = new UsuarioEmpresa
        {
            Usuario = usuario,
            EmpresaId = empresa.Id,
            Rol = RolEmpresa.Administrador,
            Activo = true,
            FechaAsignacion = DateTime.UtcNow
        };
        _context.UsuariosEmpresas.Add(usuarioEmpresa);
        
        await _context.SaveChangesAsync();

        // Enviar correo
        string html = $@"
        <h2>¡Bienvenido a NóminaApp!</h2>
        <p>Tu código de verificación es: <strong>{codigo}</strong></p>
        <p>Ingrésalo en la pantalla de verificación para activar tu cuenta.</p>";
        
        await _emailService.EnviarAsync(dto.Email, dto.NombreUsuario, "Verifica tu cuenta de NóminaApp", html);

        return Ok(new { message = "Cuenta creada. Revisa tu correo para obtener el código de verificación." });
    }

    [HttpPost("verificar-email")]
    public async Task<ActionResult<AuthResponseDto>> VerificarEmail(VerificarEmailDto dto)
    {
        var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == dto.Email);
        if (usuario == null) return NotFound("Usuario no encontrado.");
        if (usuario.EmailVerificado) return BadRequest("El correo ya está verificado.");
        if (usuario.CodigoVerificacion != dto.Codigo) return BadRequest("Código incorrecto.");

        usuario.EmailVerificado = true;
        usuario.CodigoVerificacion = null;
        await _context.SaveChangesAsync();

        var token = GenerarToken(usuario);
        return Ok(new AuthResponseDto
        {
            Token      = token,
            Nombre     = usuario.Nombre,
            Email      = usuario.Email,
            Rol        = usuario.Rol.ToString(),
            EmpresaId  = usuario.EmpresaId,
            Expiracion = DateTime.UtcNow.AddHours(int.Parse(_config["Jwt:ExpirationHours"]!)),
            EmpleadoId = usuario.EmpleadoId,
        });
    }
}