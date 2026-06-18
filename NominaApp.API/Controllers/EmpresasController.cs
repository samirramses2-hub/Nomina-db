using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NominaApp.Core.Entities;
using NominaApp.Infrastructure.Data;
// using NominaApp.API.DTOs; // Descomenta esto si luego haces un CrearEmpresaDto

namespace NominaApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmpresasController : ControllerBase 
{
    private readonly NominaDbContext _context;

    // 1. El constructor ahora se llama IGUAL que la clase
    public EmpresasController(NominaDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Empresa>>> GetAll()
    {
        // 2. Ahora consultamos la tabla Empresas
        return await _context.Empresas.ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Empresa>> GetById(int id)
    {
        // 3. Buscamos en la tabla Empresas
        var empresa = await _context.Empresas.FindAsync(id);

        if (empresa is null) return NotFound();
        return empresa;
    }

    // Nota: Eliminé los métodos de crear/desactivar empleados porque 
    // esa lógica debe vivir en tu EmpleadosController, no aquí.
}