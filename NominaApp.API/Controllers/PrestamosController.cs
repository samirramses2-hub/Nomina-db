using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NominaApp.Core.Entities;
using NominaApp.Infrastructure.Data;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PrestamosController : ControllerBase
{
    private readonly NominaDbContext _context;

    public PrestamosController(NominaDbContext context)
    {
        _context = context;
    }

    [HttpGet("empresa/{empresaId}")]
    public async Task<ActionResult<object>> GetByEmpresa(int empresaId)
    {
        var prestamos = await _context.Prestamos
            .Include(p => p.Empleado)
            .Include(p => p.Pagos)
            .Where(p => p.EmpresaId == empresaId)
            .OrderByDescending(p => p.FechaCreacion)
            .ToListAsync();

        return Ok(prestamos.Select(p => new
        {
            p.Id,
            p.EmpleadoId,
            empleado          = $"{p.Empleado.Nombre} {p.Empleado.ApellidoPaterno}".Trim(),
            p.MontoTotal,
            p.MontoRestante,
            p.PagoQuincenal,
            p.NumeroPagos,
            p.PagosRealizados,
            p.TasaInteres,
            estado            = p.Estado.ToString(),
            p.Concepto,
            p.Autorizador,
            fechaOtorgamiento = p.FechaOtorgamiento.ToString("dd/MM/yyyy"),
            fechaLiquidacion  = p.FechaLiquidacion?.ToString("dd/MM/yyyy"),
            pagosRestantes    = p.NumeroPagos - p.PagosRealizados,
            porcentajePagado  = Math.Round((decimal)p.PagosRealizados / p.NumeroPagos * 100, 1),
            totalPagado       = Math.Round(p.MontoTotal - p.MontoRestante, 2)
        }));
    }

    [HttpGet("{id}/amortizacion")]
    public async Task<ActionResult<object>> GetAmortizacion(int id)
    {
        var prestamo = await _context.Prestamos
            .Include(p => p.Empleado)
            .Include(p => p.Pagos)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (prestamo is null) return NotFound();

        var tabla = GenerarTablaAmortizacion(
            prestamo.MontoTotal,
            prestamo.PagoQuincenal,
            prestamo.TasaInteres,
            prestamo.NumeroPagos,
            prestamo.FechaOtorgamiento
        );

        return Ok(new
        {
            prestamo = new
            {
                prestamo.Id,
                empleado          = $"{prestamo.Empleado.Nombre} {prestamo.Empleado.ApellidoPaterno}".Trim(),
                prestamo.MontoTotal,
                prestamo.MontoRestante,
                prestamo.PagoQuincenal,
                prestamo.NumeroPagos,
                prestamo.PagosRealizados,
                prestamo.TasaInteres,
                estado            = prestamo.Estado.ToString(),
                prestamo.Concepto,
                totalPagado       = Math.Round(prestamo.MontoTotal - prestamo.MontoRestante, 2),
                totalIntereses    = Math.Round(tabla.Sum(t => t.Interes), 2)
            },
            tabla,
            pagosRealizados = prestamo.Pagos.Select(p => new
            {
                p.NumeroPago,
                p.MontoPago,
                p.MontoCapital,
                p.MontoInteres,
                p.SaldoRestante,
                fechaPago = p.FechaPago.ToString("dd/MM/yyyy"),
                p.Observaciones
            }).OrderBy(p => p.NumeroPago).ToList()
        });
    }

    [HttpPost]
    public async Task<ActionResult<object>> Crear([FromBody] CrearPrestamoDto dto)
    {
        var empleado = await _context.Empleados.FindAsync(dto.EmpleadoId);
        if (empleado is null) return NotFound("Empleado no encontrado.");

        // Verificar que no tenga préstamo activo
        var prestamoActivo = await _context.Prestamos
            .AnyAsync(p => p.EmpleadoId == dto.EmpleadoId && p.Estado == EstadoPrestamo.Activo);
        if (prestamoActivo)
            return BadRequest("El empleado ya tiene un préstamo activo. Liquídalo antes de otorgar otro.");

        // Calcular pago quincenal con interés
        var pagoQuincenal = dto.TasaInteres > 0
            ? CalcularPagoConInteres(dto.MontoTotal, dto.TasaInteres / 100m / 24, dto.NumeroPagos)
            : Math.Round(dto.MontoTotal / dto.NumeroPagos, 2);

        var prestamo = new Prestamo
        {
            EmpleadoId        = dto.EmpleadoId,
            EmpresaId         = empleado.EmpresaId,
            MontoTotal        = dto.MontoTotal,
            MontoRestante     = dto.MontoTotal,
            PagoQuincenal     = pagoQuincenal,
            NumeroPagos       = dto.NumeroPagos,
            PagosRealizados   = 0,
            TasaInteres       = dto.TasaInteres,
            FechaOtorgamiento = dto.FechaOtorgamiento,
            Concepto          = dto.Concepto,
            Autorizador       = dto.Autorizador,
            Estado            = EstadoPrestamo.Activo,
            FechaCreacion     = DateTime.UtcNow
        };

        _context.Prestamos.Add(prestamo);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            prestamo.Id,
            mensaje       = "Préstamo registrado correctamente.",
            pagoQuincenal,
            totalAPagar   = Math.Round(pagoQuincenal * dto.NumeroPagos, 2),
            totalIntereses = Math.Round(pagoQuincenal * dto.NumeroPagos - dto.MontoTotal, 2)
        });
    }

    [HttpPost("{id}/pago")]
    public async Task<ActionResult<object>> RegistrarPago(int id, [FromBody] PagoManualDto dto)
    {
        var prestamo = await _context.Prestamos.FindAsync(id);
        if (prestamo is null) return NotFound();
        if (prestamo.Estado != EstadoPrestamo.Activo)
            return BadRequest("El préstamo no está activo.");

        var interes  = Math.Round(prestamo.MontoRestante * (prestamo.TasaInteres / 100m / 24), 2);
        var capital  = Math.Round(dto.Monto - interes, 2);
        var saldoNuevo = Math.Round(prestamo.MontoRestante - capital, 2);

        if (saldoNuevo < 0) saldoNuevo = 0;

        prestamo.PagosRealizados++;
        prestamo.MontoRestante = saldoNuevo;

        if (saldoNuevo <= 0 || prestamo.PagosRealizados >= prestamo.NumeroPagos)
        {
            prestamo.Estado           = EstadoPrestamo.Liquidado;
            prestamo.FechaLiquidacion = DateTime.UtcNow;
            prestamo.MontoRestante    = 0;
        }

        _context.PagosPrestamo.Add(new PagoPrestamo
        {
            PrestamoId      = id,
            PeriodoNominaId = dto.PeriodoNominaId,
            MontoPago       = dto.Monto,
            MontoInteres    = interes,
            MontoCapital    = capital,
            SaldoRestante   = saldoNuevo,
            NumeroPago      = prestamo.PagosRealizados,
            FechaPago       = DateTime.UtcNow,
            Observaciones   = dto.Observaciones
        });

        await _context.SaveChangesAsync();

        return Ok(new
        {
            mensaje       = prestamo.Estado == EstadoPrestamo.Liquidado
                ? "Préstamo liquidado completamente." : "Pago registrado correctamente.",
            saldoRestante = saldoNuevo,
            estado        = prestamo.Estado.ToString(),
            pagosRestantes = prestamo.NumeroPagos - prestamo.PagosRealizados
        });
    }

    // Aplicar descuentos de préstamos en un periodo
    [HttpPost("aplicar-descuentos/{periodoId}")]
    public async Task<ActionResult<object>> AplicarDescuentos(int periodoId)
    {
        var periodo = await _context.PeriodosNomina
            .Include(p => p.Empresa)
            .FirstOrDefaultAsync(p => p.Id == periodoId);
        if (periodo is null) return NotFound();

        var prestamosActivos = await _context.Prestamos
            .Where(p => p.EmpresaId == periodo.EmpresaId && p.Estado == EstadoPrestamo.Activo)
            .ToListAsync();

        var descuentosAplicados = 0;
        var totalDescuentado    = 0m;

        foreach (var prestamo in prestamosActivos)
        {
            var yaDescuento = await _context.PagosPrestamo
                .AnyAsync(p => p.PrestamoId == prestamo.Id && p.PeriodoNominaId == periodoId);
            if (yaDescuento) continue;

            var interes  = Math.Round(prestamo.MontoRestante * (prestamo.TasaInteres / 100m / 24), 2);
            var capital  = Math.Round(prestamo.PagoQuincenal - interes, 2);
            var saldoNuevo = Math.Round(prestamo.MontoRestante - capital, 2);
            if (saldoNuevo < 0) saldoNuevo = 0;

            prestamo.PagosRealizados++;
            prestamo.MontoRestante = saldoNuevo;

            if (saldoNuevo <= 0 || prestamo.PagosRealizados >= prestamo.NumeroPagos)
            {
                prestamo.Estado           = EstadoPrestamo.Liquidado;
                prestamo.FechaLiquidacion = DateTime.UtcNow;
                prestamo.MontoRestante    = 0;
            }

            _context.PagosPrestamo.Add(new PagoPrestamo
            {
                PrestamoId      = prestamo.Id,
                PeriodoNominaId = periodoId,
                MontoPago       = prestamo.PagoQuincenal,
                MontoInteres    = interes,
                MontoCapital    = capital,
                SaldoRestante   = saldoNuevo,
                NumeroPago      = prestamo.PagosRealizados,
                FechaPago       = DateTime.UtcNow,
                Observaciones   = $"Descuento automático periodo {periodo.FechaInicio:dd/MM/yyyy}-{periodo.FechaFin:dd/MM/yyyy}"
            });

            // Agregar como incidencia de descuento en nómina
            _context.Incidencias.Add(new Incidencia
            {
                EmpleadoId      = prestamo.EmpleadoId,
                PeriodoNominaId = periodoId,
                Tipo            = TipoIncidencia.DescuentoInfonavit,
                Cantidad        = prestamo.PagoQuincenal,
                Observaciones   = $"Descuento préstamo #{prestamo.Id} — pago {prestamo.PagosRealizados}/{prestamo.NumeroPagos}",
                FechaRegistro   = DateTime.UtcNow
            });

            descuentosAplicados++;
            totalDescuentado += prestamo.PagoQuincenal;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            mensaje             = $"{descuentosAplicados} descuento(s) aplicados automáticamente.",
            descuentosAplicados,
            totalDescuentado    = Math.Round(totalDescuentado, 2),
            periodo             = $"{periodo.FechaInicio:dd/MM/yyyy} — {periodo.FechaFin:dd/MM/yyyy}"
        });
    }

    private List<FilaAmortizacion> GenerarTablaAmortizacion(
        decimal monto, decimal pago, decimal tasaAnual, int pagos, DateTime fechaInicio)
    {
        var tabla  = new List<FilaAmortizacion>();
        var saldo  = monto;
        var tasaPer = tasaAnual / 100m / 24;
        var fecha  = fechaInicio;

        for (int i = 1; i <= pagos; i++)
        {
            var interes  = Math.Round(saldo * tasaPer, 2);
            var capital  = Math.Round(pago - interes, 2);
            saldo        = Math.Round(saldo - capital, 2);
            if (saldo < 0) saldo = 0;

            tabla.Add(new FilaAmortizacion
            {
                Numero    = i,
                Fecha     = fecha.AddDays(i * 15).ToString("dd/MM/yyyy"),
                Pago      = pago,
                Capital   = capital,
                Interes   = interes,
                Saldo     = saldo
            });

            if (saldo == 0) break;
        }

        return tabla;
    }

    private decimal CalcularPagoConInteres(decimal monto, decimal tasaPer, int pagos)
    {
        if (tasaPer == 0) return Math.Round(monto / pagos, 2);
        var factor = (decimal)Math.Pow((double)(1 + tasaPer), pagos);
        return Math.Round(monto * tasaPer * factor / (factor - 1), 2);
    }
}

public class FilaAmortizacion
{
    public int     Numero  { get; set; }
    public string  Fecha   { get; set; } = string.Empty;
    public decimal Pago    { get; set; }
    public decimal Capital { get; set; }
    public decimal Interes { get; set; }
    public decimal Saldo   { get; set; }
}

public class CrearPrestamoDto
{
    public int      EmpleadoId        { get; set; }
    public decimal  MontoTotal        { get; set; }
    public int      NumeroPagos       { get; set; }
    public decimal  TasaInteres       { get; set; } = 0;
    public DateTime FechaOtorgamiento { get; set; }
    public string?  Concepto          { get; set; }
    public string?  Autorizador       { get; set; }
}

public class PagoManualDto
{
    public decimal Monto          { get; set; }
    public int?    PeriodoNominaId { get; set; }
    public string? Observaciones  { get; set; }
}