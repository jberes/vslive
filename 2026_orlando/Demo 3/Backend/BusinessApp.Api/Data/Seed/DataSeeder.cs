using Bogus;
using BusinessApp.Api.Models.Marketing;
using BusinessApp.Api.Models.Projects;
using BusinessApp.Api.Models.Sales;
using Microsoft.EntityFrameworkCore;

namespace BusinessApp.Api.Data.Seed;

public sealed class SeedOptions
{
    public int CustomerCount { get; set; } = 500;
    public int OrderCount { get; set; } = 5000;
    public int CampaignCount { get; set; } = 48;
    public int LeadCount { get; set; } = 2000;
    public int RandomSeed { get; set; } = 8675309;
}

/// <summary>
/// Deterministic generator for a realistic, internally consistent data set. Dates are anchored on
/// "today" so dashboards always show recent activity.
/// </summary>
public sealed class DataSeeder(AppDbContext db, ILogger<DataSeeder> logger, SeedOptions options)
{
    private readonly Random _rng = new(options.RandomSeed);
    private readonly DateTime _today = DateTime.Today;

    private static readonly string[] ContactTitles =
    [
        "Sales Representative", "Owner", "Order Administrator", "Marketing Manager", "Accounting Manager",
        "Sales Agent", "Sales Associate", "Purchasing Manager", "Marketing Assistant", "Sales Manager",
        "Assistant Sales Agent", "Operations Manager", "Buyer",
    ];

    private static readonly string[] CompanySuffixes =
    [
        "Markets", "Grocers", "Foods", "Delicatessen", "Trading Co.", "Imports", "Gourmet", "Provisions",
        "Fine Foods", "Wholesale", "Market Hall", "Pantry", "Food Hall", "Supply", "& Sons", "Specialty Foods",
    ];

    // (Country, Bogus locale, weight)
    private static readonly (string Country, string Locale, int Weight)[] Countries =
    [
        ("USA", "en_US", 18), ("Germany", "de", 13), ("France", "fr", 10), ("UK", "en_GB", 9),
        ("Brazil", "pt_BR", 7), ("Spain", "es", 5), ("Mexico", "es_MX", 5), ("Canada", "en_CA", 5),
        ("Italy", "it", 5), ("Sweden", "sv", 3), ("Austria", "de_AT", 3), ("Switzerland", "de_CH", 2),
        ("Ireland", "en_IE", 2), ("Netherlands", "nl", 3), ("Australia", "en_AU", 3), ("Norway", "nb_NO", 2),
        ("Finland", "fi", 2), ("Poland", "pl", 2), ("Portugal", "pt_PT", 2),
    ];

    /// <returns><c>true</c> when data was generated; <c>false</c> when the database already had data.</returns>
    public async Task<bool> SeedAsync(CancellationToken ct = default)
    {
        if (await db.Categories.AnyAsync(ct))
        {
            logger.LogInformation("Database already contains data; skipping seed.");
            return false;
        }

        Randomizer.Seed = new Random(options.RandomSeed);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        db.ChangeTracker.AutoDetectChangesEnabled = false;

        var categories = NorthwindReferenceData.Categories
            .Select(c => new Category { Name = c.Name, Description = c.Description }).ToList();
        db.Categories.AddRange(categories);

        var suppliers = NorthwindReferenceData.Suppliers().ToList();
        foreach (var s in suppliers) s.HomePage = $"https://www.{Slug(s.CompanyName)}.example.com";
        db.Suppliers.AddRange(suppliers);
        await db.SaveChangesAsync(ct);

        var products = NorthwindReferenceData.Products.Select(p => new Product
        {
            Name = p.Item1,
            SupplierId = suppliers[p.Item2 - 1].Id,
            CategoryId = categories[p.Item3 - 1].Id,
            QuantityPerUnit = p.Item4,
            UnitPrice = p.Item5,
            UnitsInStock = p.Item6,
            UnitsOnOrder = p.Item7,
            ReorderLevel = p.Item8,
            Discontinued = p.Item9,
        }).ToList();
        db.Products.AddRange(products);

        var employees = SeedEmployees();
        var shippers = NorthwindReferenceData.Shippers.Select(s => new Shipper { CompanyName = s.Name, Phone = s.Phone }).ToList();
        db.Shippers.AddRange(shippers);

        var customers = SeedCustomers();
        await db.SaveChangesAsync(ct);

        SeedOrders(customers, employees, shippers, products);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();

        var resources = SeedResources(employees);
        await db.SaveChangesAsync(ct);
        SeedProjects(resources, customers);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();

        SeedMarketing(categories, employees, customers);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        db.ChangeTracker.AutoDetectChangesEnabled = true;

        logger.LogInformation("Database seeded in {Elapsed} ms", sw.ElapsedMilliseconds);
        return true;
    }

