using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NominaApp.Core.Calculos;
using NominaApp.Core.Entities;
using NominaApp.Infrastructure.Data;
using System.Text;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SuaController : ControllerBase
{
    private readonly NominaDbContext _context;
    private const decimal UMA_2025 = 108.57m;
    private const decimal SALARIO_MIN_2025 = 248.93m;
    private const decimal TOPE_SBC = 108.57m * 25; // 25 UMAs

    public SuaController(NominaDbContext context)
    {
        _context = context;
    }

    [HttpGet("cedula/{empresaId}")]
    public async Task<ActionResult<object>> GetCedula(
        int empresaId,
        [FromQuery] int mes,
        [FromQuery] int anio)
    {
        var empresa = await _context.Empresas.FindAsync(empresaId);
        if (empresa is null) return NotFound();

        var empleados = await _context.Empleados
            .Where(e => e.EmpresaId == empresaId && e.Activo)
            .ToListAsync();

        var periodosMes = await _context.PeriodosNomina
            .Where(p => p.EmpresaId == empresaId
                     && p.FechaInicio.Month <= mes
                     && p.FechaFin.Month >= mes
                     && p.FechaInicio.Year == anio)
            .OrderBy(p => p.FechaInicio)
            .ToListAsync();

        var incidencias = await _context.Incidencias
            .Where(i => periodosMes.Select(p => p.Id).Contains(i.PeriodoNominaId))
            .ToListAsync();

        // Conceptos SUA
        var conceptos = new ConceptosSUA();
        var detalleEmpleados = new List<object>();

        foreach (var emp in empleados)
        {
            var imss = MotorCalculo.CalcularCuotasIMSS(emp.SalarioDiario, 30);
            var sbc  = Math.Min(imss.SBC, TOPE_SBC);

            // Días cotizados en el mes
            var diasCotizados = 30;
            var incEmp = incidencias.Where(i => i.EmpleadoId == emp.Id).ToList();
            var ausencias = incEmp.Where(i =>
                i.Tipo == TipoIncidencia.LicenciaSinGoce ||
                i.Tipo == TipoIncidencia.IncapacidadIMSS).Sum(i => i.Cantidad);
            diasCotizados = Math.Max(0, 30 - (int)ausencias);

            var factor = diasCotizados / 30m;

            // Cuotas obrero (parte que descuenta al empleado)
            var enfermedadMatObrero  = Math.Round(sbc * 30 * 0.0040m * factor, 2);
            var invalidezVidaObrero  = Math.Round(sbc * 30 * 0.00625m * factor, 2);
            var cesantiaVejezObrero  = Math.Round(sbc * 30 * 0.01125m * factor, 2);
            var oomfObrero           = Math.Round(sbc * 30 * 0.00375m * factor, 2);
            var totalObrero          = Math.Round(enfermedadMatObrero + invalidezVidaObrero + cesantiaVejezObrero + oomfObrero, 2);

            // Cuotas patronales
            var enfermedadMatPatron  = Math.Round(sbc * 30 * 0.2040m * factor, 2);
            var invalidezVidaPatron  = Math.Round(sbc * 30 * 0.0175m * factor, 2);
            var guarderias           = Math.Round(sbc * 30 * 0.0100m * factor, 2);
            var riesgoTrabajo        = Math.Round(sbc * 30 * 0.0100m * factor, 2);
            var retiroSAR            = Math.Round(sbc * 30 * 0.0200m * factor, 2);
            var cesantiaVejezPatron  = Math.Round(sbc * 30 * 0.0315m * factor, 2);
            var infonavit            = Math.Round(sbc * 30 * 0.0500m * factor, 2);
            var totalPatronal        = Math.Round(enfermedadMatPatron + invalidezVidaPatron + guarderias + riesgoTrabajo + retiroSAR + cesantiaVejezPatron + infonavit, 2);

            // Acumular en conceptos SUA
            conceptos.EnfermedadMaternidadObrero  += enfermedadMatObrero;
            conceptos.InvalidezVidaObrero          += invalidezVidaObrero;
            conceptos.CesantiaVejezObrero          += cesantiaVejezObrero;
            conceptos.OOMFObrero                   += oomfObrero;
            conceptos.EnfermedadMaternidadPatron   += enfermedadMatPatron;
            conceptos.InvalidezVidaPatron          += invalidezVidaPatron;
            conceptos.Guarderias                   += guarderias;
            conceptos.RiesgoTrabajo                += riesgoTrabajo;
            conceptos.RetiroSAR                    += retiroSAR;
            conceptos.CesantiaVejezPatron          += cesantiaVejezPatron;
            conceptos.Infonavit                    += infonavit;

            detalleEmpleados.Add(new
            {
                empleadoId       = emp.Id,
                nombre           = $"{emp.Nombre} {emp.ApellidoPaterno}".Trim(),
                nss              = emp.NSS,
                rfc              = emp.RFC,
                salarioDiario    = emp.SalarioDiario,
                sbc              = Math.Round(sbc, 2),
                sbcMensual       = Math.Round(sbc * 30, 2),
                diasCotizados,
                cuotaObrero      = totalObrero,
                cuotaPatronal    = totalPatronal,
                totalCuotas      = Math.Round(totalObrero + totalPatronal, 2),
                desglose = new
                {
                    obrero = new
                    {
                        enfermedadMaternidad = enfermedadMatObrero,
                        invalidezVida        = invalidezVidaObrero,
                        cesantiaVejez        = cesantiaVejezObrero,
                        oomf                 = oomfObrero,
                        total                = totalObrero
                    },
                    patronal = new
                    {
                        enfermedadMaternidad = enfermedadMatPatron,
                        invalidezVida        = invalidezVidaPatron,
                        guarderias,
                        riesgoTrabajo,
                        retiroSAR,
                        cesantiaVejez        = cesantiaVejezPatron,
                        infonavit,
                        total                = totalPatronal
                    }
                }
            });
        }

        // Redondear totales
        conceptos.Redondear();

        var totalObreroCedula   = conceptos.TotalObrero;
        var totalPatronalCedula = conceptos.TotalPatronal;
        var totalSAR            = conceptos.RetiroSAR + conceptos.CesantiaVejezPatron + conceptos.CesantiaVejezObrero;
        var totalInfonavit      = conceptos.Infonavit;
        var totalIMSS           = Math.Round(totalObreroCedula + totalPatronalCedula - totalSAR - totalInfonavit, 2);

        // Fecha límite de pago (día 17 del mes siguiente)
        var mesSigniente   = mes == 12 ? 1 : mes + 1;
        var anioSiguiente  = mes == 12 ? anio + 1 : anio;
        var fechaLimite    = new DateTime(anioSiguiente, mesSigniente, 17);
        if (fechaLimite.DayOfWeek == DayOfWeek.Saturday) fechaLimite = fechaLimite.AddDays(2);
        if (fechaLimite.DayOfWeek == DayOfWeek.Sunday)   fechaLimite = fechaLimite.AddDays(1);

        return Ok(new
        {
            empresa         = empresa.RazonSocial,
            rfc             = empresa.RFC,
            mes,
            anio,
            nombreMes       = new[] { "", "Enero","Febrero","Marzo","Abril","Mayo","Junio","Julio","Agosto","Septiembre","Octubre","Noviembre","Diciembre" }[mes],
            totalEmpleados  = empleados.Count,
            periodosCalc    = periodosMes.Count,
            fechaLimitePago = fechaLimite.ToString("dd/MM/yyyy"),
            uma             = UMA_2025,
            topeSBC         = Math.Round(TOPE_SBC, 2),

            resumen = new
            {
                totalObrero         = Math.Round(totalObreroCedula, 2),
                totalPatronal       = Math.Round(totalPatronalCedula, 2),
                totalIMSS           = Math.Round(totalIMSS, 2),
                totalSAR            = Math.Round(totalSAR, 2),
                totalInfonavit      = Math.Round(totalInfonavit, 2),
                totalAPagar         = Math.Round(totalObreroCedula + totalPatronalCedula, 2),
            },

            conceptosSUA = new
            {
                imss = new[]
                {
                    new { concepto = "Enf. y Mat. — Obrero",   clave = "EM-O", monto = conceptos.EnfermedadMaternidadObrero,  tipo = "obrero" },
                    new { concepto = "Inv. y Vida — Obrero",   clave = "IV-O", monto = conceptos.InvalidezVidaObrero,          tipo = "obrero" },
                    new { concepto = "Ces. y Vejez — Obrero",  clave = "CV-O", monto = conceptos.CesantiaVejezObrero,          tipo = "obrero" },
                    new { concepto = "OOMF — Obrero",          clave = "OF-O", monto = conceptos.OOMFObrero,                   tipo = "obrero" },
                    new { concepto = "Enf. y Mat. — Patronal", clave = "EM-P", monto = conceptos.EnfermedadMaternidadPatron,  tipo = "patronal" },
                    new { concepto = "Inv. y Vida — Patronal", clave = "IV-P", monto = conceptos.InvalidezVidaPatron,          tipo = "patronal" },
                    new { concepto = "Guarderías — Patronal",  clave = "GU-P", monto = conceptos.Guarderias,                   tipo = "patronal" },
                    new { concepto = "Riesgo Trabajo",         clave = "RT-P", monto = conceptos.RiesgoTrabajo,                tipo = "patronal" },
                },
                sar = new[]
                {
                    new { concepto = "Retiro — Patronal",       clave = "RT-S", monto = conceptos.RetiroSAR,            tipo = "patronal" },
                    new { concepto = "Ces. y Vejez — Patronal", clave = "CV-S", monto = conceptos.CesantiaVejezPatron,  tipo = "patronal" },
                    new { concepto = "Ces. y Vejez — Obrero",   clave = "CV-O", monto = conceptos.CesantiaVejezObrero,  tipo = "obrero" },
                },
                infonavit = new[]
                {
                    new { concepto = "Aportación INFONAVIT",   clave = "IN-P", monto = conceptos.Infonavit,             tipo = "patronal" },
                }
            },

            empleados = detalleEmpleados
        });
    }

    [HttpGet("archivo-sua/{empresaId}")]
    public async Task<IActionResult> DescargarArchivoSUA(
        int empresaId, [FromQuery] int mes, [FromQuery] int anio)
    {
        var empresa = await _context.Empresas.FindAsync(empresaId);
        if (empresa is null) return NotFound();

        var empleados = await _context.Empleados
            .Where(e => e.EmpresaId == empresaId && e.Activo)
            .OrderBy(e => e.NSS)
            .ToListAsync();

        var sb = new StringBuilder();

        // Encabezado tipo SUA
        sb.AppendLine($"1|{empresa.RFC}|{empresa.RazonSocial}|{anio}{mes:D2}|{empleados.Count}");

        foreach (var emp in empleados)
        {
            var imss = MotorCalculo.CalcularCuotasIMSS(emp.SalarioDiario, 30);
            var sbc  = Math.Round(Math.Min(imss.SBC, TOPE_SBC), 2);
            sb.AppendLine($"9|{emp.NSS}|{emp.CURP}|{emp.Nombre} {emp.ApellidoPaterno} {emp.ApellidoMaterno}|{sbc:F2}|30|0|{anio}{mes:D2}01|{anio}{mes:D2}30");
        }

        var bytes    = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"SUA_{empresa.RFC}_{anio}{mes:D2}.sua";
        return File(bytes, "text/plain", fileName);
    }

    [HttpGet("comparativo/{empresaId}")]
    public async Task<ActionResult<object>> GetComparativo(int empresaId, [FromQuery] int anio)
    {
        var empresa = await _context.Empresas.FindAsync(empresaId);
        if (empresa is null) return NotFound();

        var empleados = await _context.Empleados
            .Where(e => e.EmpresaId == empresaId && e.Activo)
            .ToListAsync();

        var meses = new List<object>();
        for (int mes = 1; mes <= 12; mes++)
        {
            var periodosMes = await _context.PeriodosNomina
                .Where(p => p.EmpresaId == empresaId
                         && p.FechaInicio.Year == anio
                         && p.FechaInicio.Month <= mes
                         && p.FechaFin.Month >= mes)
                .ToListAsync();

            decimal totalCuotas = 0;
            decimal totalSBC    = 0;

            foreach (var emp in empleados)
            {
                var imss = MotorCalculo.CalcularCuotasIMSS(emp.SalarioDiario, 30);
                var sbc  = Math.Min(imss.SBC, TOPE_SBC);
                totalCuotas += Math.Round(imss.TotalObrero + imss.TotalPatronal, 2);
                totalSBC    += sbc;
            }

            meses.Add(new
            {
                mes,
                nombre        = new[] { "","Ene","Feb","Mar","Abr","May","Jun","Jul","Ago","Sep","Oct","Nov","Dic" }[mes],
                totalCuotas   = Math.Round(totalCuotas, 2),
                totalSBC      = Math.Round(totalSBC, 2),
                empleados     = empleados.Count,
                periodos      = periodosMes.Count,
                tieneDatos    = periodosMes.Count > 0
            });
        }

        return Ok(new
        {
            empresa  = empresa.RazonSocial,
            anio,
            meses,
            totalAnual = Math.Round(meses.Sum(m => (decimal)m.GetType().GetProperty("totalCuotas")!.GetValue(m)!), 2)
        });
    }
}

