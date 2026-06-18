using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NominaApp.Core.Entities;
using NominaApp.Infrastructure.Data;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VacacionesController : ControllerBase
{
    private readonly NominaDbContext _context;

    public VacacionesController(NominaDbContext context)
    {
        _context = context;
    }

    [HttpGet("empresa/{empresaId}")]
    public async Task<ActionResult<object>> GetByEmpresa(int empresaId)
    {
        var empleados = await _context.Empleados
            .Where(e => e.EmpresaId == empresaId && e.Activo)
            .ToListAsync();

        var resultados = new List<object>();

        foreach (var emp in empleados)
        {
            var antiguedad   = (DateTime.UtcNow - emp.FechaIngreso).Days;
            var anosCompletos = antiguedad / 365;
            var diasCorresponden = CalcularDiasVacaciones(anosCompletos);

            var diasTomados = (await _context.Incidencias
    .Where(i => i.EmpleadoId == emp.Id && i.Tipo == TipoIncidencia.Vacaciones)
    .ToListAsync())
    .Sum(i => i.Cantidad);
            var diasDisponibles = diasCorresponden - (int)diasTomados;
            var porcentajeUsado = diasCorresponden > 0 ? Math.Round((decimal)diasTomados / diasCorresponden * 100, 1) : 0;

            resultados.Add(new
            {
                empleadoId      = emp.Id,
                nombre          = $"{emp.Nombre} {emp.ApellidoPaterno} {emp.ApellidoMaterno}".Trim(),
                codigoEmpleado  = emp.CodigoEmpleado,
                fechaIngreso    = emp.FechaIngreso.ToString("dd/MM/yyyy"),
                antiguedadDias  = antiguedad,
                anosCompletos,
                diasCorresponden,
                diasTomados     = (int)diasTomados,
                diasDisponibles,
                porcentajeUsado,
                alerta          = diasDisponibles <= 0 ? "agotado" :
                                  diasDisponibles <= 3 ? "critico" :
                                  diasDisponibles <= 6 ? "bajo" : "ok",
                proximoAumento  = new DateTime(emp.FechaIngreso.Year + anosCompletos + 1, emp.FechaIngreso.Month, emp.FechaIngreso.Day).ToString("dd/MM/yyyy"),
                diasProximoAnio = CalcularDiasVacaciones(anosCompletos + 1)
            });
        }

        var alertas = resultados.Count(r => (string)r.GetType().GetProperty("alerta")!.GetValue(r)! != "ok");

        return Ok(new
        {
            totalEmpleados = empleados.Count,
            alertas,
            empleados = resultados.OrderBy(r => (string)r.GetType().GetProperty("alerta")!.GetValue(r)!).ToList()
        });
    }

    [HttpGet("empleado/{empleadoId}")]
    public async Task<ActionResult<object>> GetByEmpleado(int empleadoId)
    {
        var emp = await _context.Empleados
            .Include(e => e.Empresa)
            .FirstOrDefaultAsync(e => e.Id == empleadoId);
        if (emp is null) return NotFound();

        var antiguedad    = (DateTime.UtcNow - emp.FechaIngreso).Days;
        var anosCompletos = antiguedad / 365;

        var historico = new List<object>();
        for (int i = 0; i <= anosCompletos; i++)
        {
            var diasAnio = CalcularDiasVacaciones(i);
            var inicio   = emp.FechaIngreso.AddYears(i);
            var fin      = emp.FechaIngreso.AddYears(i + 1).AddDays(-1);

            var tomadosAnio = (await _context.Incidencias
                .Include(inc => inc.PeriodoNomina)
                .Where(inc => inc.EmpleadoId == emp.Id &&
                              inc.Tipo == TipoIncidencia.Vacaciones &&
                              inc.PeriodoNomina.FechaInicio >= inicio &&
                              inc.PeriodoNomina.FechaFin <= fin)
                .ToListAsync())
                .Sum(inc => inc.Cantidad);

            historico.Add(new
            {
                anio             = i + 1,
                periodo          = $"{inicio:dd/MM/yyyy} — {fin:dd/MM/yyyy}",
                diasCorresponden = diasAnio,
                diasTomados      = (int)tomadosAnio,
                diasDisponibles  = diasAnio - (int)tomadosAnio,
                vencimiento      = fin.ToString("dd/MM/yyyy")
            });
        }

        var totalTomados      = (await _context.Incidencias
            .Where(i => i.EmpleadoId == emp.Id && i.Tipo == TipoIncidencia.Vacaciones)
            .ToListAsync())
            .Sum(i => i.Cantidad);
        var diasActuales      = CalcularDiasVacaciones(anosCompletos);
        var diasDisponibles   = diasActuales - (int)totalTomados;

        return Ok(new
        {
            empleado = new
            {
                emp.Id,
                nombre          = $"{emp.Nombre} {emp.ApellidoPaterno}".Trim(),
                emp.CodigoEmpleado,
                empresa         = emp.Empresa.RazonSocial,
                fechaIngreso    = emp.FechaIngreso.ToString("dd/MM/yyyy"),
                anosCompletos,
                antiguedadTexto = $"{anosCompletos} año(s)"
            },
            resumen = new
            {
                diasActuales,
                totalTomados    = (int)totalTomados,
                diasDisponibles,
                proximoAnio     = CalcularDiasVacaciones(anosCompletos + 1),
                fechaProximoAumento = emp.FechaIngreso.AddYears(anosCompletos + 1).ToString("dd/MM/yyyy")
            },
            historico
        });
    }

    [HttpGet("solicitudes/{empresaId}")]
    public async Task<ActionResult<IEnumerable<object>>> GetSolicitudesPorEmpresa(int empresaId)
    {
        var solicitudes = await _context.SolicitudesVacaciones
            .Include(s => s.Empleado)
            .Where(s => s.Empleado.EmpresaId == empresaId)
            .OrderByDescending(s => s.FechaSolicitud)
            .Select(s => new {
                s.Id,
                empleado = s.Empleado.Nombre + " " + s.Empleado.ApellidoPaterno,
                s.EmpleadoId,
                fechaSolicitud = s.FechaSolicitud.ToString("dd/MM/yyyy HH:mm"),
                fechaInicio = s.FechaInicio.ToString("yyyy-MM-dd"),
                fechaFin = s.FechaFin.ToString("yyyy-MM-dd"),
                s.DiasSolicitados,
                estado = s.Estado.ToString(),
                s.ComentariosEmpleado,
                s.ComentariosRRHH
            })
            .ToListAsync();

        return Ok(solicitudes);
    }

    public class SolicitudDto
    {
        public int EmpleadoId { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public int DiasSolicitados { get; set; }
        public string? Comentarios { get; set; }
    }

    [HttpPost("solicitar")]
    public async Task<IActionResult> Solicitar([FromBody] SolicitudDto dto)
    {
        var solicitud = new SolicitudVacaciones
        {
            EmpleadoId = dto.EmpleadoId,
            FechaSolicitud = DateTime.UtcNow,
            FechaInicio = dto.FechaInicio,
            FechaFin = dto.FechaFin,
            DiasSolicitados = dto.DiasSolicitados,
            Estado = EstadoSolicitudVacaciones.Pendiente,
            ComentariosEmpleado = dto.Comentarios
        };

        _context.SolicitudesVacaciones.Add(solicitud);
        await _context.SaveChangesAsync();
        return Ok(solicitud);
    }

    public class ResolucionDto
    {
        public bool Aprobar { get; set; }
        public string? ComentariosRRHH { get; set; }
    }

    [HttpPut("solicitudes/{id}/resolver")]
    public async Task<IActionResult> Resolver(int id, [FromBody] ResolucionDto dto)
    {
        var solicitud = await _context.SolicitudesVacaciones
            .Include(s => s.Empleado)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (solicitud == null) return NotFound();

        solicitud.Estado = dto.Aprobar ? EstadoSolicitudVacaciones.Aprobada : EstadoSolicitudVacaciones.Rechazada;
        solicitud.ComentariosRRHH = dto.ComentariosRRHH;

        if (dto.Aprobar)
        {
            var periodo = await _context.PeriodosNomina
                .OrderByDescending(p => p.FechaInicio)
                .FirstOrDefaultAsync(p => p.EmpresaId == solicitud.Empleado.EmpresaId);

            if (periodo != null)
            {
                var incidencia = new Incidencia
                {
                    EmpleadoId = solicitud.EmpleadoId,
                    PeriodoNominaId = periodo.Id,
                    Tipo = TipoIncidencia.Vacaciones,
                    Cantidad = solicitud.DiasSolicitados,
                    FechaRegistro = DateTime.UtcNow,
                    Observaciones = "Aprobado desde Portal"
                };
                _context.Incidencias.Add(incidencia);
            }
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Solicitud resuelta" });
    }

    private int CalcularDiasVacaciones(int anos) => anos switch
    {
        0 or 1 => 12,
        2      => 14,
        3      => 16,
        4      => 18,
        >= 5  and < 10  => 20,
        >= 10 and < 15  => 22,
        >= 15 and < 20  => 24,
        >= 20 and < 25  => 26,
        _               => 28
    };
}