    // ---------------------------------------------------------------- Sales

    private List<Employee> SeedEmployees()
    {
        var faker = new Faker("en");
        var list = new List<Employee>();
        foreach (var e in NorthwindReferenceData.Employees)
        {
            var hire = _today.AddYears(-_rng.Next(2, 15)).AddDays(-_rng.Next(0, 365));
            list.Add(new Employee
            {
                FirstName = e.Item1,
                LastName = e.Item2,
                Title = e.Item3,
                TitleOfCourtesy = e.Item4,
                City = e.Item5,
                Region = e.Item6,
                PostalCode = e.Item7,
                Country = e.Item8,
                Address = e.Item9,
                Extension = e.Item10,
                Email = $"{e.Item1.ToLowerInvariant()}.{e.Item2.ToLowerInvariant()}@northwind.example.com",
                HomePhone = e.Item8 == "USA" ? faker.Phone.PhoneNumber("(206) 555-####") : faker.Phone.PhoneNumber("(71) 555-####"),
                BirthDate = _today.AddYears(-_rng.Next(26, 62)).AddDays(-_rng.Next(0, 365)),
                HireDate = hire,
                Notes = $"{e.Item1} joined Northwind in {hire:yyyy} as {Article(e.Item3)} {e.Item3}. {faker.Lorem.Sentence(12)}",
            });
        }
        db.Employees.AddRange(list);
        db.SaveChanges();

        for (var i = 0; i < list.Count; i++)
        {
            var reportsTo = NorthwindReferenceData.Employees[i].Item11;
            if (reportsTo is int r) list[i].ReportsToId = list[r - 1].Id;
        }
        db.ChangeTracker.DetectChanges();
        return list;
    }

    private List<Customer> SeedCustomers()
    {
        var customers = new List<Customer>();
        var codes = new HashSet<string>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var c in NorthwindReferenceData.ClassicCustomers)
        {
            codes.Add(c.Item1);
            names.Add(c.Item2);
            customers.Add(new Customer
            {
                Code = c.Item1, CompanyName = c.Item2, ContactName = c.Item3, ContactTitle = c.Item4,
                Address = c.Item5, City = c.Item6, Region = c.Item7, PostalCode = c.Item8, Country = c.Item9,
                Phone = c.Item10, Fax = c.Item11,
                Email = $"{Slug(c.Item3.Split(' ')[0])}@{Slug(c.Item2)}.example.com",
                CreatedAt = _today.AddYears(-4).AddDays(-_rng.Next(0, 400)),
            });
        }

        var fakers = new Dictionary<string, Faker>();
        var totalWeight = Countries.Sum(c => c.Weight);

        while (customers.Count < options.CustomerCount)
        {
            var roll = _rng.Next(totalWeight);
            var country = Countries.First(c => (roll -= c.Weight) < 0);
            if (!fakers.TryGetValue(country.Locale, out var f)) fakers[country.Locale] = f = new Faker(country.Locale);

            var first = f.Name.FirstName();
            var last = f.Name.LastName();
            var city = f.Address.City();
            var company = _rng.Next(10) switch
            {
                < 4 => f.Company.CompanyName(),
                < 7 => $"{last} {Pick(CompanySuffixes)}",
                _ => $"{city} {Pick(CompanySuffixes)}",
            };
            if (!names.Add(company)) continue;

            var code = MakeCode(company, codes);
            var hasRegion = country.Country is "USA" or "Canada" or "Brazil" or "Mexico" or "Australia";
            customers.Add(new Customer
            {
                Code = code,
                CompanyName = company,
                ContactName = $"{first} {last}",
                ContactTitle = Pick(ContactTitles),
                Email = $"{Slug(first)}.{Slug(last)}@{Slug(company)}.example.com",
                Address = f.Address.StreetAddress(),
                City = city,
                Region = hasRegion ? f.Address.StateAbbr() : null,
                PostalCode = f.Address.ZipCode(),
                Country = country.Country,
                Phone = f.Phone.PhoneNumber(),
                Fax = _rng.Next(3) == 0 ? f.Phone.PhoneNumber() : null,
                CreatedAt = _today.AddYears(-4).AddDays(_rng.Next(0, 365 * 4)),
            });
        }

