using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NominaApp.Core.Calculos;
using NominaApp.Core.Entities;
using NominaApp.Infrastructure.Data;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PortalController : ControllerBase
{
    private readonly NominaDbContext _context;

    public PortalController(NominaDbContext context)
    {
        _context = context;
    }

    [HttpGet("{empleadoId}")]
    public async Task<ActionResult<object>> GetPortal(int empleadoId)
    {
        var empleado = await _context.Empleados
            .Include(e => e.Empresa)
            .FirstOrDefaultAsync(e => e.Id == empleadoId);
        if (empleado is null) return NotFound();

        var periodos = await _context.PeriodosNomina
            .Where(p => p.EmpresaId == empleado.EmpresaId)
            .OrderByDescending(p => p.FechaInicio)
            .Take(12)
            .ToListAsync();

        var historial = new List<object>();
        foreach (var periodo in periodos)
        {
            var incidencias = await _context.Incidencias
                .Where(i => i.EmpleadoId == empleadoId && i.PeriodoNominaId == periodo.Id)
                .ToListAsync();

            var diasPeriodo = (periodo.FechaFin - periodo.FechaInicio).Days + 1;
            var parametros  = new ParametrosCalculo
            {
                SalarioDiario        = empleado.SalarioDiario,
                DiasPeriodo          = diasPeriodo,
                EjercicioFiscal      = periodo.EjercicioFiscal,
                FaltasInjustificadas = incidencias.Where(i => i.Tipo == TipoIncidencia.FaltaInjustificada).Sum(i => i.Cantidad),
                FaltasJustificadas   = incidencias.Where(i => i.Tipo == TipoIncidencia.FaltaJustificada).Sum(i => i.Cantidad),
                DiasVacaciones       = incidencias.Where(i => i.Tipo == TipoIncidencia.Vacaciones).Sum(i => i.Cantidad),
                HorasExtraSimples    = incidencias.Where(i => i.Tipo == TipoIncidencia.HoraExtraSimple).Sum(i => i.Cantidad),
                HorasExtraDobles     = incidencias.Where(i => i.Tipo == TipoIncidencia.HoraExtraDoble).Sum(i => i.Cantidad),
                HorasExtraTriples    = incidencias.Where(i => i.Tipo == TipoIncidencia.HoraExtraTriple).Sum(i => i.Cantidad),
                Bonos                = incidencias.Where(i => i.Tipo == TipoIncidencia.Bono).Sum(i => i.Cantidad),
                DiasPrimaDominical   = incidencias.Where(i => i.Tipo == TipoIncidencia.PrimaDominical).Sum(i => i.Cantidad)
            };

            var calculo = MotorCalculo.Calcular(parametros);
            var cfdi = await _context.CFDIs.FirstOrDefaultAsync(c => c.EmpleadoId == empleadoId && c.PeriodoNominaId == periodo.Id);

            historial.Add(new
            {
                periodoId    = periodo.Id,
                descripcion  = periodo.Descripcion.Length > 0 ? periodo.Descripcion : $"{periodo.FechaInicio:dd/MM/yyyy} — {periodo.FechaFin:dd/MM/yyyy}",
                fechaPago    = periodo.FechaPago.ToString("dd/MM/yyyy"),
                estado       = periodo.Estado.ToString(),
                percepciones = calculo.TotalPercepciones,
                deducciones  = calculo.TotalDeducciones,
                neto         = calculo.NetoPagar,
                isr          = calculo.DetalleISR.ISRRetenido,
                imss         = calculo.Deducciones.FirstOrDefault(d => d.Concepto == "IMSS obrero")?.Monto ?? 0,
                cfdiId       = cfdi?.Id,
                firmado      = cfdi?.FechaFirma != null,
                fechaFirma   = cfdi?.FechaFirma?.ToString("dd/MM/yyyy HH:mm")
            });
        }

        var diasVacaciones = CalcularDiasVacaciones(empleado.FechaIngreso);
        var diasTomados    = await _context.Incidencias
            .Where(i => i.EmpleadoId == empleadoId && i.Tipo == TipoIncidencia.Vacaciones)
            .SumAsync(i => i.Cantidad);

        var solicitudesVacaciones = await _context.SolicitudesVacaciones
            .Where(s => s.EmpleadoId == empleadoId)
            .OrderByDescending(s => s.FechaSolicitud)
            .ToListAsync();

        var antiguedad  = (DateTime.UtcNow - empleado.FechaIngreso).Days;

        return Ok(new
        {
            empleado = new
            {
                empleado.Id,
                empleado.CodigoEmpleado,
                nombreCompleto   = $"{empleado.Nombre} {empleado.ApellidoPaterno} {empleado.ApellidoMaterno}".Trim(),
                empleado.RFC,
                empleado.NSS,
                empleado.SalarioDiario,
                empresa          = empleado.Empresa.RazonSocial,
                fechaIngreso     = empleado.FechaIngreso.ToString("dd/MM/yyyy"),
                antiguedadTexto  = $"{antiguedad / 365} año(s) {(antiguedad % 365) / 30} mes(es)",
                tipoContrato     = empleado.TipoContrato.ToString(),
            },
            vacaciones = new
            {
                disponibles = diasVacaciones - (int)diasTomados,
                tomados     = (int)diasTomados,
                totales     = diasVacaciones,
                solicitudes = solicitudesVacaciones.Select(s => new {
                    s.Id,
                    fechaSolicitud = s.FechaSolicitud.ToString("dd/MM/yyyy"),
                    fechaInicio = s.FechaInicio.ToString("dd/MM/yyyy"),
                    fechaFin = s.FechaFin.ToString("dd/MM/yyyy"),
                    s.DiasSolicitados,
                    estado = s.Estado.ToString(),
                    s.ComentariosRRHH
                })
            },
            historial,
            resumen = new
            {
                totalPeriodos = historial.Count,
                totalNeto     = Math.Round(historial.Sum(h => (decimal)h.GetType().GetProperty("neto")!.GetValue(h)!), 2),
                totalISR      = Math.Round(historial.Sum(h => (decimal)h.GetType().GetProperty("isr")!.GetValue(h)!), 2),
            }
        });
    }

    private int CalcularDiasVacaciones(DateTime fechaIngreso)
    {
        var anos = (DateTime.UtcNow - fechaIngreso).Days / 365;
        return anos switch
        {
            0 => 12,
            1 => 12,
            2 => 14,
            3 => 16,
            4 => 18,
            >= 5 and < 10 => 20,
            >= 10 and < 15 => 22,
            >= 15 and < 20 => 24,
            >= 20 and < 25 => 26,
            _ => 28
        };
    }

    [HttpPost("firmar/{cfdiId}")]
    public async Task<IActionResult> FirmarRecibo(int cfdiId)
    {
        var cfdi = await _context.CFDIs.FindAsync(cfdiId);
        if (cfdi is null) return NotFound("Recibo no encontrado");

        if (cfdi.FechaFirma != null)
            return BadRequest("El recibo ya fue firmado anteriormente.");

        cfdi.FechaFirma = DateTime.UtcNow;
        cfdi.IPFirma = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Desconocida";
        
        await _context.SaveChangesAsync();
        return Ok(new { message = "Recibo firmado correctamente." });
    }
}