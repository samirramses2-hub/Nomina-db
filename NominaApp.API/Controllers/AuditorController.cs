using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NominaApp.Core.Calculos;
using NominaApp.Core.Entities;
using NominaApp.Infrastructure.Data;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuditorController : ControllerBase
{
    private readonly NominaDbContext _context;

    public AuditorController(NominaDbContext context)
    {
        _context = context;
    }

    [HttpGet("{empresaId}")]
    public async Task<ActionResult<object>> Auditar(int empresaId)
    {
        var empresa = await _context.Empresas.FindAsync(empresaId);
        if (empresa is null) return NotFound();

        var empleados = await _context.Empleados
            .Where(e => e.EmpresaId == empresaId && e.Activo)
            .ToListAsync();

        var periodos = await _context.PeriodosNomina
            .Where(p => p.EmpresaId == empresaId)
            .OrderByDescending(p => p.FechaInicio)
            .Take(6)
            .ToListAsync();

        var incidencias = await _context.Incidencias
            .Where(i => periodos.Select(p => p.Id).Contains(i.PeriodoNominaId))
            .ToListAsync();

        var hallazgos = new List<HallazgoAuditoria>();

        // ── 1. VALIDACIÓN DE RFC ─────────────────────────────────
        foreach (var emp in empleados)
        {
            var rfc = emp.RFC?.Trim().ToUpper() ?? "";
            if (string.IsNullOrEmpty(rfc))
            {
                hallazgos.Add(new HallazgoAuditoria
                {
                    Nivel      = "alto",
                    Categoria  = "RFC",
                    EmpleadoId = emp.Id,
                    Empleado   = $"{emp.Nombre} {emp.ApellidoPaterno}",
                    Mensaje    = "RFC vacío — el empleado no puede ser timbrado.",
                    Detalle    = "El RFC es obligatorio para generar CFDI de nómina."
                });
            }
            else if (rfc.Length != 13)
            {
                hallazgos.Add(new HallazgoAuditoria
                {
                    Nivel      = "alto",
                    Categoria  = "RFC",
                    EmpleadoId = emp.Id,
                    Empleado   = $"{emp.Nombre} {emp.ApellidoPaterno}",
                    Mensaje    = $"RFC con longitud incorrecta ({rfc.Length} caracteres, debe ser 13).",
                    Detalle    = $"RFC actual: {rfc}"
                });
            }
            else if (!ValidarFormatoRFC(rfc))
            {
                hallazgos.Add(new HallazgoAuditoria
                {
                    Nivel      = "alto",
                    Categoria  = "RFC",
                    EmpleadoId = emp.Id,
                    Empleado   = $"{emp.Nombre} {emp.ApellidoPaterno}",
                    Mensaje    = "RFC con formato inválido.",
                    Detalle    = $"RFC actual: {rfc}. Formato esperado: 4 letras + 6 dígitos + 3 alfanuméricos."
                });
            }
        }

        // ── 2. VALIDACIÓN DE CURP ────────────────────────────────
        foreach (var emp in empleados)
        {
            var curp = emp.CURP?.Trim().ToUpper() ?? "";
            if (string.IsNullOrEmpty(curp))
            {
                hallazgos.Add(new HallazgoAuditoria
                {
                    Nivel      = "medio",
                    Categoria  = "CURP",
                    EmpleadoId = emp.Id,
                    Empleado   = $"{emp.Nombre} {emp.ApellidoPaterno}",
                    Mensaje    = "CURP vacío — requerido para CFDI de nómina.",
                    Detalle    = "El CURP es obligatorio en el complemento de nómina 1.2."
                });
            }
            else if (curp.Length != 18)
            {
                hallazgos.Add(new HallazgoAuditoria
                {
                    Nivel      = "medio",
                    Categoria  = "CURP",
                    EmpleadoId = emp.Id,
                    Empleado   = $"{emp.Nombre} {emp.ApellidoPaterno}",
                    Mensaje    = $"CURP con longitud incorrecta ({curp.Length} caracteres, debe ser 18).",
                    Detalle    = $"CURP actual: {curp}"
                });
            }
        }

        // ── 3. VALIDACIÓN DE NSS ─────────────────────────────────
        foreach (var emp in empleados)
        {
            var nss = emp.NSS?.Trim() ?? "";
            if (string.IsNullOrEmpty(nss))
            {
                hallazgos.Add(new HallazgoAuditoria
                {
                    Nivel      = "medio",
                    Categoria  = "NSS",
                    EmpleadoId = emp.Id,
                    Empleado   = $"{emp.Nombre} {emp.ApellidoPaterno}",
                    Mensaje    = "NSS vacío — requerido para cuotas IMSS.",
                    Detalle    = "El Número de Seguridad Social es obligatorio."
                });
            }
            else if (nss.Length != 11 || !nss.All(char.IsDigit))
            {
                hallazgos.Add(new HallazgoAuditoria
                {
                    Nivel      = "medio",
                    Categoria  = "NSS",
                    EmpleadoId = emp.Id,
                    Empleado   = $"{emp.Nombre} {emp.ApellidoPaterno}",
                    Mensaje    = $"NSS inválido ({nss.Length} dígitos, debe ser 11 numéricos).",
                    Detalle    = $"NSS actual: {nss}"
                });
            }
        }

        // ── 4. VALIDACIÓN DE SALARIO MÍNIMO ─────────────────────
        foreach (var emp in empleados)
        {
            if (emp.SalarioDiario < 248.93m)
            {
                hallazgos.Add(new HallazgoAuditoria
                {
                    Nivel      = "alto",
                    Categoria  = "Salario",
                    EmpleadoId = emp.Id,
                    Empleado   = $"{emp.Nombre} {emp.ApellidoPaterno}",
                    Mensaje    = $"Salario de ${emp.SalarioDiario:F2}/día es menor al mínimo vigente ($248.93/día).",
                    Detalle    = "Violación LFT Art. 90. Riesgo de multa del IMSS e INFONAVIT."
                });
            }
        }

        // ── 5. VALIDACIÓN DE SBC ─────────────────────────────────
        foreach (var emp in empleados)
        {
            var imss = MotorCalculo.CalcularCuotasIMSS(emp.SalarioDiario, 15);
            var sbcCalculado = imss.SBC;
            var sbcMov = await _context.MovimientosIMSS
                .Where(m => m.EmpleadoId == emp.Id)
                .OrderByDescending(m => m.FechaMovimiento)
                .Select(m => m.SalarioDiarioIntegrado)
                .FirstOrDefaultAsync();

            if (sbcMov > 0)
            {
                var diferencia = Math.Abs(sbcCalculado - sbcMov) / sbcCalculado * 100;
                if (diferencia > 5)
                {
                    hallazgos.Add(new HallazgoAuditoria
                    {
                        Nivel      = "alto",
                        Categoria  = "SBC",
                        EmpleadoId = emp.Id,
                        Empleado   = $"{emp.Nombre} {emp.ApellidoPaterno}",
                        Mensaje    = $"SBC registrado ante IMSS (${sbcMov:F2}) difiere {diferencia:F1}% del calculado (${sbcCalculado:F2}).",
                        Detalle    = "Diferencia mayor al 5% puede resultar en diferencias de cuotas IMSS."
                    });
                }
            }
        }

        // ── 6. VALIDACIÓN DE ISR FUERA DE RANGO ─────────────────
        if (periodos.Any())
        {
            var ultimoPeriodo = periodos.First();
            var diasPeriodo   = (ultimoPeriodo.FechaFin - ultimoPeriodo.FechaInicio).Days + 1;

            foreach (var emp in empleados)
            {
                var incEmp = incidencias.Where(i => i.EmpleadoId == emp.Id && i.PeriodoNominaId == ultimoPeriodo.Id).ToList();
                var parametros = new ParametrosCalculo
                {
                    SalarioDiario        = emp.SalarioDiario,
                    DiasPeriodo          = diasPeriodo,
                    EjercicioFiscal      = ultimoPeriodo.EjercicioFiscal,
                    FaltasInjustificadas = incEmp.Where(i => i.Tipo == TipoIncidencia.FaltaInjustificada).Sum(i => i.Cantidad),
                    FaltasJustificadas   = incEmp.Where(i => i.Tipo == TipoIncidencia.FaltaJustificada).Sum(i => i.Cantidad),
                    DiasVacaciones       = incEmp.Where(i => i.Tipo == TipoIncidencia.Vacaciones).Sum(i => i.Cantidad),
                    HorasExtraSimples    = incEmp.Where(i => i.Tipo == TipoIncidencia.HoraExtraSimple).Sum(i => i.Cantidad),
                    HorasExtraDobles     = incEmp.Where(i => i.Tipo == TipoIncidencia.HoraExtraDoble).Sum(i => i.Cantidad),
                    HorasExtraTriples    = incEmp.Where(i => i.Tipo == TipoIncidencia.HoraExtraTriple).Sum(i => i.Cantidad),
                    Bonos                = incEmp.Where(i => i.Tipo == TipoIncidencia.Bono).Sum(i => i.Cantidad),
                    DiasPrimaDominical   = incEmp.Where(i => i.Tipo == TipoIncidencia.PrimaDominical).Sum(i => i.Cantidad),
                };

                var calculo     = MotorCalculo.Calcular(parametros);
                var isrEsperado = calculo.DetalleISR.ISRRetenido;
                var pctISR      = calculo.TotalPercepciones > 0
                    ? isrEsperado / calculo.TotalPercepciones * 100
                    : 0;

                if (pctISR > 35)
                {
                    hallazgos.Add(new HallazgoAuditoria
                    {
                        Nivel      = "alto",
                        Categoria  = "ISR",
                        EmpleadoId = emp.Id,
                        Empleado   = $"{emp.Nombre} {emp.ApellidoPaterno}",
                        Mensaje    = $"ISR de ${isrEsperado:F2} representa {pctISR:F1}% del salario bruto — inusualmente alto.",
                        Detalle    = "Verificar si hubo percepciones extraordinarias o error de captura."
                    });
                }
            }
        }

        // ── 7. INCIDENCIAS DUPLICADAS ────────────────────────────
        foreach (var periodo in periodos)
        {
            var incPeriodo = incidencias
                .Where(i => i.PeriodoNominaId == periodo.Id)
                .GroupBy(i => new { i.EmpleadoId, i.Tipo })
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var grupo in incPeriodo)
            {
                var emp = empleados.FirstOrDefault(e => e.Id == grupo.Key.EmpleadoId);
                if (emp is null) continue;
                hallazgos.Add(new HallazgoAuditoria
                {
                    Nivel      = "medio",
                    Categoria  = "Duplicados",
                    EmpleadoId = emp.Id,
                    Empleado   = $"{emp.Nombre} {emp.ApellidoPaterno}",
                    Mensaje    = $"Incidencia duplicada: {grupo.Key.Tipo} aparece {grupo.Count()} veces en el periodo.",
                    Detalle    = $"Periodo: {periodo.FechaInicio:dd/MM/yyyy} — {periodo.FechaFin:dd/MM/yyyy}"
                });
            }
        }

        // ── 8. EMPLEADOS SIN CLABE ───────────────────────────────
        foreach (var emp in empleados.Where(e => string.IsNullOrEmpty(e.CLABE)))
        {
            hallazgos.Add(new HallazgoAuditoria
            {
                Nivel      = "bajo",
                Categoria  = "Bancario",
                EmpleadoId = emp.Id,
                Empleado   = $"{emp.Nombre} {emp.ApellidoPaterno}",
                Mensaje    = "Sin CLABE registrada — no se puede generar layout de pago.",
                Detalle    = "Registrar CLABE interbancaria de 18 dígitos."
            });
        }

        // ── 9. EMPLEADOS SIN MOVIMIENTO DE ALTA IMSS ────────────
        foreach (var emp in empleados)
        {
            var tieneAlta = await _context.MovimientosIMSS
                .AnyAsync(m => m.EmpleadoId == emp.Id && m.TipoMovimiento == TipoMovimientoIMSS.Alta);
            if (!tieneAlta)
            {
                hallazgos.Add(new HallazgoAuditoria
                {
                    Nivel      = "medio",
                    Categoria  = "IMSS",
                    EmpleadoId = emp.Id,
                    Empleado   = $"{emp.Nombre} {emp.ApellidoPaterno}",
                    Mensaje    = "Sin movimiento de alta registrado ante el IMSS.",
                    Detalle    = "Registrar el alta en el módulo de Movimientos IMSS y reportar al IDSE."
                });
            }
        }

        // ── 10. PERIODOS SIN CERRAR VENCIDOS ────────────────────
        var periodosVencidos = periodos
            .Where(p => p.Estado == EstadoPeriodo.Abierto && p.FechaFin < DateTime.UtcNow.AddDays(-5))
            .ToList();

        foreach (var per in periodosVencidos)
        {
            hallazgos.Add(new HallazgoAuditoria
            {
                Nivel      = "alto",
                Categoria  = "Periodo",
                EmpleadoId = null,
                Empleado   = "Empresa",
                Mensaje    = $"Periodo {per.FechaInicio:dd/MM/yyyy}—{per.FechaFin:dd/MM/yyyy} venció hace {(DateTime.UtcNow - per.FechaFin).Days} días y sigue abierto.",
                Detalle    = "Los CFDIs de nómina deben timbrarse dentro del periodo vigente."
            });
        }

        // ── RESUMEN ──────────────────────────────────────────────
        var altos  = hallazgos.Count(h => h.Nivel == "alto");
        var medios = hallazgos.Count(h => h.Nivel == "medio");
        var bajos  = hallazgos.Count(h => h.Nivel == "bajo");

        var nivelGeneral = altos > 0 ? "alto" : medios > 2 ? "medio" : medios > 0 ? "medio" : "bajo";

        return Ok(new
        {
            empresa        = empresa.RazonSocial,
            rfc            = empresa.RFC,
            fechaAuditoria = DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm"),
            totalEmpleados = empleados.Count,
            totalHallazgos = hallazgos.Count,
            nivelGeneral,
            resumen = new { altos, medios, bajos },
            hallazgos = hallazgos.OrderBy(h =>
                h.Nivel == "alto" ? 0 : h.Nivel == "medio" ? 1 : 2).ToList()
        });
    }

    private bool ValidarFormatoRFC(string rfc)
    {
        if (rfc.Length != 13) return false;
        var patron = new System.Text.RegularExpressions.Regex(
            @"^[A-Z&Ñ]{4}\d{6}[A-Z0-9]{3}$");
        return patron.IsMatch(rfc);
    }
}

public class HallazgoAuditoria
{
    public string  Nivel      { get; set; } = string.Empty;
    public string  Categoria  { get; set; } = string.Empty;
    public int?    EmpleadoId { get; set; }
    public string  Empleado   { get; set; } = string.Empty;
    public string  Mensaje    { get; set; } = string.Empty;
    public string  Detalle    { get; set; } = string.Empty;
}