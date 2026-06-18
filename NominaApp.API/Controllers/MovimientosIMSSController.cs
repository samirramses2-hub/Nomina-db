using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NominaApp.Core.Calculos;
using NominaApp.Core.Entities;
using NominaApp.Infrastructure.Data;
using NominaApp.API.DTOs;
using System.Text;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MovimientosIMSSController : ControllerBase
{
    private readonly NominaDbContext _context;

    public MovimientosIMSSController(NominaDbContext context)
    {
        _context = context;
    }

    [HttpGet("empresa/{empresaId}")]
    public async Task<ActionResult<object>> GetByEmpresa(int empresaId)
    {
        var movimientos = await _context.MovimientosIMSS
            .Include(m => m.Empleado)
            .Where(m => m.EmpresaId == empresaId)
            .OrderByDescending(m => m.FechaMovimiento)
            .ToListAsync();

        return Ok(movimientos.Select(m => new
        {
            m.Id,
            m.EmpleadoId,
            nombreEmpleado      = $"{m.Empleado.Nombre} {m.Empleado.Apellido​Paterno}",
            m.Empleado.NSS,
            m.Empleado.RFC,
            tipoMovimiento      = m.TipoMovimiento.ToString(),
            tipoMovimientoNum   = (int)m.TipoMovimiento,
            fechaMovimiento     = m.FechaMovimiento.ToString("dd/MM/yyyy"),
            m.SalarioDiarioIntegrado,
            estado              = m.Estado.ToString(),
            m.Observaciones,
            fechaCreacion       = m.FechaCreacion.ToString("dd/MM/yyyy HH:mm")
        }));
    }

    [HttpPost]
    public async Task<ActionResult<object>> Crear(CrearMovimientoIMSSDto dto)
    {
        var empleado = await _context.Empleados
            .Include(e => e.Empresa)
            .FirstOrDefaultAsync(e => e.Id == dto.EmpleadoId);
        if (empleado is null) return NotFound("Empleado no encontrado.");

        var imss = MotorCalculo.CalcularCuotasIMSS(dto.SalarioDiarioIntegrado, 15);

        var movimiento = new MovimientoIMSS
        {
            EmpleadoId             = dto.EmpleadoId,
            EmpresaId              = empleado.EmpresaId,
            TipoMovimiento         = (TipoMovimientoIMSS)dto.TipoMovimiento,
            FechaMovimiento        = dto.FechaMovimiento,
            SalarioDiarioIntegrado = dto.SalarioDiarioIntegrado,
            Observaciones          = dto.Observaciones,
            Estado                 = EstadoMovimiento.Pendiente,
            FechaCreacion          = DateTime.UtcNow
        };

        // Si es alta automática actualizar empleado como activo
        if (dto.TipoMovimiento == (int)TipoMovimientoIMSS.Alta)
        {
            empleado.Activo = true;
            empleado.FechaIngreso = dto.FechaMovimiento;
        }
        // Si es baja marcar empleado como inactivo
        else if (dto.TipoMovimiento == (int)TipoMovimientoIMSS.Baja)
        {
            empleado.Activo = false;
        }
        // Si es modificación de salario actualizar
        else if (dto.TipoMovimiento == (int)TipoMovimientoIMSS.ModificacionSalario)
        {
            empleado.SalarioDiario = dto.SalarioDiarioIntegrado;
        }

        _context.MovimientosIMSS.Add(movimiento);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            movimiento.Id,
            mensaje        = $"Movimiento {movimiento.TipoMovimiento} registrado correctamente.",
            sbc            = imss.SBC,
            factorInteg    = imss.FactorIntegracion
        });
    }

    [HttpPatch("{id}/reportado")]
    public async Task<IActionResult> MarcarReportado(int id)
    {
        var mov = await _context.MovimientosIMSS.FindAsync(id);
        if (mov is null) return NotFound();
        mov.Estado = EstadoMovimiento.Reportado;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("generar-idse/{empresaId}")]
    public async Task<IActionResult> GenerarIDSE(int empresaId, [FromQuery] bool soloPendientes = true)
    {
        var empresa = await _context.Empresas.FindAsync(empresaId);
        if (empresa is null) return NotFound();

        var query = _context.MovimientosIMSS
            .Include(m => m.Empleado)
            .Where(m => m.EmpresaId == empresaId);

        if (soloPendientes)
            query = query.Where(m => m.Estado == EstadoMovimiento.Pendiente);

        var movimientos = await query
            .OrderBy(m => m.FechaMovimiento)
            .ToListAsync();

        var sb = new StringBuilder();

        // Encabezado IDSE
        sb.AppendLine("REGISTRO_PATRONAL|RFC_PATRON|RAZON_SOCIAL|NSS|RFC_EMPLEADO|NOMBRE|TIPO_MOVIMIENTO|FECHA|SDI|SALARIO_MINIMO");

        foreach (var m in movimientos)
        {
            var tipoNum = m.TipoMovimiento switch
            {
                TipoMovimientoIMSS.Alta                => "08",
                TipoMovimientoIMSS.Baja                => "02",
                TipoMovimientoIMSS.ModificacionSalario => "07",
                TipoMovimientoIMSS.ReingresoPorBaja    => "08",
                TipoMovimientoIMSS.AusenciaSinGoce     => "11",
                TipoMovimientoIMSS.RegresoAusencia     => "12",
                _ => "08"
            };

            sb.AppendLine(
                $"A1234567890|" +
                $"{empresa.RFC}|" +
                $"{empresa.RazonSocial}|" +
                $"{m.Empleado.NSS}|" +
                $"{m.Empleado.RFC}|" +
                $"{m.Empleado.Nombre} {m.Empleado.ApellidoPaterno} {m.Empleado.ApellidoMaterno}|" +
                $"{tipoNum}|" +
                $"{m.FechaMovimiento:yyyyMMdd}|" +
                $"{m.SalarioDiarioIntegrado:F2}|" +
                $"248.93"
            );
        }

        var bytes    = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"IDSE_{empresa.RFC}_{DateTime.Now:yyyyMMdd}.txt";

        // Marcar como reportados
        if (soloPendientes)
        {
            foreach (var m in movimientos)
                m.Estado = EstadoMovimiento.Reportado;
            await _context.SaveChangesAsync();
        }

        return File(bytes, "text/plain", fileName);
    }

    [HttpGet("alertas/{empresaId}")]
    public async Task<ActionResult<object>> GetAlertas(int empresaId)
    {
        var empleados = await _context.Empleados
            .Where(e => e.EmpresaId == empresaId && e.Activo)
            .ToListAsync();

        var movimientos = await _context.MovimientosIMSS
            .Where(m => m.EmpresaId == empresaId)
            .ToListAsync();

        var alertas = new List<object>();

        foreach (var emp in empleados)
        {
            var tieneAlta = movimientos.Any(m =>
                m.EmpleadoId == emp.Id &&
                m.TipoMovimiento == TipoMovimientoIMSS.Alta);

            if (!tieneAlta)
                alertas.Add(new
                {
                    tipo     = "warning",
                    empleado = $"{emp.Nombre} {emp.ApellidoPaterno}",
                    mensaje  = "No tiene movimiento de ALTA registrado en el sistema.",
                    empleadoId = emp.Id
                });

            // SBC desactualizado — si el salario cambió hace más de 30 días sin movimiento
            var ultimoMov = movimientos
                .Where(m => m.EmpleadoId == emp.Id)
                .OrderByDescending(m => m.FechaMovimiento)
                .FirstOrDefault();

            if (ultimoMov != null &&
                ultimoMov.SalarioDiarioIntegrado != emp.SalarioDiario &&
                ultimoMov.TipoMovimiento != TipoMovimientoIMSS.ModificacionSalario)
            {
                alertas.Add(new
                {
                    tipo     = "danger",
                    empleado = $"{emp.Nombre} {emp.ApellidoPaterno}",
                    mensaje  = $"El SBC registrado (${ultimoMov.SalarioDiarioIntegrado:F2}) difiere del salario actual (${emp.SalarioDiario:F2}). Reportar modificación al IMSS.",
                    empleadoId = emp.Id
                });
            }
        }

        var pendientes = movimientos.Count(m => m.Estado == EstadoMovimiento.Pendiente);

        return Ok(new
        {
            pendientes,
            alertas
        });
    }
}