public class ConceptosSUA
{
    public decimal EnfermedadMaternidadObrero  { get; set; }
    public decimal InvalidezVidaObrero          { get; set; }
    public decimal CesantiaVejezObrero          { get; set; }
    public decimal OOMFObrero                   { get; set; }
    public decimal EnfermedadMaternidadPatron   { get; set; }
    public decimal InvalidezVidaPatron          { get; set; }
    public decimal Guarderias                   { get; set; }
    public decimal RiesgoTrabajo                { get; set; }
    public decimal RetiroSAR                    { get; set; }
    public decimal CesantiaVejezPatron          { get; set; }
    public decimal Infonavit                    { get; set; }

    public decimal TotalObrero   => EnfermedadMaternidadObrero + InvalidezVidaObrero + CesantiaVejezObrero + OOMFObrero;
    public decimal TotalPatronal => EnfermedadMaternidadPatron + InvalidezVidaPatron + Guarderias + RiesgoTrabajo + RetiroSAR + CesantiaVejezPatron + Infonavit;

    public void Redondear()
    {
        EnfermedadMaternidadObrero  = Math.Round(EnfermedadMaternidadObrero, 2);
        InvalidezVidaObrero          = Math.Round(InvalidezVidaObrero, 2);
        CesantiaVejezObrero          = Math.Round(CesantiaVejezObrero, 2);
        OOMFObrero                   = Math.Round(OOMFObrero, 2);
        EnfermedadMaternidadPatron   = Math.Round(EnfermedadMaternidadPatron, 2);
        InvalidezVidaPatron          = Math.Round(InvalidezVidaPatron, 2);
        Guarderias                   = Math.Round(Guarderias, 2);
        RiesgoTrabajo                = Math.Round(RiesgoTrabajo, 2);
        RetiroSAR                    = Math.Round(RetiroSAR, 2);
        CesantiaVejezPatron          = Math.Round(CesantiaVejezPatron, 2);
        Infonavit                    = Math.Round(Infonavit, 2);
    }
}