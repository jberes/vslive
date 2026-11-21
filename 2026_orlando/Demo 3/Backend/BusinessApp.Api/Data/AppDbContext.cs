using BusinessApp.Api.Models.Marketing;
using BusinessApp.Api.Models.Projects;
using BusinessApp.Api.Models.Sales;
using Microsoft.EntityFrameworkCore;

namespace BusinessApp.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // Sales
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Shipper> Shippers => Set<Shipper>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderDetail> OrderDetails => Set<OrderDetail>();

    // Projects / Gantt
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectTask> ProjectTasks => Set<ProjectTask>();
    public DbSet<TaskDependency> TaskDependencies => Set<TaskDependency>();
    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<TaskAssignment> TaskAssignments => Set<TaskAssignment>();

    // Marketing
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<LeadActivity> LeadActivities => Set<LeadActivity>();

    /// <summary>SQLite julianday(); subtract two of these to get a day difference in queries.</summary>
    [DbFunction("julianday", IsBuiltIn = true)]
    public static double JulianDay(DateTime value) => throw new NotSupportedException("Only usable in LINQ to Entities.");

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // SQLite has no native decimal type; storing as REAL lets SUM/AVG/ORDER BY run in the database.
        // Reading back rounds to 4 places so binary float noise (18.3999999...) doesn't leak into the API.
        builder.Properties<decimal>().HaveConversion<DecimalToRealConverter>();
        builder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(32);
    }

    private sealed class DecimalToRealConverter() : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<decimal, double>(
        v => (double)v,
        v => FromReal(v));

    // Dividing by 1.000...m strips trailing zeros, so 18.4000m serializes as 18.4.
    private static decimal FromReal(double v) => Math.Round((decimal)v, 4) / 1.000000000000000000000000000000000m;

    protected override void OnModelCreating(ModelBuilder b)
    {
        // ---------- Sales ----------
        b.Entity<Category>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(64);
            e.HasIndex(x => x.Name).IsUnique();
        });

        b.Entity<Supplier>(e =>
        {
            e.Property(x => x.CompanyName).HasMaxLength(128);
            e.HasIndex(x => x.CompanyName);
        });

        b.Entity<Product>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(128);
            e.HasIndex(x => x.Name);
            e.HasOne(x => x.Category).WithMany(c => c.Products).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Supplier).WithMany(s => s.Products).HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Customer>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(8);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.CompanyName).HasMaxLength(128);
            e.HasIndex(x => x.CompanyName);
            e.HasIndex(x => x.Country);
        });

        b.Entity<Employee>(e =>
        {
            e.HasOne(x => x.ReportsTo).WithMany().HasForeignKey(x => x.ReportsToId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Order>(e =>
        {
            e.HasIndex(x => x.OrderDate);
            e.HasOne(x => x.Customer).WithMany(c => c.Orders).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Employee).WithMany(c => c.Orders).HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ShipVia).WithMany(c => c.Orders).HasForeignKey(x => x.ShipViaId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<OrderDetail>(e =>
        {
            e.HasKey(x => new { x.OrderId, x.ProductId });
            e.HasOne(x => x.Order).WithMany(o => o.Details).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Product).WithMany(p => p.OrderDetails).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- Projects ----------
        b.Entity<Project>(e =>
        {
            e.HasOne(x => x.Manager).WithMany().HasForeignKey(x => x.ManagerId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<ProjectTask>(e =>
        {
            e.HasIndex(x => new { x.ProjectId, x.ParentId, x.SortOrder });
            e.HasOne(x => x.Project).WithMany(p => p.Tasks).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Parent).WithMany(p => p.Children).HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<TaskDependency>(e =>
        {
            e.HasIndex(x => new { x.PredecessorId, x.SuccessorId }).IsUnique();
            e.HasOne(x => x.Predecessor).WithMany(t => t.Successors).HasForeignKey(x => x.PredecessorId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Successor).WithMany(t => t.Predecessors).HasForeignKey(x => x.SuccessorId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Resource>(e =>
        {
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<TaskAssignment>(e =>
        {
            e.HasKey(x => new { x.TaskId, x.ResourceId });
            e.HasOne(x => x.Task).WithMany(t => t.Assignments).HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Resource).WithMany(r => r.Assignments).HasForeignKey(x => x.ResourceId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- Marketing ----------
        b.Entity<Campaign>(e =>
        {
            e.HasOne(x => x.TargetCategory).WithMany().HasForeignKey(x => x.TargetCategoryId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Lead>(e =>
        {
            e.HasIndex(x => x.Status);
            e.HasOne(x => x.Campaign).WithMany(c => c.Leads).HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.ConvertedCustomer).WithMany().HasForeignKey(x => x.ConvertedCustomerId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<LeadActivity>(e =>
        {
            e.HasOne(x => x.Lead).WithMany(l => l.Activities).HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
