using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NominaApp.Core.Calculos;
using NominaApp.Core.Entities;
using NominaApp.Infrastructure.Data;
using System.Text;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PagosController : ControllerBase
{
    private readonly NominaDbContext _context;

    public PagosController(NominaDbContext context)
    {
        _context = context;
    }

    [HttpGet("layout/{periodoId}")]
    public async Task<IActionResult> GenerarLayout(int periodoId)
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
        var sb = new StringBuilder();

        // Encabezado del layout SPEI
        sb.AppendLine("TIPO_PAGO|FECHA_PAGO|RFC_ORDENANTE|NOMBRE_ORDENANTE|CUENTA_ORDENANTE|RFC_BENEFICIARIO|NOMBRE_BENEFICIARIO|CLABE_BENEFICIARIO|BANCO_BENEFICIARIO|MONTO|CONCEPTO|REFERENCIA");

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
                DiasPrimaDominical   = incEmp.Where(i => i.Tipo == TipoIncidencia.PrimaDominical).Sum(i => i.Cantidad)
            };

            var calculo = MotorCalculo.Calcular(parametros);
            var clabe   = empleado.CLABE ?? "000000000000000000";
            var banco   = empleado.Banco ?? "BANAMEX";
            var nombre  = $"{empleado.Nombre} {empleado.ApellidoPaterno} {empleado.ApellidoMaterno}".Trim();

            sb.AppendLine(
                $"SPEI|" +
                $"{periodo.FechaFin:yyyyMMdd}|" +
                $"{periodo.Empresa.RFC}|" +
                $"{periodo.Empresa.RazonSocial}|" +
                $"000000000000000000|" +
                $"{empleado.RFC}|" +
                $"{nombre}|" +
                $"{clabe}|" +
                $"{banco}|" +
                $"{calculo.NetoPagar:F2}|" +
                $"NOMINA {periodo.FechaInicio:ddMMyyyy}-{periodo.FechaFin:ddMMyyyy}|" +
                $"NOM{periodoId:D6}{empleado.Id:D4}"
            );
        }

        // Total al final
        var totalNeto = empleados.Sum(emp => {
            var incEmp = incidencias.Where(i => i.EmpleadoId == emp.Id).ToList();
            var p = new ParametrosCalculo
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
                DiasPrimaDominical   = incEmp.Where(i => i.Tipo == TipoIncidencia.PrimaDominical).Sum(i => i.Cantidad)
            };
            return MotorCalculo.Calcular(p).NetoPagar;
        });

        sb.AppendLine($"TOTAL|||||||||||{totalNeto:F2}||");

        var bytes    = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"layout_nomina_{periodo.FechaInicio:yyyyMMdd}_{periodo.FechaFin:yyyyMMdd}.txt";
        return File(bytes, "text/plain", fileName);
    }

    [HttpGet("resumen/{periodoId}")]
    public async Task<ActionResult<object>> GetResumen(int periodoId)
    {
        var periodo = await _context.PeriodosNomina
            .Include(p => p.Empresa)
            .FirstOrDefaultAsync(p => p.Id == periodoId);
        if (periodo is null) return NotFound();

        var empleados = await _context.Empleados
            .Where(e => e.EmpresaId == periodo.EmpresaId && e.Activo)
            .ToListAsync();

        var incidencias = await _context.Incidencias
            .Where(i => i.PeriodoNominaId == periodoId)
            .ToListAsync();

        var diasPeriodo = (periodo.FechaFin - periodo.FechaInicio).Days + 1;
        var pagos = new List<object>();

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
            pagos.Add(new
            {
                empleadoId   = empleado.Id,
                nombre       = $"{empleado.Nombre} {empleado.ApellidoPaterno} {empleado.ApellidoMaterno}".Trim(),
                rfc          = empleado.RFC,
                clabe        = empleado.CLABE ?? "Sin CLABE registrada",
                banco        = empleado.Banco ?? "Sin banco",
                netoPagar    = calculo.NetoPagar,
                tieneCLABE   = !string.IsNullOrEmpty(empleado.CLABE)
            });
        }

        return Ok(new
        {
            empresa      = periodo.Empresa.RazonSocial,
            periodo      = $"{periodo.FechaInicio:dd/MM/yyyy} — {periodo.FechaFin:dd/MM/yyyy}",
            fechaPago    = periodo.FechaFin.ToString("dd/MM/yyyy"),
            totalEmpleados = empleados.Count,
            totalNeto    = Math.Round(pagos.Sum(p => (decimal)p.GetType().GetProperty("netoPagar")!.GetValue(p)!), 2),
            sinCLABE     = pagos.Count(p => !(bool)p.GetType().GetProperty("tieneCLABE")!.GetValue(p)!),
            pagos
        });
    }
}