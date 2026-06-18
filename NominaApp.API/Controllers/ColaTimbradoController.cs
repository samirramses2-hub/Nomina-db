using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NominaApp.Core.Calculos;
using NominaApp.Core.Entities;
using NominaApp.Infrastructure.Data;
using NominaApp.Infrastructure.Servicios;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ColaTimbradoController : ControllerBase
{
    private readonly NominaDbContext _context;
    private const string FacturamaUsuario  = "Samir0501";
    private const string FacturamaPassword = "Srpv180501";

    public ColaTimbradoController(NominaDbContext context)
    {
        _context = context;
    }

    // Encolar todos los empleados de un periodo
    [HttpPost("encolar/{periodoId}")]
    public async Task<ActionResult<object>> Encolar(
        int periodoId,
        [FromQuery] bool diferido = false,
        [FromQuery] string? fechaDiferido = null)
    {
        var periodo = await _context.PeriodosNomina
            .Include(p => p.Empresa)
            .FirstOrDefaultAsync(p => p.Id == periodoId);
        if (periodo is null) return NotFound("Periodo no encontrado.");

        var empleados = await _context.Empleados
            .Where(e => e.EmpresaId == periodo.EmpresaId && e.Activo)
            .ToListAsync();

        var encolados = 0;
        var yaEncolados = 0;

        foreach (var emp in empleados)
        {
            var yaExiste = await _context.ColaTimbrado
                .AnyAsync(c => c.EmpleadoId == emp.Id
                            && c.PeriodoNominaId == periodoId
                            && (c.Estado == EstadoCola.Pendiente
                             || c.Estado == EstadoCola.Completado
                             || c.Estado == EstadoCola.Procesando
                             || c.Estado == EstadoCola.Diferido));
            if (yaExiste) { yaEncolados++; continue; }

            _context.ColaTimbrado.Add(new ColaTimbrado
            {
                EmpleadoId      = emp.Id,
                PeriodoNominaId = periodoId,
                Estado          = diferido ? EstadoCola.Diferido : EstadoCola.Pendiente,
                Intentos        = 0,
                MaxIntentos     = 3,
                Diferido        = diferido,
                FechaDiferido   = diferido && fechaDiferido != null
                    ? DateTime.Parse(fechaDiferido) : null,
                FechaCreacion   = DateTime.UtcNow
            });
            encolados++;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            mensaje     = $"{encolados} empleados encolados para timbrado.",
            encolados,
            yaEncolados,
            diferido,
            fechaDiferido
        });
    }

    // Estado de la cola de un periodo
    [HttpGet("estado/{periodoId}")]
    public async Task<ActionResult<object>> GetEstado(int periodoId)
    {
        var periodo = await _context.PeriodosNomina
            .Include(p => p.Empresa)
            .FirstOrDefaultAsync(p => p.Id == periodoId);
        if (periodo is null) return NotFound();

        var cola = await _context.ColaTimbrado
            .Include(c => c.Empleado)
            .Where(c => c.PeriodoNominaId == periodoId)
            .OrderBy(c => c.Id)
            .ToListAsync();

        return Ok(new
        {
            periodoId,
            empresa        = periodo.Empresa.RazonSocial,
            periodo        = $"{periodo.FechaInicio:dd/MM/yyyy} — {periodo.FechaFin:dd/MM/yyyy}",
            totalEncolados = cola.Count,
            pendientes     = cola.Count(c => c.Estado == EstadoCola.Pendiente),
            procesando     = cola.Count(c => c.Estado == EstadoCola.Procesando),
            completados    = cola.Count(c => c.Estado == EstadoCola.Completado),
            errores        = cola.Count(c => c.Estado == EstadoCola.Error),
            diferidos      = cola.Count(c => c.Estado == EstadoCola.Diferido),
            cancelados     = cola.Count(c => c.Estado == EstadoCola.Cancelado),
            porcentaje     = cola.Count > 0
                ? Math.Round((decimal)cola.Count(c => c.Estado == EstadoCola.Completado) / cola.Count * 100, 1)
                : 0,
            items = cola.Select(c => new
            {
                c.Id,
                c.EmpleadoId,
                empleado       = $"{c.Empleado.Nombre} {c.Empleado.ApellidoPaterno}".Trim(),
                estado         = c.Estado.ToString(),
                c.Intentos,
                c.MaxIntentos,
                c.UUID,
                c.UltimoError,
                c.Diferido,
                fechaDiferido  = c.FechaDiferido?.ToString("dd/MM/yyyy HH:mm"),
                proximoIntento = c.ProximoIntento?.ToString("dd/MM/yyyy HH:mm"),
                fechaCompletado = c.FechaCompletado?.ToString("dd/MM/yyyy HH:mm"),
                fechaCreacion  = c.FechaCreacion.ToString("dd/MM/yyyy HH:mm")
            }).ToList()
        });
    }

    // Procesar la cola — timbrar empleados pendientes
    [HttpPost("procesar/{periodoId}")]
    public async Task<ActionResult<object>> Procesar(int periodoId)
    {
        var periodo = await _context.PeriodosNomina
            .Include(p => p.Empresa)
            .FirstOrDefaultAsync(p => p.Id == periodoId);
        if (periodo is null) return NotFound();

        var pendientes = await _context.ColaTimbrado
            .Include(c => c.Empleado)
            .Where(c => c.PeriodoNominaId == periodoId
                     && (c.Estado == EstadoCola.Pendiente || c.Estado == EstadoCola.Error)
                     && c.Intentos < c.MaxIntentos
                     && (c.ProximoIntento == null || c.ProximoIntento <= DateTime.UtcNow))
            .OrderBy(c => c.Intentos)
            .ToListAsync();

        if (!pendientes.Any())
            return Ok(new { mensaje = "No hay elementos pendientes en la cola.", procesados = 0 });

        var incidencias = await _context.Incidencias
            .Where(i => i.PeriodoNominaId == periodoId)
            .ToListAsync();

        var diasPeriodo = (periodo.FechaFin - periodo.FechaInicio).Days + 1;
        var facturama   = new FacturamaService(FacturamaUsuario, FacturamaPassword);

        int exitosos = 0;
        int fallidos = 0;

        foreach (var item in pendientes)
        {
            item.Estado   = EstadoCola.Procesando;
            item.Intentos++;
            await _context.SaveChangesAsync();

            try
            {
                var emp    = item.Empleado;
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
                };

                var calculo = MotorCalculo.Calcular(parametros);
                var imss    = MotorCalculo.CalcularCuotasIMSS(emp.SalarioDiario, diasPeriodo);

                var percepciones = calculo.Percepciones.Select((p, idx) => new FacturamaPerceptionDetail
                {
                    PerceptionType = p.Concepto == "Salario base" ? "001" : "019",
                    Code           = $"{idx + 1:D3}",
                    Description    = p.Concepto,
                    TaxedAmount    = Math.Round(p.Monto, 2),
                    ExemptAmount   = 0
                }).ToList();

                var deducciones = calculo.Deducciones.Select((d, idx) => new FacturamaDeductionDetail
                {
                    DeduccionType = d.Concepto == "ISR retenido" ? "002" : "001",
                    Code          = $"{idx + 1:D3}",
                    Description   = d.Concepto,
                    Amount        = Math.Round(d.Monto, 2)
                }).ToList();

                var subsidio = Math.Round(Math.Max(calculo.DetalleISR.SubsidioEmpleo, 0.01m), 2);

                var cfdi = new FacturamaCfdiNomina
                {
                    NameId          = "16",
                    ExpeditionPlace = "22820",
                    CfdiType        = "N",
                    PaymentMethod   = "PPD",
                    Currency        = "MXN",
                    Folio           = $"{emp.Id}-{periodoId}-{DateTime.UtcNow:yyyyMMddHHmmss}",
                    Receiver = new FacturamaReceiver
                    {
                        Rfc          = emp.RFC,
                        Name         = $"{emp.Nombre} {emp.ApellidoPaterno}".Trim().ToUpper(),
                        CfdiUse      = "CN01",
                        FiscalRegime = "605",
                        TaxZipCode   = "36257"
                    },
                    Complemento = new FacturamaComplemento
                    {
                        Payroll = new FacturamaPayroll
                        {
                            Type               = "O",
                            PaymentDate        = periodo.FechaPago.ToString("yyyy-MM-ddTHH:mm:ss"),
                            InitialPaymentDate = periodo.FechaInicio.ToString("yyyy-MM-ddTHH:mm:ss"),
                            FinalPaymentDate   = periodo.FechaFin.ToString("yyyy-MM-ddTHH:mm:ss"),
                            DaysPaid           = diasPeriodo,
                            Issuer = new FacturamaPayrollIssuer
                            {
                                EmployerRegistration = "A1234567890"
                            },
                            Employee = new FacturamaPayrollEmployee
                            {
                                Curp                    = emp.CURP,
                                SocialSecurityNumber    = emp.NSS,
                                StartDateLaborRelations = emp.FechaIngreso.ToString("yyyy-MM-ddTHH:mm:ss"),
                                ContractType            = "01",
                                RegimeType              = "02",
                                Unionized               = false,
                                TypeOfJourney           = "01",
                                EmployeeNumber          = emp.Id.ToString(),
                                Department              = "General",
                                Position                = "Empleado",
                                PositionRisk            = "1",
                                FrequencyPayment        = "04",
                                Bank                    = "BANAMEX",
                                BankAccount             = "1234567890123456",
                                BaseSalary              = Math.Round(emp.SalarioDiario, 2),
                                DailySalary             = Math.Round(imss.SBC, 2),
                                FederalEntityKey        = "JAL"
                            },
                            Perceptions   = new FacturamaPerceptions { Details = percepciones },
                            Deductions    = new FacturamaDeductions  { Details = deducciones },
                            OtherPayments = new List<FacturamaOtherPayment>
                            {
                                new FacturamaOtherPayment
                                {
                                    OtherPaymentType  = "002",
                                    Code              = "001",
                                    Description       = "Subsidio al empleo",
                                    Amount            = subsidio,
                                    EmploymentSubsidy = new FacturamaEmploymentSubsidy { Amount = subsidio }
                                }
                            }
                        }
                    }
                };

                var respuesta = await facturama.TimbrarNominaAsync(cfdi);

                if (respuesta.Exito)
                {
                    item.Estado          = EstadoCola.Completado;
                    item.UUID            = respuesta.UUID;
                    item.FechaCompletado = DateTime.UtcNow;
                    item.UltimoError     = null;

                    // Guardar en CFDIs
                    _context.CFDIs.Add(new CFDIRegistro
                    {
                        EmpleadoId      = emp.Id,
                        PeriodoNominaId = periodoId,
                        UUID            = respuesta.UUID,
                        RFCEmisor       = "EKU9003173C9",
                        RFCReceptor     = emp.RFC,
                        Total           = calculo.NetoPagar,
                        Estado          = EstadoCFDI.Vigente,
                        FechaTimbrado   = DateTime.UtcNow
                    });

                    exitosos++;
                }
                else
                {
                    item.Estado        = EstadoCola.Error;
                    item.UltimoError   = respuesta.Error;
                    item.ProximoIntento = DateTime.UtcNow.AddMinutes(item.Intentos * 2);
                    fallidos++;
                }
            }
            catch (Exception ex)
            {
                item.Estado        = EstadoCola.Error;
                item.UltimoError   = ex.Message;
                item.ProximoIntento = DateTime.UtcNow.AddMinutes(item.Intentos * 2);
                fallidos++;
            }

            await _context.SaveChangesAsync();
            await Task.Delay(500); // Pausa entre requests al PAC
        }

        return Ok(new
        {
            exitosos,
            fallidos,
            pendientes = pendientes.Count - exitosos - fallidos
        });
    }

    // Reintentar un item específico
    [HttpPost("reintentar/{itemId}")]
    public async Task<IActionResult> Reintentar(int itemId)
    {
        var item = await _context.ColaTimbrado.FindAsync(itemId);
        if (item is null) return NotFound();

        item.Estado         = EstadoCola.Pendiente;
        item.ProximoIntento = null;
        item.UltimoError    = null;
        await _context.SaveChangesAsync();

        return Ok(new { mensaje = "Item reestablecido a pendiente." });
    }

    // Cancelar un item de la cola
    [HttpPost("cancelar/{itemId}")]
    public async Task<IActionResult> Cancelar(int itemId)
    {
        var item = await _context.ColaTimbrado.FindAsync(itemId);
        if (item is null) return NotFound();
        item.Estado = EstadoCola.Cancelado;
        await _context.SaveChangesAsync();
        return Ok(new { mensaje = "Item cancelado." });
    }

    // Activar diferidos que ya llegaron a su fecha
    [HttpPost("activar-diferidos")]
    public async Task<ActionResult<object>> ActivarDiferidos()
    {
        var diferidos = await _context.ColaTimbrado
            .Where(c => c.Estado == EstadoCola.Diferido
                     && c.FechaDiferido != null
                     && c.FechaDiferido <= DateTime.UtcNow)
            .ToListAsync();

        foreach (var item in diferidos)
            item.Estado = EstadoCola.Pendiente;

        await _context.SaveChangesAsync();

        return Ok(new { activados = diferidos.Count });
    }
}