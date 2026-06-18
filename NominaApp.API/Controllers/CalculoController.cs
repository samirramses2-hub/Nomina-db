using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NominaApp.Core.Calculos;
using NominaApp.Core.Entities;
using NominaApp.Infrastructure.Data;
using NominaApp.API.Reportes;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using static NominaApp.Core.Calculos.MotorCalculo;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CalculoController : ControllerBase
{
    private readonly NominaDbContext _context;

    public CalculoController(NominaDbContext context)
    {
        _context = context;
    }

    [HttpGet("simular/{empleadoId}/{periodoId}")]
public async Task<ActionResult<ResultadoCalculo>> Simular(int empleadoId, int periodoId)
{
    var empleado = await _context.Empleados.FindAsync(empleadoId);
    if (empleado is null) return NotFound("Empleado no encontrado.");

    var periodo = await _context.PeriodosNomina.FindAsync(periodoId);
    if (periodo is null) return NotFound("Periodo no encontrado.");

    if (empleado.EmpresaId != periodo.EmpresaId)
        return BadRequest("El empleado no pertenece a la empresa del periodo.");

    var incidencias = await _context.Incidencias
        .Where(i => i.EmpleadoId == empleadoId && i.PeriodoNominaId == periodoId)
        .ToListAsync();

    var diasPeriodo = (periodo.FechaFin - periodo.FechaInicio).Days + 1;

    var parametros = new ParametrosCalculo
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
        DiasPrimaDominical   = incidencias.Where(i => i.Tipo == TipoIncidencia.PrimaDominical).Sum(i => i.Cantidad),
        IncapacidadIMSS       = incidencias.Where(i => i.Tipo == TipoIncidencia.IncapacidadIMSS).Sum(i => i.Cantidad),
        IncapacidadRiesgo     = incidencias.Where(i => i.Tipo == TipoIncidencia.IncapacidadRiesgo).Sum(i => i.Cantidad),
        IncapacidadMaternidad = incidencias.Where(i => i.Tipo == TipoIncidencia.IncapacidadMaternidad).Sum(i => i.Cantidad),
        LicenciaSinGoce       = incidencias.Where(i => i.Tipo == TipoIncidencia.LicenciaSinGoce).Sum(i => i.Cantidad),
        PrimaVacacional       = incidencias.Where(i => i.Tipo == TipoIncidencia.PrimaVacacional).Sum(i => i.Cantidad),
        Aguinaldo             = incidencias.Where(i => i.Tipo == TipoIncidencia.Aguinaldo).Sum(i => i.Cantidad),
        DescuentoInfonavit    = incidencias.Where(i => i.Tipo == TipoIncidencia.DescuentoInfonavit).Sum(i => i.Cantidad),
    };

    try
    {
        var resultado = MotorCalculo.Calcular(parametros);
        return Ok(resultado);
    }
    catch (ArgumentException ex)
    {
        return BadRequest(ex.Message);
    }
}
    [HttpGet("recibo/{empleadoId}/{periodoId}")]
public async Task<IActionResult> GenerarRecibo(int empleadoId, int periodoId)
{
    var empleado = await _context.Empleados
        .Include(e => e.Empresa)
        .FirstOrDefaultAsync(e => e.Id == empleadoId);
    if (empleado is null) return NotFound("Empleado no encontrado.");

    var periodo = await _context.PeriodosNomina.FindAsync(periodoId);
    if (periodo is null) return NotFound("Periodo no encontrado.");

    var incidencias = await _context.Incidencias
        .Where(i => i.EmpleadoId == empleadoId && i.PeriodoNominaId == periodoId)
        .ToListAsync();

    var diasPeriodo = (periodo.FechaFin - periodo.FechaInicio).Days + 1;

    var parametros = new ParametrosCalculo
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
        DiasPrimaDominical   = incidencias.Where(i => i.Tipo == TipoIncidencia.PrimaDominical).Sum(i => i.Cantidad),
        IncapacidadIMSS       = incidencias.Where(i => i.Tipo == TipoIncidencia.IncapacidadIMSS).Sum(i => i.Cantidad),
        IncapacidadRiesgo     = incidencias.Where(i => i.Tipo == TipoIncidencia.IncapacidadRiesgo).Sum(i => i.Cantidad),
        IncapacidadMaternidad = incidencias.Where(i => i.Tipo == TipoIncidencia.IncapacidadMaternidad).Sum(i => i.Cantidad),
        LicenciaSinGoce       = incidencias.Where(i => i.Tipo == TipoIncidencia.LicenciaSinGoce).Sum(i => i.Cantidad),
        PrimaVacacional       = incidencias.Where(i => i.Tipo == TipoIncidencia.PrimaVacacional).Sum(i => i.Cantidad),
        Aguinaldo             = incidencias.Where(i => i.Tipo == TipoIncidencia.Aguinaldo).Sum(i => i.Cantidad),
        DescuentoInfonavit    = incidencias.Where(i => i.Tipo == TipoIncidencia.DescuentoInfonavit).Sum(i => i.Cantidad),
    };

    var calculo = MotorCalculo.Calcular(parametros);

    QuestPDF.Settings.License = LicenseType.Community;

    var documento = new ReciboNominaDocument(empleado, empleado.Empresa, periodo, calculo);
    var pdf = documento.GeneratePdf();

    return File(pdf, "application/pdf", $"recibo_{empleadoId}_{periodoId}.pdf");
}
[HttpGet("imss/{empleadoId}/{periodoId}")]
public async Task<ActionResult<CuotasIMSS>> CalcularIMSS(int empleadoId, int periodoId)
{
    var empleado = await _context.Empleados.FindAsync(empleadoId);
    if (empleado is null) return NotFound("Empleado no encontrado.");

    var periodo = await _context.PeriodosNomina.FindAsync(periodoId);
    if (periodo is null) return NotFound("Periodo no encontrado.");

    var diasPeriodo = (periodo.FechaFin - periodo.FechaInicio).Days + 1;

    var cuotas = MotorCalculo.CalcularCuotasIMSS(
        empleado.SalarioDiario,
        diasPeriodo
    );

    return Ok(cuotas);
}
}