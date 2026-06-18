using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NominaApp.Core.Calculos;
using NominaApp.Core.Entities;
using NominaApp.Infrastructure.Data;
using NominaApp.API.DTOs;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProcesamientoController : ControllerBase
{
    private readonly NominaDbContext _context;

    public ProcesamientoController(NominaDbContext context)
    {
        _context = context;
    }

    [HttpGet("calcular/{periodoId}")]
    public async Task<ActionResult<ProcesamientoMasivoResultDto>> CalcularMasivo(int periodoId)
    {
        var periodo = await _context.PeriodosNomina
            .Include(p => p.Empresa)
            .FirstOrDefaultAsync(p => p.Id == periodoId);
        if (periodo is null) return NotFound("Periodo no encontrado.");

        var empleados = await _context.Empleados
            .Where(e => e.EmpresaId == periodo.EmpresaId && e.Activo)
            .OrderBy(e => e.CodigoEmpleado)
            .ToListAsync();

        var incidencias = await _context.Incidencias
            .Where(i => i.PeriodoNominaId == periodoId)
            .ToListAsync();

        var diasPeriodo = (periodo.FechaFin - periodo.FechaInicio).Days + 1;
        var resultado   = new ProcesamientoMasivoResultDto
        {
            PeriodoId      = periodoId,
            Periodo        = $"{periodo.FechaInicio:dd/MM/yyyy} — {periodo.FechaFin:dd/MM/yyyy}",
            Empresa        = periodo.Empresa.RazonSocial,
            TotalEmpleados = empleados.Count
        };

        foreach (var emp in empleados)
        {
            var incEmp = incidencias.Where(i => i.EmpleadoId == emp.Id).ToList();
            var parametros = new ParametrosCalculo
            {
                SalarioDiario        = emp.SalarioDiario,
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
            var imss    = MotorCalculo.CalcularCuotasIMSS(emp.SalarioDiario, diasPeriodo);

            resultado.Empleados.Add(new ResultadoEmpleadoDto
            {
                EmpleadoId       = emp.Id,
                CodigoEmpleado   = emp.CodigoEmpleado,
                NombreCompleto   = $"{emp.Nombre} {emp.ApellidoPaterno} {emp.ApellidoMaterno}".Trim(),
                RFC              = emp.RFC,
                TotalPercepciones = calculo.TotalPercepciones,
                TotalDeducciones  = calculo.TotalDeducciones,
                NetoPagar        = calculo.NetoPagar,
                CostoEmpresa     = Math.Round(calculo.TotalPercepciones + imss.TotalPatronal, 2),
                Percepciones     = calculo.Percepciones.Select(p => new LineaDto
                {
                    Concepto    = p.Concepto,
                    Monto       = p.Monto,
                    Explicacion = p.Explicacion
                }).ToList(),
                Deducciones = calculo.Deducciones.Select(d => new LineaDto
                {
                    Concepto    = d.Concepto,
                    Monto       = d.Monto,
                    Explicacion = d.Explicacion
                }).ToList(),
                DetalleISR = calculo.DetalleISR.Explicacion,
                Estado     = "calculado"
            });
        }

        resultado.TotalNeto         = Math.Round(resultado.Empleados.Sum(e => e.NetoPagar), 2);
        resultado.TotalCostoEmpresa = Math.Round(resultado.Empleados.Sum(e => e.CostoEmpresa), 2);

        return Ok(resultado);
    }
}