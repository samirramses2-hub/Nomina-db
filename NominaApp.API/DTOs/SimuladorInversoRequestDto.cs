namespace NominaApp.API.DTOs;

public class SimuladorInversoRequestDto
{
    public decimal SueldoNetoDeseado { get; set; }
    public int DiasPeriodo { get; set; } = 30; // Por defecto mensual
    public int EjercicioFiscal { get; set; } = 2025; // Por defecto el año actual o próximo
}
