using Microsoft.AspNetCore.Mvc;
using NominaApp.Core.Entities;
using NominaApp.Infrastructure.Data;

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SeedController : ControllerBase
{
    private readonly NominaDbContext _context;

    public SeedController(NominaDbContext context)
    {
        _context = context;
    }

    [HttpPost("datos-prueba")]
    public async Task<ActionResult<object>> CrearDatosPrueba()
    {
        // Empresa
        var empresa = new Empresa
        {
            RazonSocial    = "Tecnologías del Noroeste SA de CV",
            RFC            = "TNO200101AB1",
            RegimenFiscal  = "601",
            DomicilioFiscal = "Ensenada, Baja California"
        };
        _context.Empresas.Add(empresa);
        await _context.SaveChangesAsync();

        // Empleados
        var empleados = new List<Empleado>
        {
            new Empleado { EmpresaId = empresa.Id, CodigoEmpleado = "EMP001", Nombre = "Carlos", ApellidoPaterno = "Ramírez", ApellidoMaterno = "López", RFC = "RALC850101AB1", CURP = "RALC850101HBCLPN01", NSS = "12345678901", SalarioDiario = 450m, TipoContrato = TipoContrato.TiempoIndefinido, TipoPeriodo = TipoPeriodo.Quincenal, FechaIngreso = new DateTime(2020, 1, 15), Activo = true, Banco = "BBVA", CLABE = "012345678901234567", Puesto = "Gerente General", FechaCreacion = DateTime.UtcNow },
            new Empleado { EmpresaId = empresa.Id, CodigoEmpleado = "EMP002", Nombre = "María", ApellidoPaterno = "González", ApellidoMaterno = "Pérez", RFC = "GOPM900215CD2", CURP = "GOPM900215MBCNRL02", NSS = "12345678902", SalarioDiario = 380m, TipoContrato = TipoContrato.TiempoIndefinido, TipoPeriodo = TipoPeriodo.Quincenal, FechaIngreso = new DateTime(2021, 3, 1), Activo = true, Banco = "BANAMEX", CLABE = "012345678901234568", Puesto = "Contadora", FechaCreacion = DateTime.UtcNow },
            new Empleado { EmpresaId = empresa.Id, CodigoEmpleado = "EMP003", Nombre = "Luis", ApellidoPaterno = "Hernández", ApellidoMaterno = "Torres", RFC = "HETL951010EF3", CURP = "HETL951010HBCRNL03", NSS = "12345678903", SalarioDiario = 320m, TipoContrato = TipoContrato.TiempoIndefinido, TipoPeriodo = TipoPeriodo.Quincenal, FechaIngreso = new DateTime(2022, 6, 15), Activo = true, Banco = "SANTANDER", CLABE = "012345678901234569", Puesto = "Desarrollador", FechaCreacion = DateTime.UtcNow },
            new Empleado { EmpresaId = empresa.Id, CodigoEmpleado = "EMP004", Nombre = "Ana", ApellidoPaterno = "Martínez", ApellidoMaterno = "Ruiz", RFC = "MARA880520GH4", CURP = "MARA880520MBCRZN04", NSS = "12345678904", SalarioDiario = 350m, TipoContrato = TipoContrato.TiempoIndefinido, TipoPeriodo = TipoPeriodo.Quincenal, FechaIngreso = new DateTime(2021, 9, 1), Activo = true, Banco = "BANORTE", CLABE = "012345678901234570", Puesto = "Vendedora", FechaCreacion = DateTime.UtcNow },
            new Empleado { EmpresaId = empresa.Id, CodigoEmpleado = "EMP005", Nombre = "Roberto", ApellidoPaterno = "Sánchez", ApellidoMaterno = "Flores", RFC = "SAFR920305IJ5", CURP = "SAFR920305HBCNLB05", NSS = "12345678905", SalarioDiario = 290m, TipoContrato = TipoContrato.TiempoIndefinido, TipoPeriodo = TipoPeriodo.Quincenal, FechaIngreso = new DateTime(2023, 1, 10), Activo = true, Banco = "BBVA", CLABE = "012345678901234571", Puesto = "Soporte Técnico", FechaCreacion = DateTime.UtcNow },
            new Empleado { EmpresaId = empresa.Id, CodigoEmpleado = "EMP006", Nombre = "Laura", ApellidoPaterno = "Vázquez", ApellidoMaterno = "Morales", RFC = "VAML870714KL6", CURP = "VAML870714MBCZRL06", NSS = "12345678906", SalarioDiario = 410m, TipoContrato = TipoContrato.TiempoIndefinido, TipoPeriodo = TipoPeriodo.Quincenal, FechaIngreso = new DateTime(2019, 5, 20), Activo = true, Banco = "HSBC", CLABE = "012345678901234572", Puesto = "Directora de RH", FechaCreacion = DateTime.UtcNow },
        };
        _context.Empleados.AddRange(empleados);
        await _context.SaveChangesAsync();

        // Departamentos
        var deptos = new List<Departamento>
        {
            new Departamento { EmpresaId = empresa.Id, Nombre = "Dirección General",  CodigoDepartamento = "DIR", JefeId = empleados[0].Id, Activo = true, FechaCreacion = DateTime.UtcNow },
            new Departamento { EmpresaId = empresa.Id, Nombre = "Administración",      CodigoDepartamento = "ADM", JefeId = empleados[1].Id, Activo = true, FechaCreacion = DateTime.UtcNow },
            new Departamento { EmpresaId = empresa.Id, Nombre = "Tecnología",          CodigoDepartamento = "TEC", JefeId = empleados[2].Id, Activo = true, FechaCreacion = DateTime.UtcNow },
            new Departamento { EmpresaId = empresa.Id, Nombre = "Ventas",              CodigoDepartamento = "VEN", JefeId = empleados[3].Id, Activo = true, FechaCreacion = DateTime.UtcNow },
            new Departamento { EmpresaId = empresa.Id, Nombre = "Recursos Humanos",    CodigoDepartamento = "RH",  JefeId = empleados[5].Id, Activo = true, FechaCreacion = DateTime.UtcNow },
        };
        _context.Departamentos.AddRange(deptos);
        await _context.SaveChangesAsync();

        // Asignar empleados a departamentos
        empleados[0].DepartamentoId = deptos[0].Id;
        empleados[1].DepartamentoId = deptos[1].Id;
        empleados[2].DepartamentoId = deptos[2].Id;
        empleados[3].DepartamentoId = deptos[3].Id;
        empleados[4].DepartamentoId = deptos[2].Id;
        empleados[5].DepartamentoId = deptos[4].Id;
        await _context.SaveChangesAsync();

        // Periodos quincenales 2025
        var periodos = new List<PeriodoNomina>();
        for (int i = 1; i <= 6; i++)
        {
            var inicio = i % 2 == 1
                ? new DateTime(2025, (i + 1) / 2, 1)
                : new DateTime(2025, i / 2, 16);
            var fin = i % 2 == 1
                ? new DateTime(2025, (i + 1) / 2, 15)
                : new DateTime(2025, i / 2, DateTime.DaysInMonth(2025, i / 2));
            periodos.Add(new PeriodoNomina
            {
                EmpresaId       = empresa.Id,
                NumeroPeriodo   = i,
                Descripcion     = $"Quincena {i} 2025",
                FechaInicio     = inicio,
                FechaFin        = fin,
                FechaPago       = fin.AddDays(3),
                TipoPeriodo     = TipoPeriodo.Quincenal,
                EjercicioFiscal = 2025,
                Estado          = i < 6 ? EstadoPeriodo.Cerrado : EstadoPeriodo.Abierto,
                FechaCreacion   = DateTime.UtcNow
            });
        }
        _context.PeriodosNomina.AddRange(periodos);
        await _context.SaveChangesAsync();

        // Incidencias en periodos cerrados
        var random = new Random(42);
        foreach (var periodo in periodos.Take(5))
        {
            foreach (var emp in empleados)
            {
                if (random.Next(0, 3) == 0)
                    _context.Incidencias.Add(new Incidencia { EmpleadoId = emp.Id, PeriodoNominaId = periodo.Id, Tipo = TipoIncidencia.HoraExtraSimple, Cantidad = random.Next(2, 8), FechaRegistro = DateTime.UtcNow });
                if (random.Next(0, 5) == 0)
                    _context.Incidencias.Add(new Incidencia { EmpleadoId = emp.Id, PeriodoNominaId = periodo.Id, Tipo = TipoIncidencia.Bono, Cantidad = random.Next(500, 2000), FechaRegistro = DateTime.UtcNow });
            }
        }
        await _context.SaveChangesAsync();

        // Historial salarial
        foreach (var emp in empleados)
        {
            _context.HistorialSalarial.Add(new HistorialSalarial
            {
                EmpleadoId    = emp.Id,
                SalarioDiario = emp.SalarioDiario,
                FechaVigencia = emp.FechaIngreso,
                Motivo        = "Salario inicial de contratación",
                Activo        = true,
                FechaRegistro = DateTime.UtcNow
            });
        }
        await _context.SaveChangesAsync();

        return Ok(new
        {
            mensaje         = "Datos de prueba creados correctamente.",
            empresaId       = empresa.Id,
            empresa         = empresa.RazonSocial,
            empleados       = empleados.Count,
            departamentos   = deptos.Count,
            periodos        = periodos.Count,
            credenciales    = "Usa admin@nomina.com / Admin123! para iniciar sesión"
        });
    }
}