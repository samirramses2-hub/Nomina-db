using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NominaApp.Core.Calculos;
using NominaApp.Core.Entities;
using NominaApp.Infrastructure.Data;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PolizasController : ControllerBase
{
    private readonly NominaDbContext _context;

    public PolizasController(NominaDbContext context)
    {
        _context = context;
    }

    [HttpGet("{periodoId}")]
    public async Task<ActionResult<object>> GetPoliza(int periodoId)
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

        // Acumulados de la nómina
        decimal totalSueldos        = 0;
        decimal totalHorasExtra     = 0;
        decimal totalBonos          = 0;
        decimal totalPrimaDominical = 0;
        decimal totalISRRetenido    = 0;
        decimal totalIMSSObrero     = 0;
        decimal totalIMSSPatronal   = 0;
        decimal totalSAR            = 0;
        decimal totalINFONAVIT      = 0;
        decimal totalNeto           = 0;

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

            totalSueldos        += calculo.Percepciones.FirstOrDefault(p => p.Concepto == "Salario base")?.Monto ?? 0;
            totalHorasExtra     += calculo.Percepciones.Where(p => p.Concepto.Contains("extra")).Sum(p => p.Monto);
            totalBonos          += calculo.Percepciones.Where(p => p.Concepto == "Bono").Sum(p => p.Monto);
            totalPrimaDominical += calculo.Percepciones.Where(p => p.Concepto == "Prima dominical").Sum(p => p.Monto);
            totalISRRetenido    += calculo.DetalleISR.ISRRetenido;
            totalIMSSObrero     += imss.TotalObrero;
            totalIMSSPatronal   += imss.EnfermedadMaternidadPatron + imss.InvalidezVidaPatron + imss.GuarderiasPrestaciones + imss.RiesgoTrabajo;
            totalSAR            += imss.RetiroSAR;
            totalINFONAVIT      += imss.Infonavit;
            var netoBruto = calculo.TotalPercepciones - calculo.DetalleISR.ISRRetenido - imss.TotalObrero;
            totalNeto += Math.Round(netoBruto, 2);
        }

        totalSueldos        = Math.Round(totalSueldos, 2);
        totalHorasExtra     = Math.Round(totalHorasExtra, 2);
        totalBonos          = Math.Round(totalBonos, 2);
        totalPrimaDominical = Math.Round(totalPrimaDominical, 2);
        totalISRRetenido    = Math.Round(totalISRRetenido, 2);
        totalIMSSObrero     = Math.Round(totalIMSSObrero, 2);
        totalIMSSPatronal   = Math.Round(totalIMSSPatronal, 2);
        totalSAR            = Math.Round(totalSAR, 2);
        totalINFONAVIT      = Math.Round(totalINFONAVIT, 2);
        totalNeto           = Math.Round(totalNeto, 2);

        var totalGastoEmpresa = Math.Round(totalSueldos + totalHorasExtra + totalBonos + totalPrimaDominical + totalIMSSPatronal + totalSAR + totalINFONAVIT, 2);

        var cargos = new List<object>();
        var abonos = new List<object>();

        // CARGOS
        if (totalSueldos > 0)
            cargos.Add(new { cuenta = "6101-001", nombre = "Sueldos y salarios", monto = totalSueldos, descripcion = "Gasto por sueldos del periodo" });

        if (totalHorasExtra > 0)
            cargos.Add(new { cuenta = "6101-002", nombre = "Horas extra", monto = totalHorasExtra, descripcion = "Gasto por horas extra del periodo" });

        if (totalBonos > 0)
            cargos.Add(new { cuenta = "6101-003", nombre = "Bonos y gratificaciones", monto = totalBonos, descripcion = "Bonos pagados en el periodo" });

        if (totalPrimaDominical > 0)
            cargos.Add(new { cuenta = "6101-004", nombre = "Prima dominical", monto = totalPrimaDominical, descripcion = "Prima dominical del periodo" });

        if (totalIMSSPatronal > 0)
            cargos.Add(new { cuenta = "6104-001", nombre = "Cuotas IMSS patronal", monto = totalIMSSPatronal, descripcion = "Cuotas patronales IMSS (EM + IV + Guard + Riesgo)" });

        if (totalSAR > 0)
            cargos.Add(new { cuenta = "6104-002", nombre = "Aportaciones SAR", monto = totalSAR, descripcion = "Aportaciones al Sistema de Ahorro para el Retiro" });

        if (totalINFONAVIT > 0)
            cargos.Add(new { cuenta = "6104-003", nombre = "Aportaciones INFONAVIT", monto = totalINFONAVIT, descripcion = "Aportaciones patronales al INFONAVIT" });

        // ABONOS
        if (totalISRRetenido > 0)
            abonos.Add(new { cuenta = "2115-001", nombre = "ISR por enterar", monto = totalISRRetenido, descripcion = "ISR retenido a empleados por enterar al SAT" });

        if (totalIMSSObrero > 0)
            abonos.Add(new { cuenta = "2115-002", nombre = "IMSS obrero por enterar", monto = totalIMSSObrero, descripcion = "Cuotas obreras retenidas por enterar al IMSS" });

        if (totalIMSSPatronal > 0)
            abonos.Add(new { cuenta = "2115-003", nombre = "IMSS patronal por pagar", monto = totalIMSSPatronal, descripcion = "Cuotas patronales IMSS por pagar" });

        if (totalSAR > 0)
            abonos.Add(new { cuenta = "2115-004", nombre = "SAR por pagar", monto = totalSAR, descripcion = "Aportaciones SAR por pagar" });

        if (totalINFONAVIT > 0)
            abonos.Add(new { cuenta = "2115-005", nombre = "INFONAVIT por pagar", monto = totalINFONAVIT, descripcion = "Aportaciones INFONAVIT por pagar" });

        abonos.Add(new { cuenta = "1102-001", nombre = "Bancos", monto = totalNeto, descripcion = "Pago neto de nómina dispersado a empleados" });

        var totalCargos = Math.Round(cargos.Sum(c => (decimal)c.GetType().GetProperty("monto")!.GetValue(c)!), 2);
        var totalAbonos = Math.Round(abonos.Sum(a => (decimal)a.GetType().GetProperty("monto")!.GetValue(a)!), 2);

        return Ok(new
        {
            empresa          = periodo.Empresa.RazonSocial,
            rfc              = periodo.Empresa.RFC,
            periodo          = $"{periodo.FechaInicio:dd/MM/yyyy} — {periodo.FechaFin:dd/MM/yyyy}",
            fechaPoliza      = periodo.FechaFin.ToString("dd/MM/yyyy"),
            concepto         = $"Nómina {periodo.FechaInicio:dd/MM/yyyy} al {periodo.FechaFin:dd/MM/yyyy}",
            numEmpleados     = empleados.Count,
            cargos,
            abonos,
            totalCargos,
            totalAbonos,
            cuadra           = totalCargos == totalAbonos,
            diferencia       = Math.Abs(totalCargos - totalAbonos)
        });
    }
}