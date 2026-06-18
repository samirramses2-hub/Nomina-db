using Microsoft.EntityFrameworkCore;
using NominaApp.Core.Entities;


namespace NominaApp.Infrastructure.Data;

public class NominaDbContext : DbContext
{
    public NominaDbContext(DbContextOptions<NominaDbContext> options) : base(options) { }

    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Empleado> Empleados => Set<Empleado>();
    public DbSet<PeriodoNomina> PeriodosNomina => Set<PeriodoNomina>();
    public DbSet<Incidencia> Incidencias => Set<Incidencia>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<DeclaracionPTU> DeclaracionesPTU => Set<DeclaracionPTU>();
    public DbSet<MovimientoIMSS> MovimientosIMSS => Set<MovimientoIMSS>();
    public DbSet<HistorialSalarial> HistorialSalarial => Set<HistorialSalarial>();
    public DbSet<CFDIRegistro> CFDIs => Set<CFDIRegistro>();
    public DbSet<ColaTimbrado> ColaTimbrado => Set<ColaTimbrado>();
    public DbSet<Asistencia> Asistencias => Set<Asistencia>();
    public DbSet<HorarioEmpleado> HorariosEmpleados => Set<HorarioEmpleado>();
    public DbSet<Prestamo> Prestamos => Set<Prestamo>();
    public DbSet<PagoPrestamo> PagosPrestamo => Set<PagoPrestamo>();
    public DbSet<UsuarioEmpresa> UsuariosEmpresas => Set<UsuarioEmpresa>();
    public DbSet<Departamento> Departamentos => Set<Departamento>();
    public DbSet<SolicitudVacaciones> SolicitudesVacaciones => Set<SolicitudVacaciones>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Empresa
        modelBuilder.Entity<Empresa>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.RFC).HasMaxLength(13).IsRequired();
            e.Property(x => x.RazonSocial).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.RFC).IsUnique();
        });

        // Empleado
        modelBuilder.Entity<Empleado>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.RFC).HasMaxLength(13).IsRequired();
            e.Property(x => x.CURP).HasMaxLength(18).IsRequired();
            e.Property(x => x.NSS).HasMaxLength(11).IsRequired();
            e.Property(x => x.SalarioDiario).HasPrecision(18, 4);
            e.HasOne(x => x.Empresa)
             .WithMany(x => x.Empleados)
             .HasForeignKey(x => x.EmpresaId);
        });

        // PeriodoNomina
        modelBuilder.Entity<PeriodoNomina>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Empresa)
             .WithMany()
             .HasForeignKey(x => x.EmpresaId);
             e.Property(x => x.NumeroPeriodo).HasDefaultValue(0);
            e.Property(x => x.Descripcion).HasMaxLength(100);
            e.Property(x => x.FechaPago).HasDefaultValueSql("GETUTCDATE()");
        });

        // Incidencia
        modelBuilder.Entity<Incidencia>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Cantidad).HasPrecision(18, 4);
            e.HasOne(x => x.Empleado)
             .WithMany(x => x.Incidencias)
             .HasForeignKey(x => x.EmpleadoId);
            e.HasOne(x => x.PeriodoNomina)
             .WithMany(x => x.Incidencias)
             .HasForeignKey(x => x.PeriodoNominaId);
        });

        modelBuilder.Entity<Usuario>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Nombre).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<MovimientoIMSS>(e =>
        {
            e.HasOne(m => m.Empleado)
            .WithMany()
            .HasForeignKey(m => m.EmpleadoId)
            .OnDelete(DeleteBehavior.NoAction);

            e.HasOne(m => m.Empresa)
            .WithMany()
            .HasForeignKey(m => m.EmpresaId)
            .OnDelete(DeleteBehavior.NoAction);
        });
        

        modelBuilder.Entity<CFDIRegistro>(e =>
        {
            e.HasOne(c => c.Empleado)
            .WithMany()
            .HasForeignKey(c => c.EmpleadoId)
            .OnDelete(DeleteBehavior.NoAction);

            e.HasOne(c => c.PeriodoNomina)
            .WithMany()
            .HasForeignKey(c => c.PeriodoNominaId)
            .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ColaTimbrado>(e =>
        {
            e.HasOne(c => c.Empleado)
            .WithMany()
            .HasForeignKey(c => c.EmpleadoId)
            .OnDelete(DeleteBehavior.NoAction);

            e.HasOne(c => c.PeriodoNomina)
            .WithMany()
            .HasForeignKey(c => c.PeriodoNominaId)
            .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Incidencia>(e =>
        {
            e.HasOne(i => i.Empleado)
            .WithMany()
            .HasForeignKey(i => i.EmpleadoId)
            .OnDelete(DeleteBehavior.NoAction);

            e.HasOne(i => i.PeriodoNomina)
            .WithMany()
            .HasForeignKey(i => i.PeriodoNominaId)
            .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<HistorialSalarial>(e =>
        {
            e.HasOne(h => h.Empleado)
            .WithMany()
            .HasForeignKey(h => h.EmpleadoId)
            .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ReglaPersonalizada>(e =>
        {
            e.HasOne(r => r.Empresa)
            .WithMany()
            .HasForeignKey(r => r.EmpresaId)
            .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Asistencia>(e =>
            {
                e.HasOne(a => a.Empleado)
                .WithMany()
                .HasForeignKey(a => a.EmpleadoId)
                .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<HorarioEmpleado>(e =>
            {
                e.HasOne(h => h.Empleado)
                .WithMany()
                .HasForeignKey(h => h.EmpleadoId)
                .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<Prestamo>(e =>
        {
            e.HasOne(p => p.Empleado)
            .WithMany()
            .HasForeignKey(p => p.EmpleadoId)
            .OnDelete(DeleteBehavior.NoAction);

            e.HasOne(p => p.Empresa)
            .WithMany()
            .HasForeignKey(p => p.EmpresaId)
            .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<PagoPrestamo>(e =>
        {
            e.HasOne(p => p.Prestamo)
            .WithMany(p => p.Pagos)
            .HasForeignKey(p => p.PrestamoId)
            .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<UsuarioEmpresa>(e =>
        {
            e.HasOne(ue => ue.Usuario)
            .WithMany()
            .HasForeignKey(ue => ue.UsuarioId)
            .OnDelete(DeleteBehavior.NoAction);

            e.HasOne(ue => ue.Empresa)
            .WithMany()
            .HasForeignKey(ue => ue.EmpresaId)
            .OnDelete(DeleteBehavior.NoAction);

            e.HasIndex(ue => new { ue.UsuarioId, ue.EmpresaId }).IsUnique();
        });

        modelBuilder.Entity<Departamento>(e =>
        {
            e.HasOne(d => d.Empresa)
            .WithMany()
            .HasForeignKey(d => d.EmpresaId)
            .OnDelete(DeleteBehavior.NoAction);

            e.HasOne(d => d.Jefe)
            .WithMany()
            .HasForeignKey(d => d.JefeId)
            .OnDelete(DeleteBehavior.NoAction);

            e.HasOne(d => d.DepartamentoPadre)
            .WithMany()
            .HasForeignKey(d => d.DepartamentoPadreId)
            .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Empleado>(e =>
        {
            e.HasOne(emp => emp.Departamento)
            .WithMany(d => d.Empleados)
            .HasForeignKey(emp => emp.DepartamentoId)
            .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<SolicitudVacaciones>(e =>
        {
            e.HasOne(s => s.Empleado)
            .WithMany()
            .HasForeignKey(s => s.EmpleadoId)
            .OnDelete(DeleteBehavior.NoAction);
        });
    }
}

