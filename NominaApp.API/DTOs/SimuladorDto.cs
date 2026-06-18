namespace NominaApp.API.DTOs;

public class SimuladorRequestDto
{
    public decimal SalarioDiario { get; set; }
    public int DiasPeriodo { get; set; } = 15;
    public int EjercicioFiscal { get; set; } = 2025;
    public decimal FaltasInjustificadas { get; set; }
    public decimal FaltasJustificadas { get; set; }
    public decimal HorasExtraSimples { get; set; }
    public decimal HorasExtraDobles { get; set; }
    public decimal Bonos { get; set; }
    public decimal DiasVacaciones { get; set; }
    public decimal DiasPrimaDominical { get; set; }
}