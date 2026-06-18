using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NominaApp.Core.Entities;
using NominaApp.Infrastructure.Data;
using NominaApp.API.DTOs;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GeneracionPeriodosController : ControllerBase
{
    private readonly NominaDbContext _context;

    public GeneracionPeriodosController(NominaDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<ActionResult<object>> Generar(GeneracionPeriodosRequestDto dto)
    {
        var empresa = await _context.Empresas.FindAsync(dto.EmpresaId);
        if (empresa is null) return NotFound("Empresa no encontrada.");

        var tipo = (TipoPeriodo)dto.TipoPeriodo;
        var periodos = GenerarPeriodos(dto.EjercicioFiscal, tipo, dto.DiasHabilesParaPago);
        var resultados = new List<PeriodoGeneradoDto>();
        int creados = 0;
        int omitidos = 0;

        foreach (var p in periodos)
        {
            var existe = await _context.PeriodosNomina.AnyAsync(x =>
                x.EmpresaId == dto.EmpresaId &&
                x.EjercicioFiscal == dto.EjercicioFiscal &&
                x.NumeroPeriodo == p.NumeroPeriodo &&
                x.TipoPeriodo == tipo);

            if (existe && !dto.SobreescribirExistentes)
            {
                resultados.Add(new PeriodoGeneradoDto
                {
                    NumeroPeriodo = p.NumeroPeriodo,
                    Descripcion   = p.Descripcion,
                    FechaInicio   = p.FechaInicio.ToString("dd/MM/yyyy"),
                    FechaFin      = p.FechaFin.ToString("dd/MM/yyyy"),
                    FechaPago     = p.FechaPago.ToString("dd/MM/yyyy"),
                    Generado      = false,
                    Mensaje       = "Ya existe"
                });
                omitidos++;
                continue;
            }

            p.EmpresaId = dto.EmpresaId;
            _context.PeriodosNomina.Add(p);
            resultados.Add(new PeriodoGeneradoDto
            {
                NumeroPeriodo = p.NumeroPeriodo,
                Descripcion   = p.Descripcion,
                FechaInicio   = p.FechaInicio.ToString("dd/MM/yyyy"),
                FechaFin      = p.FechaFin.ToString("dd/MM/yyyy"),
                FechaPago     = p.FechaPago.ToString("dd/MM/yyyy"),
                Generado      = true
            });
            creados++;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            empresa        = empresa.RazonSocial,
            ejercicio      = dto.EjercicioFiscal,
            tipoPeriodo    = tipo.ToString(),
            totalGenerados = creados,
            totalOmitidos  = omitidos,
            totalPeriodos  = periodos.Count,
            periodos       = resultados
        });
    }

    [HttpGet("preview")]
    public ActionResult<object> Preview(int ejercicio, int tipoPeriodo, int diasPago = 3)
    {
        var tipo    = (TipoPeriodo)tipoPeriodo;
        var periodos = GenerarPeriodos(ejercicio, tipo, diasPago);
        return Ok(periodos.Select(p => new PeriodoGeneradoDto
        {
            NumeroPeriodo = p.NumeroPeriodo,
            Descripcion   = p.Descripcion,
            FechaInicio   = p.FechaInicio.ToString("dd/MM/yyyy"),
            FechaFin      = p.FechaFin.ToString("dd/MM/yyyy"),
            FechaPago     = p.FechaPago.ToString("dd/MM/yyyy"),
            Generado      = false
        }));
    }

    private List<PeriodoNomina> GenerarPeriodos(int ejercicio, TipoPeriodo tipo, int diasPago)
    {
        var periodos = new List<PeriodoNomina>();
        var inicio   = new DateTime(ejercicio, 1, 1);
        var fin      = new DateTime(ejercicio, 12, 31);
        int numero   = 1;

        switch (tipo)
        {
            case TipoPeriodo.Quincenal:
                var fecha = inicio;
                while (fecha <= fin)
                {
                    var fechaFin = fecha.Day == 1
                        ? new DateTime(fecha.Year, fecha.Month, 15)
                        : new DateTime(fecha.Year, fecha.Month, DateTime.DaysInMonth(fecha.Year, fecha.Month));

                    if (fechaFin > fin) fechaFin = fin;

                    periodos.Add(new PeriodoNomina
                    {
                        NumeroPeriodo  = numero++,
                        Descripcion    = $"Quincena {numero - 1} {ejercicio}",
                        FechaInicio    = fecha,
                        FechaFin       = fechaFin,
                        FechaPago      = SiguienteDiaHabil(fechaFin.AddDays(diasPago)),
                        TipoPeriodo    = tipo,
                        EjercicioFiscal = ejercicio,
                        Estado         = EstadoPeriodo.Abierto,
                        FechaCreacion  = DateTime.UtcNow
                    });

                    fecha = fechaFin.AddDays(1);
                }
                break;

            case TipoPeriodo.Semanal:
                var sem = inicio;
                while (sem.DayOfWeek != DayOfWeek.Monday)
                    sem = sem.AddDays(1);

                while (sem <= fin)
                {
                    var semFin = sem.AddDays(6);
                    if (semFin > fin) semFin = fin;

                    periodos.Add(new PeriodoNomina
                    {
                        NumeroPeriodo  = numero++,
                        Descripcion    = $"Semana {numero - 1} {ejercicio}",
                        FechaInicio    = sem,
                        FechaFin       = semFin,
                        FechaPago      = SiguienteDiaHabil(semFin.AddDays(diasPago)),
                        TipoPeriodo    = tipo,
                        EjercicioFiscal = ejercicio,
                        Estado         = EstadoPeriodo.Abierto,
                        FechaCreacion  = DateTime.UtcNow
                    });

                    sem = semFin.AddDays(1);
                }
                break;

            case TipoPeriodo.Mensual:
                for (int mes = 1; mes <= 12; mes++)
                {
                    var mesInicio = new DateTime(ejercicio, mes, 1);
                    var mesFin    = new DateTime(ejercicio, mes, DateTime.DaysInMonth(ejercicio, mes));

                    periodos.Add(new PeriodoNomina
                    {
                        NumeroPeriodo  = numero++,
                        Descripcion    = $"{mesInicio:MMMM yyyy}",
                        FechaInicio    = mesInicio,
                        FechaFin       = mesFin,
                        FechaPago      = SiguienteDiaHabil(mesFin.AddDays(diasPago)),
                        TipoPeriodo    = tipo,
                        EjercicioFiscal = ejercicio,
                        Estado         = EstadoPeriodo.Abierto,
                        FechaCreacion  = DateTime.UtcNow
                    });
                }
                break;
        }

        return periodos;
    }

    private DateTime SiguienteDiaHabil(DateTime fecha)
    {
        while (fecha.DayOfWeek == DayOfWeek.Saturday || fecha.DayOfWeek == DayOfWeek.Sunday)
            fecha = fecha.AddDays(1);
        return fecha;
    }
}