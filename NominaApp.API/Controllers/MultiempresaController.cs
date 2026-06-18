using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NominaApp.Core.Entities;
using NominaApp.Infrastructure.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using BCrypt.Net;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MultiempresaController : ControllerBase
{
    private readonly NominaDbContext _context;
    private readonly IConfiguration  _config;

    public MultiempresaController(NominaDbContext context, IConfiguration config)
    {
        _context = context;
        _config  = config;
    }

    // Obtener empresas a las que tiene acceso un usuario
    [HttpGet("mis-empresas/{usuarioId}")]
    public async Task<ActionResult<object>> GetMisEmpresas(int usuarioId)
    {
        var usuario = await _context.Usuarios.FindAsync(usuarioId);
        if (usuario is null) return NotFound();

        // Administrador ve todas
        if (usuario.Rol == RolUsuario.Administrador)
        {
            var todasEmpresas = await _context.Empresas.ToListAsync();
            return Ok(todasEmpresas.Select(e => new EmpresaAsignadaDto
            {
                Id = e.Id, 
                RazonSocial = e.RazonSocial, 
                RFC = e.RFC, 
                RegimenFiscal = e.RegimenFiscal,
                Rol = "Administrador",
                EsAdministrador = true,
                Activo = true
            }));
        }

        // Otros roles ven las asignadas
        var asignaciones = await _context.UsuariosEmpresas
            .Include(ue => ue.Empresa)
            .Where(ue => ue.UsuarioId == usuarioId && ue.Activo)
            .ToListAsync();

        // Si tiene EmpresaId en el usuario, también incluirla
        if (usuario.EmpresaId.HasValue)
        {
            var empresa = await _context.Empresas.FindAsync(usuario.EmpresaId);
            if (empresa != null && !asignaciones.Any(a => a.EmpresaId == empresa.Id))
            {
                return Ok(asignaciones.Select(a => new EmpresaAsignadaDto
                {
                    Id = a.Empresa.Id, 
                    RazonSocial = a.Empresa.RazonSocial, 
                    RFC = a.Empresa.RFC, 
                    RegimenFiscal = a.Empresa.RegimenFiscal,
                    Rol = a.Rol.ToString(),
                    EsAdministrador = a.Rol == RolEmpresa.Administrador,
                    Activo = a.Activo
                }).Append(new EmpresaAsignadaDto
                {
                    Id = empresa.Id, 
                    RazonSocial = empresa.RazonSocial, 
                    RFC = empresa.RFC, 
                    RegimenFiscal = empresa.RegimenFiscal,
                    Rol = usuario.Rol.ToString(),
                    EsAdministrador = false,
                    Activo = true
                }));
            }
        }

        return Ok(asignaciones.Select(a => new EmpresaAsignadaDto
        {
            Id = a.Empresa.Id, 
            RazonSocial = a.Empresa.RazonSocial, 
            RFC = a.Empresa.RFC, 
            RegimenFiscal = a.Empresa.RegimenFiscal,
            Rol = a.Rol.ToString(),
            EsAdministrador = a.Rol == RolEmpresa.Administrador,
            Activo = a.Activo
        }));
    }

    // Asignar empresa a usuario
    [HttpPost("asignar")]
    public async Task<ActionResult<object>> Asignar([FromBody] AsignarEmpresaDto dto)
    {
        var usuario = await _context.Usuarios.FindAsync(dto.UsuarioId);
        if (usuario is null) return NotFound("Usuario no encontrado.");

        var empresa = await _context.Empresas.FindAsync(dto.EmpresaId);
        if (empresa is null) return NotFound("Empresa no encontrada.");

        var yaExiste = await _context.UsuariosEmpresas
            .AnyAsync(ue => ue.UsuarioId == dto.UsuarioId && ue.EmpresaId == dto.EmpresaId);

        if (yaExiste)
        {
            var existente = await _context.UsuariosEmpresas
                .FirstAsync(ue => ue.UsuarioId == dto.UsuarioId && ue.EmpresaId == dto.EmpresaId);
            existente.Activo = true;
            existente.Rol    = (RolEmpresa)dto.Rol;
            await _context.SaveChangesAsync();
            return Ok(new { mensaje = "Acceso actualizado correctamente." });
        }

        _context.UsuariosEmpresas.Add(new UsuarioEmpresa
        {
            UsuarioId        = dto.UsuarioId,
            EmpresaId        = dto.EmpresaId,
            Rol              = (RolEmpresa)dto.Rol,
            Activo           = true,
            FechaAsignacion  = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return Ok(new
        {
            mensaje  = $"{usuario.Nombre} ahora tiene acceso a {empresa.RazonSocial}.",
            usuario  = usuario.Nombre,
            empresa  = empresa.RazonSocial,
            rol      = ((RolEmpresa)dto.Rol).ToString()
        });
    }

    // Revocar acceso
    [HttpDelete("revocar/{usuarioId}/{empresaId}")]
    public async Task<IActionResult> Revocar(int usuarioId, int empresaId)
    {
        var asignacion = await _context.UsuariosEmpresas
            .FirstOrDefaultAsync(ue => ue.UsuarioId == usuarioId && ue.EmpresaId == empresaId);
        if (asignacion is null) return NotFound();

        asignacion.Activo = false;
        await _context.SaveChangesAsync();
        return Ok(new { mensaje = "Acceso revocado correctamente." });
    }

    // Dashboard multiempresa — resumen de todas las empresas del usuario
    [HttpGet("dashboard/{usuarioId}")]
    public async Task<ActionResult<object>> DashboardMultiempresa(int usuarioId)
    {
        var usuario = await _context.Usuarios.FindAsync(usuarioId);
        if (usuario is null) return NotFound();

        List<int> empresaIds;
        if (usuario.Rol == RolUsuario.Administrador)
        {
            empresaIds = await _context.Empresas.Select(e => e.Id).ToListAsync();
        }
        else
        {
            empresaIds = await _context.UsuariosEmpresas
                .Where(ue => ue.UsuarioId == usuarioId && ue.Activo)
                .Select(ue => ue.EmpresaId)
                .ToListAsync();

            if (usuario.EmpresaId.HasValue && !empresaIds.Contains(usuario.EmpresaId.Value))
                empresaIds.Add(usuario.EmpresaId.Value);
        }

        var resumen = new List<object>();

        foreach (var empId in empresaIds)
        {
            var empresa = await _context.Empresas.FindAsync(empId);
            if (empresa is null) continue;

            var empleadosActivos = await _context.Empleados
                .CountAsync(e => e.EmpresaId == empId && e.Activo);

            var ultimoPeriodo = await _context.PeriodosNomina
                .Where(p => p.EmpresaId == empId)
                .OrderByDescending(p => p.FechaFin)
                .FirstOrDefaultAsync();

            var periodosAbiertos = await _context.PeriodosNomina
                .CountAsync(p => p.EmpresaId == empId && p.Estado == EstadoPeriodo.Abierto);

            var empleados = await _context.Empleados
                .Where(e => e.EmpresaId == empId && e.Activo)
                .ToListAsync();

            decimal costoEstimado = 0;
            if (ultimoPeriodo != null)
            {
                var diasPeriodo = (ultimoPeriodo.FechaFin - ultimoPeriodo.FechaInicio).Days + 1;
                foreach (var emp in empleados)
                {
                    var calculo = NominaApp.Core.Calculos.MotorCalculo.Calcular(new NominaApp.Core.Calculos.ParametrosCalculo
                    {
                        SalarioDiario = emp.SalarioDiario,
                        DiasPeriodo   = diasPeriodo,
                        EjercicioFiscal = ultimoPeriodo.EjercicioFiscal
                    });
                    var imss = NominaApp.Core.Calculos.MotorCalculo.CalcularCuotasIMSS(emp.SalarioDiario, diasPeriodo);
                    costoEstimado += calculo.TotalPercepciones + imss.TotalPatronal;
                }
            }

            // Alertas rápidas
            var sinClabe = await _context.Empleados
                .CountAsync(e => e.EmpresaId == empId && e.Activo && (e.CLABE == null || e.CLABE == ""));
            var salarioMinimo = await _context.Empleados
                .CountAsync(e => e.EmpresaId == empId && e.Activo && e.SalarioDiario < 248.93m);

            resumen.Add(new
            {
                empresaId       = empId,
                empresa         = empresa.RazonSocial,
                rfc             = empresa.RFC,
                empleadosActivos,
                periodosAbiertos,
                costoUltimoPeriodo = Math.Round(costoEstimado, 2),
                ultimoPeriodo   = ultimoPeriodo != null
                    ? $"{ultimoPeriodo.FechaInicio:dd/MM/yyyy} — {ultimoPeriodo.FechaFin:dd/MM/yyyy}"
                    : "Sin periodos",
                estadoUltimoPeriodo = ultimoPeriodo?.Estado.ToString() ?? "N/A",
                alertas = new
                {
                    sinClabe,
                    salarioMinimo,
                    total = sinClabe + salarioMinimo + (periodosAbiertos > 1 ? 1 : 0)
                }
            });
        }

        return Ok(new
        {
            usuario        = usuario.Nombre,
            totalEmpresas  = resumen.Count,
            totalEmpleados = resumen.Sum(r => (int)r.GetType().GetProperty("empleadosActivos")!.GetValue(r)!),
            costoTotal     = Math.Round(resumen.Sum(r => (decimal)r.GetType().GetProperty("costoUltimoPeriodo")!.GetValue(r)!), 2),
            empresas       = resumen
        });
    }

    // Cambiar contexto de empresa (token con empresa específica)
    [HttpPost("cambiar-empresa")]
    public async Task<ActionResult<object>> CambiarEmpresa([FromBody] CambiarEmpresaDto dto)
    {
        var usuario = await _context.Usuarios.FindAsync(dto.UsuarioId);
        if (usuario is null) return NotFound();

        // Verificar acceso
        if (usuario.Rol != RolUsuario.Administrador)
        {
            var tieneAcceso = await _context.UsuariosEmpresas
                .AnyAsync(ue => ue.UsuarioId == dto.UsuarioId
                             && ue.EmpresaId == dto.EmpresaId
                             && ue.Activo);
            if (!tieneAcceso && usuario.EmpresaId != dto.EmpresaId)
                return Forbid();
        }

        var empresa = await _context.Empresas.FindAsync(dto.EmpresaId);
        if (empresa is null) return NotFound("Empresa no encontrada.");

        // Generar token con empresa activa
        var key    = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds  = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new Claim(ClaimTypes.Name,           usuario.Nombre),
            new Claim(ClaimTypes.Email,          usuario.Email),
            new Claim(ClaimTypes.Role,           usuario.Rol.ToString()),
            new Claim("EmpresaId",               dto.EmpresaId.ToString()),
            new Claim("EmpresaActiva",           empresa.RazonSocial),
        };

        var token = new JwtSecurityToken(
            issuer:             _config["Jwt:Issuer"],
            audience:           _config["Jwt:Audience"],
            claims:             claims,
            expires:            DateTime.UtcNow.AddHours(8),
            signingCredentials: creds
        );

        return Ok(new
        {
            token          = new JwtSecurityTokenHandler().WriteToken(token),
            empresaId      = dto.EmpresaId,
            empresaActiva  = empresa.RazonSocial,
            mensaje        = $"Contexto cambiado a {empresa.RazonSocial}"
        });
    }

    // Listar todos los usuarios con sus empresas asignadas
    [HttpGet("usuarios-empresas")]
    public async Task<ActionResult<object>> GetUsuariosEmpresas()
    {
        var usuarios = await _context.Usuarios
            .Where(u => u.Activo)
            .ToListAsync();

        var asignaciones = await _context.UsuariosEmpresas
            .Include(ue => ue.Empresa)
            .Where(ue => ue.Activo)
            .ToListAsync();

        return Ok(usuarios.Select(u => new
        {
            u.Id, u.Nombre, u.Email,
            rol      = u.Rol.ToString(),
            empresas = u.Rol == RolUsuario.Administrador
                ? new[] { new { id = 0, nombre = "Todas las empresas", rol = "Administrador" } }
                : asignaciones
                    .Where(a => a.UsuarioId == u.Id)
                    .Select(a => new { id = a.EmpresaId, nombre = a.Empresa.RazonSocial, rol = a.Rol.ToString() })
                    .ToArray()
        }));
    }
}

public class AsignarEmpresaDto
{
    public int UsuarioId { get; set; }
    public int EmpresaId { get; set; }
    public int Rol       { get; set; } = 2;
}

public class CambiarEmpresaDto
{
    public int UsuarioId { get; set; }
    public int EmpresaId { get; set; }
}

public class EmpresaAsignadaDto
{
    public int Id { get; set; }
    public string RazonSocial { get; set; } = string.Empty;
    public string RFC { get; set; } = string.Empty;
    public string RegimenFiscal { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public bool EsAdministrador { get; set; }
    public bool Activo { get; set; }
}