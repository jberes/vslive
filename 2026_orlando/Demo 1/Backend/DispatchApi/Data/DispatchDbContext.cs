using DispatchApi.Models;
using Microsoft.EntityFrameworkCore;

namespace DispatchApi.Data;

public class DispatchDbContext(DbContextOptions<DispatchDbContext> options) : DbContext(options)
{
    public DbSet<SeedMeta> SeedMeta => Set<SeedMeta>();
    public DbSet<Region> Regions => Set<Region>();
    public DbSet<Depot> Depots => Set<Depot>();
    public DbSet<Dispatcher> Dispatchers => Set<Dispatcher>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Technician> Technicians => Set<Technician>();
    public DbSet<TechnicianSkill> TechnicianSkills => Set<TechnicianSkill>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<TimeOffRequest> TimeOffRequests => Set<TimeOffRequest>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<SlaPolicy> SlaPolicies => Set<SlaPolicy>();
    public DbSet<JobType> JobTypes => Set<JobType>();
    public DbSet<Part> Parts => Set<Part>();
    public DbSet<DepotInventory> DepotInventory => Set<DepotInventory>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobEvent> JobEvents => Set<JobEvent>();
    public DbSet<JobNote> JobNotes => Set<JobNote>();
    public DbSet<JobPart> JobParts => Set<JobPart>();
    public DbSet<Reschedule> Reschedules => Set<Reschedule>();

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        // Every enum is persisted as its name (TEXT column with CHECK constraint in schema.sql)
        b.Properties<Enum>().HaveConversion<string>();
    }

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<SeedMeta>().ToTable("SeedMeta").Property(x => x.Id).ValueGeneratedNever();
        m.Entity<DepotInventory>().ToTable("DepotInventory").HasKey(x => new { x.DepotId, x.PartId });
        m.Entity<TechnicianSkill>().HasKey(x => new { x.TechnicianId, x.SkillId });
        m.Entity<TechnicianSkill>().HasOne(x => x.Technician).WithMany(t => t.Skills).HasForeignKey(x => x.TechnicianId);

        m.Entity<Shift>().HasOne(x => x.Technician).WithMany(t => t.Shifts).HasForeignKey(x => x.TechnicianId);
        m.Entity<TimeOffRequest>().HasOne(x => x.Technician).WithMany(t => t.TimeOffRequests).HasForeignKey(x => x.TechnicianId);
        m.Entity<JobType>().HasOne(x => x.RequiredSkill).WithMany().HasForeignKey(x => x.RequiredSkillId);

        m.Entity<Job>(e =>
        {
            e.HasOne(x => x.AssignedTechnician).WithMany().HasForeignKey(x => x.AssignedTechnicianId);
            e.HasOne(x => x.AssignedByDispatcher).WithMany().HasForeignKey(x => x.AssignedByDispatcherId);
            e.HasMany(x => x.Events).WithOne(x => x.Job).HasForeignKey(x => x.JobId);
            e.HasMany(x => x.Notes).WithOne(x => x.Job).HasForeignKey(x => x.JobId);
            e.HasMany(x => x.Parts).WithOne(x => x.Job).HasForeignKey(x => x.JobId);
            e.HasMany(x => x.Reschedules).WithOne(x => x.Job).HasForeignKey(x => x.JobId);
            e.Ignore(x => x.IsOpen);
        });

        m.Entity<Reschedule>(e =>
        {
            e.HasOne(x => x.FromTechnician).WithMany().HasForeignKey(x => x.FromTechnicianId);
            e.HasOne(x => x.ToTechnician).WithMany().HasForeignKey(x => x.ToTechnicianId);
            e.HasOne(x => x.Dispatcher).WithMany().HasForeignKey(x => x.DispatcherId);
        });
    }
}
