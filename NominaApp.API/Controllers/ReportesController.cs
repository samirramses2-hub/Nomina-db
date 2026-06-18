using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NominaApp.Core.Calculos;
using NominaApp.Core.Entities;
using NominaApp.Infrastructure.Data;
using NominaApp.API.DTOs;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportesController : ControllerBase
{
    private readonly NominaDbContext _context;

    public ReportesController(NominaDbContext context)
    {
        _context = context;
    }

    [HttpGet("nomina/{periodoId}")]
    public async Task<ActionResult<ReporteNominaDto>> GetReporteNomina(int periodoId)
    {
        var periodo = await _context.PeriodosNomina
            .Include(p => p.Empresa)
            .FirstOrDefaultAsync(p => p.Id == periodoId);

        if (periodo is null) return NotFound("Periodo no encontrado.");

        var empleados = await _context.Empleados
            .Where(e => e.EmpresaId == periodo.EmpresaId && e.Activo)
            .ToListAsync();

        var incidencias = await _context.Incidencias
            .Where(i => i.PeriodoNominaId == periodoId)
            .ToListAsync();

        var diasPeriodo = (periodo.FechaFin - periodo.FechaInicio).Days + 1;
        var reporte = new ReporteNominaDto
        {
            PeriodoId          = periodoId,
            PeriodoDescripcion = $"{periodo.FechaInicio:dd/MM/yyyy} — {periodo.FechaFin:dd/MM/yyyy}",
            EmpresaRazonSocial = periodo.Empresa.RazonSocial,
            TotalEmpleados     = empleados.Count
        };

        foreach (var empleado in empleados)
        {
            var incEmp = incidencias.Where(i => i.EmpleadoId == empleado.Id).ToList();

            var parametros = new ParametrosCalculo
            {
                SalarioDiario        = empleado.SalarioDiario,
                DiasPeriodo          = diasPeriodo,
                EjercicioFiscal      = periodo.EjercicioFiscal,
                FaltasInjustificadas = incEmp.Where(i => i.Tipo == TipoIncidencia.FaltaInjustificada).Sum(i => i.Cantidad),
                FaltasJustificadas   = incEmp.Where(i => i.Tipo == TipoIncidencia.FaltaJustificada).Sum(i => i.Cantidad),
                DiasVacaciones       = incEmp.Where(i => i.Tipo == TipoIncidencia.Vacaciones).Sum(i => i.Cantidad),
                HorasExtraSimples    = incEmp.Where(i => i.Tipo == TipoIncidencia.HoraExtraSimple).Sum(i => i.Cantidad),
                HorasExtraDobles     = incEmp.Where(i => i.Tipo == TipoIncidencia.HoraExtraDoble).Sum(i => i.Cantidad),
                HorasExtraTriples    = incEmp.Where(i => i.Tipo == TipoIncidencia.HoraExtraTriple).Sum(i => i.Cantidad),
                Bonos                = incEmp.Where(i => i.Tipo == TipoIncidencia.Bono).Sum(i => i.Cantidad),
                DiasPrimaDominical   = incEmp.Where(i => i.Tipo == TipoIncidencia.PrimaDominical).Sum(i => i.Cantidad),
                IncapacidadIMSS       = incidencias.Where(i => i.Tipo == TipoIncidencia.IncapacidadIMSS).Sum(i => i.Cantidad),
IncapacidadRiesgo     = incidencias.Where(i => i.Tipo == TipoIncidencia.IncapacidadRiesgo).Sum(i => i.Cantidad),
IncapacidadMaternidad = incidencias.Where(i => i.Tipo == TipoIncidencia.IncapacidadMaternidad).Sum(i => i.Cantidad),
LicenciaSinGoce       = incidencias.Where(i => i.Tipo == TipoIncidencia.LicenciaSinGoce).Sum(i => i.Cantidad),
PrimaVacacional       = incidencias.Where(i => i.Tipo == TipoIncidencia.PrimaVacacional).Sum(i => i.Cantidad),
Aguinaldo             = incidencias.Where(i => i.Tipo == TipoIncidencia.Aguinaldo).Sum(i => i.Cantidad),
DescuentoInfonavit    = incidencias.Where(i => i.Tipo == TipoIncidencia.DescuentoInfonavit).Sum(i => i.Cantidad),
            };

            var calculo = MotorCalculo.Calcular(parametros);
            var imss    = MotorCalculo.CalcularCuotasIMSS(empleado.SalarioDiario, diasPeriodo);

            var reporteEmp = new ReporteEmpleadoDto
            {
                NombreCompleto = $"{empleado.Nombre} {empleado.ApellidoPaterno} {empleado.ApellidoMaterno}".Trim(),
                RFC            = empleado.RFC,
                SalarioDiario  = empleado.SalarioDiario,
                Percepciones   = calculo.TotalPercepciones,
                Deducciones    = calculo.TotalDeducciones,
                Neto           = calculo.NetoPagar,
                IMSSObrero     = imss.TotalObrero,
                IMSSPatronal   = imss.TotalPatronal,
                CostoEmpleado  = Math.Round(calculo.TotalPercepciones + imss.TotalPatronal, 2)
            };

            reporte.Empleados.Add(reporteEmp);
        }

        reporte.TotalPercepciones  = Math.Round(reporte.Empleados.Sum(e => e.Percepciones), 2);
        reporte.TotalDeducciones   = Math.Round(reporte.Empleados.Sum(e => e.Deducciones), 2);
        reporte.TotalNeto          = Math.Round(reporte.Empleados.Sum(e => e.Neto), 2);
        reporte.TotalIMSSObrero    = Math.Round(reporte.Empleados.Sum(e => e.IMSSObrero), 2);
        reporte.TotalIMSSPatronal  = Math.Round(reporte.Empleados.Sum(e => e.IMSSPatronal), 2);
        reporte.CostoTotalEmpresa  = Math.Round(reporte.Empleados.Sum(e => e.CostoEmpleado), 2);

        return Ok(reporte);
    }
}