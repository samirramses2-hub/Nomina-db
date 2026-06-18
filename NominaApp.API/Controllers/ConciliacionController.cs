using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NominaApp.Infrastructure.Data;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConciliacionController : ControllerBase
{
    private readonly NominaDbContext _context;

    public ConciliacionController(NominaDbContext context)
    {
        _context = context;
    }

    [HttpGet("{empresaId}/{ejercicio}/{mes}")]
    public async Task<ActionResult<object>> AuditoriaSAT(int empresaId, int ejercicio, int mes)
    {
        // 1. Obtener los CFDI registrados en nuestro sistema para ese mes
        var cfdisLocal = await _context.CFDIs
            .Include(c => c.Empleado)
            .Include(c => c.PeriodoNomina)
            .Where(c => c.PeriodoNomina.EmpresaId == empresaId 
                     && c.FechaTimbrado.Year == ejercicio 
                     && c.FechaTimbrado.Month == mes)
            .ToListAsync();

        // 2. SIMULADOR SAT: Creamos una lista "fake" del SAT basada en nuestros datos, inyectando errores
        var cfdisSAT = new List<dynamic>();
        var discrepancias = new List<object>();

        int index = 0;
        foreach (var local in cfdisLocal)
        {
            index++;
            string estadoSat = local.Estado == Core.Entities.EstadoCFDI.Vigente ? "Vigente" : "Cancelado";
            decimal totalSat = local.Total;
            string observaciones = "OK";
            string tipoDiferencia = "Ninguna";

            // Inyectar errores para demostración
            if (index == 2)
            {
                // Error 1: Cancelado en SAT pero activo en sistema
                estadoSat = "Cancelado";
                observaciones = "El recibo aparece como cancelado en el portal del SAT.";
                tipoDiferencia = "Estado Mismatch";
            }
            else if (index == 4)
            {
                // Error 2: Diferencia de montos
                totalSat = local.Total + 0.50m;
                observaciones = "El total en el SAT difiere por $0.50 MXN.";
                tipoDiferencia = "Diferencia Monto";
            }
            
            var satItem = new
            {
                uuid = local.UUID ?? $"MOCK-UUID-{index}",
                rfcEmisor = "MOCK_EMPRESA_RFC",
                rfcReceptor = local.Empleado.RFC,
                fechaEmision = local.FechaTimbrado,
                total = totalSat,
                estado = estadoSat
            };

            cfdisSAT.Add(satItem);

            // Comparación
            bool tieneError = false;
            
            if (local.Estado == Core.Entities.EstadoCFDI.Vigente && satItem.estado == "Cancelado")
                tieneError = true;
            if (local.Total != satItem.total)
                tieneError = true;

            if (tieneError)
            {
                discrepancias.Add(new
                {
                    uuid = satItem.uuid,
                    empleado = $"{local.Empleado.Nombre} {local.Empleado.ApellidoPaterno}",
                    rfc = satItem.rfcReceptor,
                    fecha = local.FechaTimbrado.ToString("dd/MM/yyyy"),
                    montoSistema = local.Total,
                    montoSat = satItem.total,
                    estadoSistema = local.Estado.ToString(),
                    estadoSat = satItem.estado,
                    observaciones,
                    tipoDiferencia
                });
            }
        }

        // Agregar un recibo "fantasma" que está en el SAT pero no en el sistema
        if (cfdisLocal.Count > 0)
        {
            var fantasma = new
            {
                uuid = Guid.NewGuid().ToString().ToUpper(),
                rfcEmisor = "MOCK_EMPRESA_RFC",
                rfcReceptor = cfdisLocal.First().Empleado.RFC,
                fechaEmision = new DateTime(ejercicio, mes, 15),
                total = 15000.00m,
                estado = "Vigente"
            };
            cfdisSAT.Add(fantasma);
            
            discrepancias.Add(new
            {
                uuid = fantasma.uuid,
                empleado = "Desconocido (Asignado al RFC)",
                rfc = fantasma.rfcReceptor,
                fecha = fantasma.fechaEmision.ToString("dd/MM/yyyy"),
                montoSistema = 0m,
                montoSat = fantasma.total,
                estadoSistema = "No Existe",
                estadoSat = fantasma.estado,
                observaciones = "Timbrado en el SAT pero no registrado en nuestro sistema.",
                tipoDiferencia = "Faltante Sistema"
            });
        }

        return Ok(new
        {
            resumen = new
            {
                totalSistema = cfdisLocal.Count,
                totalSat = cfdisSAT.Count,
                totalDiscrepancias = discrepancias.Count,
                montoTotalSistema = cfdisLocal.Sum(c => c.Total),
                montoTotalSat = cfdisSAT.Sum(c => (decimal)c.GetType().GetProperty("total").GetValue(c))
            },
            discrepancias
        });
    }
}
