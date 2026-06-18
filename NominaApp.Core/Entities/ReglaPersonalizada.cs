namespace NominaApp.Core.Entities;

public class ReglaPersonalizada
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public bool Activa { get; set; } = true;
    public int Prioridad { get; set; } = 1;
    public TipoDisparador Disparador { get; set; }
    public string CondicionJson { get; set; } = "{}";
    public string AccionJson { get; set; } = "{}";
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public int VecesEjecutada { get; set; } = 0;

    public Empresa Empresa { get; set; } = null!;
}

public enum TipoDisparador
{
    AlCalcularNomina = 1,
    AlRegistrarHoras = 2,
    AlCerrarPeriodo  = 3,
    Manual           = 4,
    PorFecha         = 5
}