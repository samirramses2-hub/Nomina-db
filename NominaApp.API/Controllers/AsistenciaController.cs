using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NominaApp.Core.Entities;
using NominaApp.Infrastructure.Data;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AsistenciaController : ControllerBase
{
    private readonly NominaDbContext _context;

    public AsistenciaController(NominaDbContext context)
    {
        _context = context;
    }

    // Registrar entrada
    [HttpPost("entrada")]
    public async Task<ActionResult<object>> RegistrarEntrada([FromBody] RegistroAsistenciaDto dto)
    {
        var empleado = await _context.Empleados
            .Include(e => e.Empresa)
            .FirstOrDefaultAsync(e => e.Id == dto.EmpleadoId);
        if (empleado is null) return NotFound("Empleado no encontrado.");

        var hoy = DateTime.UtcNow.Date;

        var yaRegistrado = await _context.Asistencias
            .AnyAsync(a => a.EmpleadoId == dto.EmpleadoId && a.Fecha == hoy && a.HoraEntrada != null);
        if (yaRegistrado)
            return BadRequest("Ya se registró la entrada de este empleado hoy.");

        // Obtener horario del empleado
        var horario = await _context.HorariosEmpleados
            .FirstOrDefaultAsync(h => h.EmpleadoId == dto.EmpleadoId
                                   && h.DiaSemana == DateTime.UtcNow.DayOfWeek
                                   && h.Activo);

        var horaEntradaEsperada = horario?.HoraEntrada ?? new TimeSpan(9, 0, 0);
        var horaSalidaEsperada  = horario?.HoraSalida  ?? new TimeSpan(18, 0, 0);
        var horaActual          = DateTime.UtcNow.TimeOfDay;

        var minutosRetardo = 0m;
        var estado = EstadoAsistencia.Presente;

        if (horaActual > horaEntradaEsperada.Add(TimeSpan.FromMinutes(5)))
        {
            minutosRetardo = (decimal)(horaActual - horaEntradaEsperada).TotalMinutes;
            estado = EstadoAsistencia.Retardo;
        }

        var asistencia = new Asistencia
        {
            EmpleadoId           = dto.EmpleadoId,
            Fecha                = hoy,
            HoraEntrada          = horaActual,
            HoraEntradaEsperada  = horaEntradaEsperada,
            HoraSalidaEsperada   = horaSalidaEsperada,
            Estado               = estado,
            MinutosRetardo       = Math.Round(minutosRetardo, 0),
            MetodoRegistro       = dto.Metodo ?? "Manual",
            CodigoQR             = dto.CodigoQR,
            Observaciones        = dto.Observaciones,
            FechaRegistro        = DateTime.UtcNow
        };

        _context.Asistencias.Add(asistencia);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            mensaje        = estado == EstadoAsistencia.Retardo
                ? $"Entrada registrada con {minutosRetardo:F0} minutos de retardo."
                : "Entrada registrada correctamente.",
            horaEntrada    = horaActual.ToString(@"hh\:mm"),
            estado         = estado.ToString(),
            minutosRetardo = Math.Round(minutosRetardo, 0),
            empleado       = $"{empleado.Nombre} {empleado.ApellidoPaterno}"
        });
    }

    // Registrar salida
    [HttpPost("salida")]
    public async Task<ActionResult<object>> RegistrarSalida([FromBody] RegistroAsistenciaDto dto)
    {
        var hoy = DateTime.UtcNow.Date;
        var asistencia = await _context.Asistencias
            .FirstOrDefaultAsync(a => a.EmpleadoId == dto.EmpleadoId
                                   && a.Fecha == hoy
                                   && a.HoraEntrada != null
                                   && a.HoraSalida == null);

        if (asistencia is null)
            return BadRequest("No se encontró registro de entrada para hoy.");

        var horaActual     = DateTime.UtcNow.TimeOfDay;
        var horasTrabajadas = (decimal)(horaActual - asistencia.HoraEntrada!.Value).TotalHours;
        var jornadaNormal   = (decimal)(asistencia.HoraSalidaEsperada!.Value - asistencia.HoraEntradaEsperada!.Value).TotalHours;
        var horasExtra      = Math.Max(0, Math.Round(horasTrabajadas - jornadaNormal, 2));

        asistencia.HoraSalida      = horaActual;
        asistencia.HorasTrabajadas = Math.Round(horasTrabajadas, 2);
        asistencia.HorasExtra      = horasExtra;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            mensaje        = "Salida registrada correctamente.",
            horaSalida     = horaActual.ToString(@"hh\:mm"),
            horasTrabajadas = Math.Round(horasTrabajadas, 2),
            horasExtra,
            empleado       = dto.EmpleadoId
        });
    }

    // Obtener asistencias por empresa y rango de fechas
    [HttpGet("empresa/{empresaId}")]
    public async Task<ActionResult<object>> GetByEmpresa(
        int empresaId,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta)
    {
        var fechaDesde = desde ?? DateTime.UtcNow.Date.AddDays(-30);
        var fechaHasta = hasta ?? DateTime.UtcNow.Date;

        var empleados = await _context.Empleados
            .Where(e => e.EmpresaId == empresaId && e.Activo)
            .ToListAsync();

        var asistencias = await _context.Asistencias
            .Where(a => empleados.Select(e => e.Id).Contains(a.EmpleadoId)
                     && a.Fecha >= fechaDesde && a.Fecha <= fechaHasta)
            .OrderByDescending(a => a.Fecha)
            .ToListAsync();

        var resumenEmpleados = empleados.Select(emp =>
        {
            var asEmp = asistencias.Where(a => a.EmpleadoId == emp.Id).ToList();
            return new
            {
                empleadoId     = emp.Id,
                nombre         = $"{emp.Nombre} {emp.ApellidoPaterno}".Trim(),
                codigo         = emp.CodigoEmpleado,
                presentes      = asEmp.Count(a => a.Estado == EstadoAsistencia.Presente),
                retardos       = asEmp.Count(a => a.Estado == EstadoAsistencia.Retardo),
                faltas         = asEmp.Count(a => a.Estado == EstadoAsistencia.FaltaInjust || a.Estado == EstadoAsistencia.FaltaJust),
                horasTrabajadas = Math.Round(asEmp.Sum(a => a.HorasTrabajadas), 2),
                horasExtra     = Math.Round(asEmp.Sum(a => a.HorasExtra), 2),
                minutosRetardoTotal = Math.Round(asEmp.Sum(a => a.MinutosRetardo), 0),
                porcentajeAsistencia = asEmp.Count > 0
                    ? Math.Round((decimal)asEmp.Count(a => a.Estado == EstadoAsistencia.Presente || a.Estado == EstadoAsistencia.Retardo) / asEmp.Count * 100, 1)
                    : 0m
            };
        }).ToList();

        return Ok(new
        {
            desde          = fechaDesde.ToString("dd/MM/yyyy"),
            hasta          = fechaHasta.ToString("dd/MM/yyyy"),
            totalEmpleados = empleados.Count,
            resumen = new
            {
                presentes  = asistencias.Count(a => a.Estado == EstadoAsistencia.Presente),
                retardos   = asistencias.Count(a => a.Estado == EstadoAsistencia.Retardo),
                faltas     = asistencias.Count(a => a.Estado == EstadoAsistencia.FaltaInjust),
                horasExtra = Math.Round(asistencias.Sum(a => a.HorasExtra), 2)
            },
            empleados      = resumenEmpleados,
            detalle        = asistencias.Select(a => new
            {
                a.Id,
                a.EmpleadoId,
                empleado       = empleados.FirstOrDefault(e => e.Id == a.EmpleadoId)?.Nombre + " " +
                                 empleados.FirstOrDefault(e => e.Id == a.EmpleadoId)?.ApellidoPaterno,
                fecha          = a.Fecha.ToString("dd/MM/yyyy"),
                horaEntrada    = a.HoraEntrada?.ToString(@"hh\:mm"),
                horaSalida     = a.HoraSalida?.ToString(@"hh\:mm"),
                estado         = a.Estado.ToString(),
                a.HorasTrabajadas,
                a.HorasExtra,
                a.MinutosRetardo,
                a.Observaciones,
                a.MetodoRegistro
            }).ToList()
        });
    }

    // Registrar falta
    [HttpPost("falta")]
    public async Task<ActionResult<object>> RegistrarFalta([FromBody] FaltaDto dto)
    {
        var fecha = dto.Fecha.Date;
        var existe = await _context.Asistencias
            .AnyAsync(a => a.EmpleadoId == dto.EmpleadoId && a.Fecha == fecha);
        if (existe)
            return BadRequest("Ya existe un registro para esta fecha.");

        _context.Asistencias.Add(new Asistencia
        {
            EmpleadoId    = dto.EmpleadoId,
            Fecha         = fecha,
            Estado        = dto.Justificada ? EstadoAsistencia.FaltaJust : EstadoAsistencia.FaltaInjust,
            Observaciones = dto.Motivo,
            FechaRegistro = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return Ok(new { mensaje = "Falta registrada correctamente." });
    }

    // Horarios del empleado
    [HttpGet("horario/{empleadoId}")]
    public async Task<ActionResult<object>> GetHorario(int empleadoId)
    {
        var horarios = await _context.HorariosEmpleados
            .Where(h => h.EmpleadoId == empleadoId && h.Activo)
            .OrderBy(h => h.DiaSemana)
            .ToListAsync();

        return Ok(horarios.Select(h => new
        {
            h.Id,
            dia          = h.DiaSemana.ToString(),
            horaEntrada  = h.HoraEntrada.ToString(@"hh\:mm"),
            horaSalida   = h.HoraSalida.ToString(@"hh\:mm"),
            horasJornada = Math.Round((h.HoraSalida - h.HoraEntrada).TotalHours, 1)
        }));
    }

    [HttpPost("horario")]
    public async Task<ActionResult<object>> GuardarHorario([FromBody] HorarioDto dto)
    {
        var existente = await _context.HorariosEmpleados
            .FirstOrDefaultAsync(h => h.EmpleadoId == dto.EmpleadoId
                                   && h.DiaSemana == (DayOfWeek)dto.DiaSemana);
        if (existente != null)
        {
            existente.HoraEntrada = TimeSpan.Parse(dto.HoraEntrada);
            existente.HoraSalida  = TimeSpan.Parse(dto.HoraSalida);
            existente.Activo      = true;
        }
        else
        {
            _context.HorariosEmpleados.Add(new HorarioEmpleado
            {
                EmpleadoId  = dto.EmpleadoId,
                DiaSemana   = (DayOfWeek)dto.DiaSemana,
                HoraEntrada = TimeSpan.Parse(dto.HoraEntrada),
                HoraSalida  = TimeSpan.Parse(dto.HoraSalida),
                Activo      = true
            });
        }

        await _context.SaveChangesAsync();
        return Ok(new { mensaje = "Horario guardado correctamente." });
    }

    // Generar QR para empleado
    [HttpGet("qr/{empleadoId}")]
    public ActionResult<object> GenerarQR(int empleadoId)
    {
        var codigo = $"NOMINA-EMP-{empleadoId}-{DateTime.UtcNow:yyyyMMdd}";
        var urlQR  = $"https://api.qrserver.com/v1/create-qr-code/?size=200x200&data={Uri.EscapeDataString(codigo)}";
        return Ok(new { empleadoId, codigo, urlQR });
    }

    // Convertir asistencias a incidencias de nómina
    [HttpPost("convertir-incidencias/{periodoId}")]
    public async Task<ActionResult<object>> ConvertirAIncidencias(int periodoId)
    {
        var periodo = await _context.PeriodosNomina
            .Include(p => p.Empresa)
            .FirstOrDefaultAsync(p => p.Id == periodoId);
        if (periodo is null) return NotFound();

        var empleados = await _context.Empleados
            .Where(e => e.EmpresaId == periodo.EmpresaId && e.Activo)
            .ToListAsync();

        var asistencias = await _context.Asistencias
            .Where(a => empleados.Select(e => e.Id).Contains(a.EmpleadoId)
                     && a.Fecha >= periodo.FechaInicio
                     && a.Fecha <= periodo.FechaFin)
            .ToListAsync();

        var incidenciasCreadas = 0;

        foreach (var emp in empleados)
        {
            var asEmp = asistencias.Where(a => a.EmpleadoId == emp.Id).ToList();

            var faltasInjust = asEmp.Count(a => a.Estado == EstadoAsistencia.FaltaInjust);
            var faltasJust   = asEmp.Count(a => a.Estado == EstadoAsistencia.FaltaJust);
            var horasExtra   = Math.Round(asEmp.Sum(a => a.HorasExtra), 2);

            if (faltasInjust > 0)
            {
                _context.Incidencias.Add(new Incidencia
                {
                    EmpleadoId      = emp.Id,
                    PeriodoNominaId = periodoId,
                    Tipo            = TipoIncidencia.FaltaInjustificada,
                    Cantidad        = faltasInjust,
                    Observaciones   = "Generado automáticamente desde control de asistencia",
                    FechaRegistro   = DateTime.UtcNow
                });
                incidenciasCreadas++;
            }

            if (faltasJust > 0)
            {
                _context.Incidencias.Add(new Incidencia
                {
                    EmpleadoId      = emp.Id,
                    PeriodoNominaId = periodoId,
                    Tipo            = TipoIncidencia.FaltaJustificada,
                    Cantidad        = faltasJust,
                    Observaciones   = "Generado automáticamente desde control de asistencia",
                    FechaRegistro   = DateTime.UtcNow
                });
                incidenciasCreadas++;
            }

            if (horasExtra > 0)
            {
                _context.Incidencias.Add(new Incidencia
                {
                    EmpleadoId      = emp.Id,
                    PeriodoNominaId = periodoId,
                    Tipo            = TipoIncidencia.HoraExtraSimple,
                    Cantidad        = horasExtra,
                    Observaciones   = "Horas extra generadas desde control de asistencia",
                    FechaRegistro   = DateTime.UtcNow
                });
                incidenciasCreadas++;
            }
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            mensaje              = $"{incidenciasCreadas} incidencias generadas automáticamente.",
            incidenciasCreadas,
            periodo              = $"{periodo.FechaInicio:dd/MM/yyyy} — {periodo.FechaFin:dd/MM/yyyy}"
        });
    }
}

public class RegistroAsistenciaDto
{
    public int     EmpleadoId    { get; set; }
    public string? Metodo        { get; set; }
    public string? CodigoQR      { get; set; }
    public string? Observaciones { get; set; }
}

public class FaltaDto
{
    public int      EmpleadoId  { get; set; }
    public DateTime Fecha       { get; set; }
    public bool     Justificada { get; set; }
    public string?  Motivo      { get; set; }
}

public class HorarioDto
{
    public int    EmpleadoId  { get; set; }
    public int    DiaSemana   { get; set; }
    public string HoraEntrada { get; set; } = "09:00";
    public string HoraSalida  { get; set; } = "18:00";
}