        db.Customers.AddRange(customers);
        return customers;
    }

    private void SeedOrders(List<Customer> customers, List<Employee> employees, List<Shipper> shippers, List<Product> products)
    {
        var salesReps = employees.Take(9).ToList();
        var repWeights = new double[] { 1.3, 0.5, 1.4, 1.6, 0.8, 1.0, 1.0, 0.9, 0.7 };

        // Pareto-ish customer weighting so some customers are clearly "key accounts".
        var custWeights = customers.Select((_, i) => 1.0 / Math.Pow(i % 97 + 1, 0.65) * (0.5 + _rng.NextDouble())).ToArray();
        // Each customer has favourite categories, which makes the per-customer dashboard meaningful.
        var catIds = products.Select(p => p.CategoryId).Distinct().ToArray();
        var custFavs = customers.ToDictionary(c => c.Id, _ => catIds.OrderBy(_ => _rng.Next()).Take(2).ToHashSet());
        var productWeights = products.Select(_ => 0.3 + _rng.NextDouble() * 1.7).ToArray();
        var discounts = new[] { 0, 0, 0, 0, 0, 0.05, 0.1, 0.15, 0.2, 0.25 };

        var start = new DateTime(_today.Year - 3, 1, 1);
        var totalDays = (_today - start).Days;

        // Orders per day weight: growth over time + Q4 seasonality + fewer weekend orders.
        var dayWeights = Enumerable.Range(0, totalDays + 1).Select(d =>
        {
            var date = start.AddDays(d);
            var growth = 1 + d / (double)totalDays * 0.9;
            var season = date.Month switch { 11 or 12 => 1.45, 10 => 1.2, 1 or 2 => 0.8, 7 or 8 => 0.9, _ => 1.0 };
            var weekday = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? 0.35 : 1.0;
            return growth * season * weekday;
        }).ToArray();

        var orderDates = Enumerable.Range(0, options.OrderCount)
            .Select(_ => start.AddDays(WeightedIndex(dayWeights)).AddHours(_rng.Next(8, 18)).AddMinutes(_rng.Next(60)))
            .OrderBy(d => d).ToList();

        var discontinuedCutoff = _today.AddMonths(-18);
        var orderId = 10248;
        var orders = new List<Order>(options.OrderCount);

        foreach (var orderDate in orderDates)
        {
            var customer = customers[WeightedIndex(custWeights)];
            if (customer.CreatedAt > orderDate) customer.CreatedAt = orderDate.Date.AddDays(-_rng.Next(5, 120));

            var age = (_today - orderDate).TotalDays;
            var shippedDays = _rng.Next(1, 21);
            var order = new Order
            {
                Id = orderId++,
                CustomerId = customer.Id,
                EmployeeId = salesReps[WeightedIndex(repWeights)].Id,
                OrderDate = orderDate,
                RequiredDate = orderDate.Date.AddDays(_rng.Next(10) == 0 ? 14 : _rng.Next(10) == 0 ? 42 : 28),
                ShippedDate = age > shippedDays && _rng.Next(150) != 0 ? orderDate.Date.AddDays(shippedDays) : null,
                ShipViaId = shippers[_rng.Next(10) < 9 ? _rng.Next(3) : 3].Id,
                ShipName = customer.CompanyName,
                ShipAddress = customer.Address,
                ShipCity = customer.City,
                ShipRegion = customer.Region,
                ShipPostalCode = customer.PostalCode,
                ShipCountry = customer.Country,
            };

            var favs = custFavs[customer.Id];
            var lineCount = _rng.Next(10) switch { < 2 => 1, < 5 => 2, < 8 => 3, < 9 => 4, _ => 5 + _rng.Next(3) };
            var used = new HashSet<int>();
            var priceFactor = 1 + (orderDate.Year - start.Year) * 0.035;
            decimal total = 0;

            for (var attempt = 0; order.Details.Count < lineCount && attempt < 40; attempt++)
            {
                var p = products[WeightedIndex(productWeights)];
                if (!favs.Contains(p.CategoryId) && _rng.Next(10) < 6) continue;
                if (p.Discontinued && orderDate > discontinuedCutoff) continue;
                if (!used.Add(p.Id)) continue;

                var qty = _rng.Next(10) < 7 ? _rng.Next(1, 31) : _rng.Next(30, 121);
                if (p.UnitPrice > 75) qty = Math.Max(1, qty / 5);
                var price = Math.Round(p.UnitPrice * (decimal)priceFactor, 2);
                var discount = discounts[_rng.Next(discounts.Length)];
                order.Details.Add(new OrderDetail { ProductId = p.Id, UnitPrice = price, Quantity = qty, Discount = discount });
                total += price * qty * (decimal)(1 - discount);
            }

            order.Freight = Math.Round(total * (decimal)(0.01 + _rng.NextDouble() * 0.04) + _rng.Next(2, 25), 2);
            orders.Add(order);
        }

        db.Orders.AddRange(orders);
        db.ChangeTracker.DetectChanges();
    }

    // ---------------------------------------------------------------- Projects / Gantt

    private List<Resource> SeedResources(List<Employee> employees)
    {
        var f = new Faker("en");
        var roles = new (string Role, decimal Rate, int Count)[]
        {
            ("Business Analyst", 85, 3), ("Solution Architect", 140, 2), ("Software Developer", 110, 7),
            ("UX Designer", 95, 3), ("QA Engineer", 80, 4), ("DevOps Engineer", 120, 2),
            ("Data Engineer", 115, 2), ("Warehouse Lead", 65, 2), ("Trainer", 70, 2), ("Procurement Specialist", 75, 1),
        };

        var resources = employees
            .Where(e => e.Title is "Project Manager" or "IT Director" or "Marketing Manager" or "Marketing Specialist" or "Sales Manager" or "Inside Sales Coordinator")
            .Select(e => new Resource
            {
                Name = $"{e.FirstName} {e.LastName}",
                Email = e.Email,
                Role = e.Title,
                HourlyRate = e.Title switch { "Project Manager" => 125, "IT Director" => 160, _ => 90 },
                EmployeeId = e.Id,
            }).ToList();

        foreach (var (role, rate, count) in roles)
        {
            for (var i = 0; i < count; i++)
            {
                var first = f.Name.FirstName();
                var last = f.Name.LastName();
                resources.Add(new Resource
                {
                    Name = $"{first} {last}",
                    Email = $"{Slug(first)}.{Slug(last)}@contractors.example.com",
                    Role = role,
                    HourlyRate = rate + _rng.Next(-10, 15),
                });
            }
        }

        db.Resources.AddRange(resources);
        return resources;
    }

    private sealed record PhaseTemplate(string Name, (string Task, int Days, string Role)[] Tasks);

    private static readonly (string Name, string Description, PhaseTemplate[] Phases)[] ProjectTemplates =
    [
        ("Online Ordering Portal", "Self-service B2B ordering site for wholesale customers.",
        [
            new("Discovery", [("Stakeholder interviews", 3, "Business Analyst"), ("Current-state process mapping", 4, "Business Analyst"), ("Requirements sign-off", 0, "Project Manager")]),
            new("Design", [("Information architecture", 3, "UX Designer"), ("Wireframes", 5, "UX Designer"), ("Visual design", 6, "UX Designer"), ("Design review", 0, "Project Manager")]),
            new("Build", [("API & data model", 8, "Software Developer"), ("Catalog & search", 6, "Software Developer"), ("Cart & checkout", 8, "Software Developer"), ("ERP integration", 10, "Solution Architect"), ("CI/CD pipeline", 3, "DevOps Engineer")]),
            new("Test", [("Test plan", 2, "QA Engineer"), ("Functional testing", 6, "QA Engineer"), ("Performance testing", 3, "QA Engineer"), ("User acceptance testing", 5, "Business Analyst")]),
            new("Launch", [("Customer training", 3, "Trainer"), ("Go-live", 0, "Project Manager"), ("Hypercare", 10, "Software Developer")]),
        ]),
        ("Warehouse Management System Upgrade", "Replace legacy WMS with cloud WMS and handheld scanners.",
        [
            new("Planning", [("Vendor selection", 8, "Procurement Specialist"), ("Contract negotiation", 5, "Procurement Specialist"), ("Kickoff", 0, "Project Manager")]),
            new("Configuration", [("Location & bin setup", 6, "Warehouse Lead"), ("Pick/pack rules", 5, "Business Analyst"), ("Label & barcode templates", 3, "Software Developer"), ("Scanner provisioning", 4, "DevOps Engineer")]),
            new("Integration", [("Order feed integration", 7, "Software Developer"), ("Inventory sync", 6, "Data Engineer"), ("Shipping carrier APIs", 5, "Software Developer")]),
            new("Cutover", [("Inventory count", 2, "Warehouse Lead"), ("Data migration", 3, "Data Engineer"), ("Floor staff training", 4, "Trainer"), ("Cutover weekend", 0, "Project Manager")]),
        ]),
        ("Sales Analytics Data Warehouse", "Central reporting warehouse for sales, product and marketing KPIs.",
        [
            new("Analysis", [("KPI catalog", 4, "Business Analyst"), ("Source system inventory", 3, "Data Engineer"), ("Architecture blueprint", 5, "Solution Architect")]),
            new("Ingestion", [("Orders pipeline", 6, "Data Engineer"), ("Products & inventory pipeline", 5, "Data Engineer"), ("Marketing pipeline", 5, "Data Engineer"), ("Data quality rules", 4, "QA Engineer")]),
            new("Modeling", [("Star schema design", 5, "Solution Architect"), ("Semantic model", 6, "Data Engineer"), ("Row-level security", 3, "DevOps Engineer")]),
            new("Reporting", [("Executive dashboard", 6, "UX Designer"), ("Sales by region report", 4, "Software Developer"), ("Product performance report", 4, "Software Developer"), ("Reports published", 0, "Project Manager")]),
        ]),
        ("Holiday Catalog Launch", "Seasonal product catalog, campaign and fulfilment readiness.",
        [
            new("Merchandising", [("Assortment planning", 6, "Business Analyst"), ("Supplier commitments", 5, "Procurement Specialist"), ("Pricing approval", 0, "Project Manager")]),
            new("Creative", [("Photography", 5, "UX Designer"), ("Copywriting", 4, "Marketing Specialist"), ("Catalog layout", 6, "UX Designer"), ("Print proof", 2, "Marketing Manager")]),
            new("Fulfilment", [("Inventory build-up", 12, "Warehouse Lead"), ("Packaging design", 4, "UX Designer"), ("Carrier capacity booking", 2, "Procurement Specialist")]),
            new("Go to market", [("Email & social campaign", 5, "Marketing Specialist"), ("Sales team briefing", 1, "Sales Manager"), ("Catalog live", 0, "Marketing Manager")]),
        ]),
        ("CRM Rollout", "Roll out CRM to sales, marketing and customer service teams.",
        [
            new("Setup", [("Org & security model", 4, "Solution Architect"), ("Pipeline stages", 2, "Business Analyst"), ("Lead scoring rules", 3, "Marketing Specialist")]),
            new("Migration", [("Customer data cleanup", 6, "Data Engineer"), ("Contact import", 3, "Data Engineer"), ("Historical orders import", 4, "Data Engineer")]),
            new("Enablement", [("Admin training", 2, "Trainer"), ("Sales rep training", 4, "Trainer"), ("Marketing training", 2, "Trainer"), ("Rollout complete", 0, "Project Manager")]),
        ]),
        ("Supplier Onboarding Portal", "Portal for suppliers to manage catalogs, invoices and compliance documents.",
        [
            new("Requirements", [("Supplier survey", 5, "Business Analyst"), ("Compliance requirements", 4, "Procurement Specialist"), ("Scope approved", 0, "Project Manager")]),
            new("Build", [("Supplier registration", 6, "Software Developer"), ("Catalog upload", 7, "Software Developer"), ("Invoice submission", 6, "Software Developer"), ("Document vault", 4, "Software Developer")]),
            new("Quality", [("Security review", 3, "DevOps Engineer"), ("Regression testing", 5, "QA Engineer"), ("Pilot with 5 suppliers", 8, "Procurement Specialist")]),
            new("Release", [("Supplier webinars", 3, "Trainer"), ("General availability", 0, "Project Manager")]),
        ]),
    ];

    private void SeedProjects(List<Resource> resources, List<Customer> customers)
    {
        var managers = resources.Where(r => r.Role == "Project Manager").ToList();
        var byRole = resources.GroupBy(r => r.Role ?? "").ToDictionary(g => g.Key, g => g.ToList());

        // Spread projects so some are finished, some in flight and some planned.
        var plans = new (int TemplateIndex, string? Suffix, int StartOffsetDays, ProjectStatus? Forced)[]
        {
            (2, "Phase 1", -420, ProjectStatus.Completed), (4, null, -300, ProjectStatus.Completed),
            (3, $"{_today.Year - 1}", -380, ProjectStatus.Completed), (0, null, -75, null), (1, null, -40, null),
            (3, $"{_today.Year}", -110, null), (5, null, -20, null), (2, "Phase 2", 25, null),
            (0, "Mobile App", 60, ProjectStatus.Planned), (1, "Distribution Center East", -10, ProjectStatus.OnHold),
        };

        foreach (var (ti, suffix, offset, forced) in plans)
        {
            var template = ProjectTemplates[ti];
            var start = NextWeekday(_today.AddDays(offset));
            var manager = managers[_rng.Next(managers.Count)];
            var project = new Project
            {
                Name = suffix is null ? template.Name : $"{template.Name} - {suffix}",
                Description = template.Description,
                StartDate = start,
                ManagerId = manager.Id,
                CustomerId = _rng.Next(3) == 0 ? customers[_rng.Next(30)].Id : null,
                Budget = _rng.Next(8, 60) * 5000,
                CreatedAt = start.AddDays(-_rng.Next(10, 40)),
                UpdatedAt = _today,
            };
            db.Projects.Add(project);

            var cursor = start;
            ProjectTask? previousPhaseLast = null;
            var phaseOrder = 0;
            var dependencies = new List<TaskDependency>();

            foreach (var phase in template.Phases)
            {
                var summary = new ProjectTask { Project = project, Name = phase.Name, SortOrder = phaseOrder++, IsSummary = true, Start = cursor, Finish = cursor };
                db.ProjectTasks.Add(summary);

                ProjectTask? prev = null;
                var taskOrder = 0;
                var phaseFinish = cursor;
                foreach (var (taskName, days, role) in phase.Tasks)
                {
                    // About a third of non-milestone tasks run in parallel with the previous one.
                    var parallel = prev is not null && days > 0 && !prev.IsMilestone && _rng.Next(3) == 0;
                    var taskStart = prev is null ? cursor : parallel ? prev.Start : prev.Finish;
                    var duration = days == 0 ? 0 : Math.Max(1, days + _rng.Next(-1, 3));
                    var task = new ProjectTask
                    {
                        Project = project,
                        Parent = summary,
                        Name = taskName,
                        SortOrder = taskOrder++,
                        Start = taskStart,
                        Finish = taskStart.AddDays(duration),
                        DurationDays = duration,
                        IsMilestone = days == 0,
                        Notes = days == 0 ? $"Milestone: {taskName}." : null,
                    };
                    db.ProjectTasks.Add(task);

                    var candidates = byRole.GetValueOrDefault(role) ?? byRole["Software Developer"];
                    var assignees = candidates.OrderBy(_ => _rng.Next()).Take(days >= 6 && candidates.Count > 1 && _rng.Next(2) == 0 ? 2 : 1);
                    foreach (var r in assignees)
                        db.TaskAssignments.Add(new TaskAssignment { Task = task, Resource = r, Units = _rng.Next(4) == 0 ? 50 : 100 });

                    var pred = prev ?? previousPhaseLast;
                    if (pred is not null)
                    {
                        dependencies.Add(new TaskDependency
                        {
                            Predecessor = pred,
                            Successor = task,
                            Type = parallel ? DependencyType.StartToStart : DependencyType.FinishToStart,
                        });
                    }

                    if (task.Finish > phaseFinish) phaseFinish = task.Finish;
                    prev = task;
                }

                summary.Finish = phaseFinish;
                summary.DurationDays = (phaseFinish - summary.Start).TotalDays;
                previousPhaseLast = prev;
                cursor = phaseFinish;
            }

            project.FinishDate = cursor;
            project.Status = forced ?? (cursor < _today ? ProjectStatus.Completed : start > _today ? ProjectStatus.Planned : ProjectStatus.Active);
            db.TaskDependencies.AddRange(dependencies);

            foreach (var t in db.ChangeTracker.Entries<ProjectTask>().Select(e => e.Entity).Where(t => t.Project == project && !t.IsSummary))
            {
                t.PercentComplete = project.Status switch
                {
                    ProjectStatus.Completed => 100,
                    ProjectStatus.Planned => 0,
                    _ when t.Finish <= _today => 100,
                    _ when t.Start >= _today => 0,
                    _ when t.IsMilestone => 0,
                    _ => (int)Math.Round((_today - t.Start).TotalDays / Math.Max(1, t.DurationDays) * 100 / 5) * 5,
                };
                if (project.Status == ProjectStatus.OnHold && t.Start > _today.AddDays(-5)) t.PercentComplete = 0;
            }
        }
        db.ChangeTracker.DetectChanges();
    }

    // ---------------------------------------------------------------- Marketing

    private void SeedMarketing(List<Category> categories, List<Employee> employees, List<Customer> customers)
    {
        var marketers = employees.Where(e => e.Title!.StartsWith("Marketing")).ToList();
        var reps = employees.Where(e => e.Title is "Sales Representative" or "Inside Sales Coordinator" or "Sales Manager").ToList();
        var seasons = new[] { ("Winter", 1), ("Spring", 3), ("Summer", 6), ("Back to Business", 9), ("Holiday", 11) };
        var themes = new Dictionary<CampaignChannel, string[]>
        {
            [CampaignChannel.Email] = ["Newsletter", "Email Blast", "Win-back Series", "Loyalty Offer"],
            [CampaignChannel.Social] = ["Social Push", "Influencer Collab", "Recipe Reels"],
            [CampaignChannel.Search] = ["Paid Search", "Shopping Ads"],
            [CampaignChannel.Display] = ["Retargeting", "Display Network"],
            [CampaignChannel.Events] = ["Trade Show", "Tasting Tour", "Food Expo"],
            [CampaignChannel.Webinar] = ["Webinar", "Chef Masterclass"],
            [CampaignChannel.DirectMail] = ["Catalog Mailer", "Sample Box"],
            [CampaignChannel.Partner] = ["Distributor Co-op", "Partner Promo"],
        };
        var channels = Enum.GetValues<CampaignChannel>();
        var f = new Faker("en");

        var campaigns = new List<Campaign>();
        var firstYear = _today.Year - 3;
        for (var i = 0; i < options.CampaignCount; i++)
        {
            var (season, month) = seasons[i % seasons.Length];
            var year = firstYear + (int)(i / (double)options.CampaignCount * 4);
            var start = new DateTime(year, month, 1).AddDays(_rng.Next(0, 25));
            if (start > _today.AddMonths(3)) start = _today.AddDays(_rng.Next(5, 80));
            var end = start.AddDays(_rng.Next(14, 75));
            // Keep a handful of campaigns in flight "today" so the Marketing screens always have live data.
            var live = i >= options.CampaignCount - 5;
            if (live)
            {
                start = _today.AddDays(-_rng.Next(5, 40));
                end = _today.AddDays(_rng.Next(10, 60));
                (season, _) = seasons.Last(s => s.Item2 <= start.Month);
                year = start.Year;
            }
            var channel = channels[_rng.Next(channels.Length)];
            var category = _rng.Next(4) == 0 ? null : categories[_rng.Next(categories.Count)];
            var budget = (decimal)(_rng.Next(5, 80) * 1000);
            var status = end < _today ? CampaignStatus.Completed
                : start > _today ? (_rng.Next(3) == 0 ? CampaignStatus.Draft : CampaignStatus.Scheduled)
                : (!live && _rng.Next(6) == 0 ? CampaignStatus.Paused : CampaignStatus.Active);
            var progress = status switch
            {
                CampaignStatus.Completed => 1.0,
                CampaignStatus.Draft or CampaignStatus.Scheduled => 0.0,
                _ => Math.Clamp((_today - start).TotalDays / (end - start).TotalDays, 0, 1),
            };
            var impressions = (int)(budget * (decimal)(20 + _rng.Next(80)) * (decimal)progress);
            campaigns.Add(new Campaign
            {
                Name = $"{season} {year} {category?.Name.Split('/')[0] ?? "Brand"} {Pick(themes[channel])}",
                Description = f.Lorem.Sentence(14),
                Channel = channel,
                Status = status,
                StartDate = start,
                EndDate = end,
                Budget = budget,
                ActualCost = Math.Round(budget * (decimal)(progress * (0.8 + _rng.NextDouble() * 0.35)), 2),
                ExpectedRevenue = budget * (decimal)(2 + _rng.NextDouble() * 6),
                Impressions = impressions,
                Clicks = (int)(impressions * (0.005 + _rng.NextDouble() * 0.03)),
                TargetCategoryId = category?.Id,
                OwnerId = marketers[_rng.Next(marketers.Count)].Id,
            });
        }
        db.Campaigns.AddRange(campaigns);

        var titles = new[] { "Purchasing Manager", "Owner", "Buyer", "Category Manager", "Operations Director", "Head Chef", "Store Manager", "Procurement Lead" };
        var startedCampaigns = campaigns.Where(c => c.StartDate <= _today).ToList();
        var subjects = new Dictionary<ActivityType, string[]>
        {
            [ActivityType.Call] = ["Intro call", "Follow-up call", "Pricing discussion", "Check-in call"],
            [ActivityType.Email] = ["Sent product catalog", "Sent price list", "Follow-up email", "Shared case study"],
            [ActivityType.Meeting] = ["On-site visit", "Lunch meeting", "Quarterly review"],
            [ActivityType.Demo] = ["Product tasting", "Ordering portal demo"],
            [ActivityType.Note] = ["Budget confirmed", "Decision maker identified", "Competitor in place"],
        };
        var activityTypes = Enum.GetValues<ActivityType>();
        var leads = new List<Lead>();

        for (var i = 0; i < options.LeadCount; i++)
        {
            var (country, locale, _) = Countries[_rng.Next(Countries.Length)];
            var lf = new Faker(locale);
            var campaign = _rng.Next(10) < 6 && startedCampaigns.Count > 0 ? startedCampaigns[_rng.Next(startedCampaigns.Count)] : null;
            var created = campaign is not null
                ? Min(campaign.StartDate.AddDays(_rng.Next(0, Math.Max(1, (campaign.EndDate - campaign.StartDate).Days + 20))), _today)
                : _today.AddDays(-_rng.Next(0, 365 * 3));
            var ageDays = (_today - created).TotalDays;

            var status = ageDays switch
            {
                < 7 => _rng.Next(10) < 7 ? LeadStatus.New : LeadStatus.Contacted,
                < 30 => (LeadStatus)_rng.Next(0, 4),
                _ => _rng.Next(100) switch { < 4 => LeadStatus.New, < 14 => LeadStatus.Contacted, < 26 => LeadStatus.Qualified, < 34 => LeadStatus.Proposal, < 56 => LeadStatus.Won, _ => LeadStatus.Lost },
            };
            var first = lf.Name.FirstName();
            var last = lf.Name.LastName();
            var company = _rng.Next(2) == 0 ? lf.Company.CompanyName() : $"{last} {Pick(CompanySuffixes)}";
            var lead = new Lead
            {
                FirstName = first,
                LastName = last,
                Title = Pick(titles),
                Company = company,
                Email = $"{Slug(first)}.{Slug(last)}@{Slug(company)}.example.com",
                Phone = lf.Phone.PhoneNumber(),
                City = lf.Address.City(),
                Country = country,
                Source = campaign is not null ? LeadSource.Campaign : (LeadSource)_rng.Next(1, 7),
                Status = status,
                Score = status switch
                {
                    LeadStatus.Won => _rng.Next(70, 101),
                    LeadStatus.Lost => _rng.Next(5, 60),
                    LeadStatus.Proposal => _rng.Next(60, 95),
                    LeadStatus.Qualified => _rng.Next(45, 85),
                    _ => _rng.Next(5, 60),
                },
                EstimatedValue = _rng.Next(5, 200) * 250,
                Campaign = campaign,
                OwnerId = reps[_rng.Next(reps.Count)].Id,
                CreatedAt = created.AddHours(_rng.Next(8, 18)),
            };

            if (status == LeadStatus.Won)
            {
                lead.ConvertedAt = Min(created.AddDays(_rng.Next(10, 90)), _today);
                lead.ConvertedCustomerId = customers[_rng.Next(customers.Count)].Id;
            }

            var activityCount = status switch { LeadStatus.New => _rng.Next(0, 2), LeadStatus.Contacted => _rng.Next(1, 3), _ => _rng.Next(2, 7) };
            var when = lead.CreatedAt;
            for (var a = 0; a < activityCount; a++)
            {
                when = when.AddDays(_rng.Next(1, 12)).AddHours(_rng.Next(-3, 4));
                if (when > _today.AddHours(18)) break;
                var type = activityTypes[_rng.Next(activityTypes.Length)];
                lead.Activities.Add(new LeadActivity
                {
                    Type = type,
                    Subject = Pick(subjects[type]),
                    Notes = _rng.Next(2) == 0 ? f.Lorem.Sentence(10) : null,
                    OccurredAt = when,
                    EmployeeId = lead.OwnerId,
                });
            }
            leads.Add(lead);
        }

        db.Leads.AddRange(leads);
        db.ChangeTracker.DetectChanges();
    }

    // ---------------------------------------------------------------- helpers

    private T Pick<T>(IReadOnlyList<T> items) => items[_rng.Next(items.Count)];

    private int WeightedIndex(double[] weights)
    {
        var total = 0d;
        foreach (var w in weights) total += w;
        var roll = _rng.NextDouble() * total;
        for (var i = 0; i < weights.Length; i++)
        {
            roll -= weights[i];
            if (roll <= 0) return i;
        }
        return weights.Length - 1;
    }

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

    private static DateTime NextWeekday(DateTime d) =>
        (d.DayOfWeek switch { DayOfWeek.Saturday => d.AddDays(2), DayOfWeek.Sunday => d.AddDays(1), _ => d }).Date;

    private static string Article(string word) => "AEIOU".Contains(char.ToUpperInvariant(word[0])) ? "an" : "a";

    private static string MakeCode(string company, HashSet<string> existing)
    {
        var letters = new string(RemoveDiacritics(company).ToUpperInvariant().Where(char.IsAsciiLetterUpper).ToArray());
        letters = (letters + "XXXXX")[..5];
        var code = letters;
        for (var i = 1; !existing.Add(code); i++) code = letters[..(5 - i.ToString().Length)] + i;
        return code;
    }

    public static string Slug(string s) =>
        new string(RemoveDiacritics(s).ToLowerInvariant().Where(char.IsAsciiLetterOrDigit).ToArray()) is { Length: > 0 } slug ? slug : "contact";

    private static string RemoveDiacritics(string s)
    {
        var normalized = s.Normalize(System.Text.NormalizationForm.FormD);
        return new string(normalized.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray())
            .Replace("ß", "ss").Replace("ø", "o").Replace("æ", "ae").Replace("ł", "l");
    }
}
