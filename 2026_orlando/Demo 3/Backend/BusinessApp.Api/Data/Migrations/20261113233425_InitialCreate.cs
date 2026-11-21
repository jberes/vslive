using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessApp.Api.Data.Migrations;

/// <inheritdoc />
public partial class _20261113233425_InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Categories",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                Description = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Categories", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Customers",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Code = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                CompanyName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                ContactName = table.Column<string>(type: "TEXT", nullable: true),
                ContactTitle = table.Column<string>(type: "TEXT", nullable: true),
                Email = table.Column<string>(type: "TEXT", nullable: true),
                Address = table.Column<string>(type: "TEXT", nullable: true),
                City = table.Column<string>(type: "TEXT", nullable: true),
                Region = table.Column<string>(type: "TEXT", nullable: true),
                PostalCode = table.Column<string>(type: "TEXT", nullable: true),
                Country = table.Column<string>(type: "TEXT", nullable: true),
                Phone = table.Column<string>(type: "TEXT", nullable: true),
                Fax = table.Column<string>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Customers", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Employees",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                FirstName = table.Column<string>(type: "TEXT", nullable: false),
                LastName = table.Column<string>(type: "TEXT", nullable: false),
                Title = table.Column<string>(type: "TEXT", nullable: true),
                TitleOfCourtesy = table.Column<string>(type: "TEXT", nullable: true),
                BirthDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                HireDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                Email = table.Column<string>(type: "TEXT", nullable: true),
                Address = table.Column<string>(type: "TEXT", nullable: true),
                City = table.Column<string>(type: "TEXT", nullable: true),
                Region = table.Column<string>(type: "TEXT", nullable: true),
                PostalCode = table.Column<string>(type: "TEXT", nullable: true),
                Country = table.Column<string>(type: "TEXT", nullable: true),
                HomePhone = table.Column<string>(type: "TEXT", nullable: true),
                Extension = table.Column<string>(type: "TEXT", nullable: true),
                Notes = table.Column<string>(type: "TEXT", nullable: true),
                ReportsToId = table.Column<int>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Employees", x => x.Id);
                table.ForeignKey(
                    name: "FK_Employees_Employees_ReportsToId",
                    column: x => x.ReportsToId,
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "Shippers",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                CompanyName = table.Column<string>(type: "TEXT", nullable: false),
                Phone = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Shippers", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Suppliers",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                CompanyName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                ContactName = table.Column<string>(type: "TEXT", nullable: true),
                ContactTitle = table.Column<string>(type: "TEXT", nullable: true),
                Address = table.Column<string>(type: "TEXT", nullable: true),
                City = table.Column<string>(type: "TEXT", nullable: true),
                Region = table.Column<string>(type: "TEXT", nullable: true),
                PostalCode = table.Column<string>(type: "TEXT", nullable: true),
                Country = table.Column<string>(type: "TEXT", nullable: true),
                Phone = table.Column<string>(type: "TEXT", nullable: true),
                Fax = table.Column<string>(type: "TEXT", nullable: true),
                HomePage = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Suppliers", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Campaigns",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Name = table.Column<string>(type: "TEXT", nullable: false),
                Description = table.Column<string>(type: "TEXT", nullable: true),
                Channel = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                EndDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                Budget = table.Column<double>(type: "REAL", nullable: false),
                ActualCost = table.Column<double>(type: "REAL", nullable: false),
                ExpectedRevenue = table.Column<double>(type: "REAL", nullable: false),
                Impressions = table.Column<int>(type: "INTEGER", nullable: false),
                Clicks = table.Column<int>(type: "INTEGER", nullable: false),
                TargetCategoryId = table.Column<int>(type: "INTEGER", nullable: true),
                OwnerId = table.Column<int>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Campaigns", x => x.Id);
                table.ForeignKey(
                    name: "FK_Campaigns_Categories_TargetCategoryId",
                    column: x => x.TargetCategoryId,
                    principalTable: "Categories",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_Campaigns_Employees_OwnerId",
                    column: x => x.OwnerId,
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "Resources",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Name = table.Column<string>(type: "TEXT", nullable: false),
                Email = table.Column<string>(type: "TEXT", nullable: true),
                Role = table.Column<string>(type: "TEXT", nullable: true),
                HourlyRate = table.Column<double>(type: "REAL", nullable: false),
                EmployeeId = table.Column<int>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Resources", x => x.Id);
                table.ForeignKey(
                    name: "FK_Resources_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "Orders",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                CustomerId = table.Column<int>(type: "INTEGER", nullable: false),
                EmployeeId = table.Column<int>(type: "INTEGER", nullable: false),
                OrderDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                RequiredDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                ShippedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                ShipViaId = table.Column<int>(type: "INTEGER", nullable: false),
                Freight = table.Column<double>(type: "REAL", nullable: false),
                ShipName = table.Column<string>(type: "TEXT", nullable: true),
                ShipAddress = table.Column<string>(type: "TEXT", nullable: true),
                ShipCity = table.Column<string>(type: "TEXT", nullable: true),
                ShipRegion = table.Column<string>(type: "TEXT", nullable: true),
                ShipPostalCode = table.Column<string>(type: "TEXT", nullable: true),
                ShipCountry = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Orders", x => x.Id);
                table.ForeignKey(
                    name: "FK_Orders_Customers_CustomerId",
                    column: x => x.CustomerId,
                    principalTable: "Customers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Orders_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Orders_Shippers_ShipViaId",
                    column: x => x.ShipViaId,
                    principalTable: "Shippers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Products",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                CategoryId = table.Column<int>(type: "INTEGER", nullable: false),
                SupplierId = table.Column<int>(type: "INTEGER", nullable: false),
                QuantityPerUnit = table.Column<string>(type: "TEXT", nullable: true),
                UnitPrice = table.Column<double>(type: "REAL", nullable: false),
                UnitsInStock = table.Column<int>(type: "INTEGER", nullable: false),
                UnitsOnOrder = table.Column<int>(type: "INTEGER", nullable: false),
                ReorderLevel = table.Column<int>(type: "INTEGER", nullable: false),
                Discontinued = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Products", x => x.Id);
                table.ForeignKey(
                    name: "FK_Products_Categories_CategoryId",
                    column: x => x.CategoryId,
                    principalTable: "Categories",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Products_Suppliers_SupplierId",
                    column: x => x.SupplierId,
                    principalTable: "Suppliers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Leads",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                FirstName = table.Column<string>(type: "TEXT", nullable: false),
                LastName = table.Column<string>(type: "TEXT", nullable: false),
                Title = table.Column<string>(type: "TEXT", nullable: true),
                Company = table.Column<string>(type: "TEXT", nullable: false),
                Email = table.Column<string>(type: "TEXT", nullable: true),
                Phone = table.Column<string>(type: "TEXT", nullable: true),
                City = table.Column<string>(type: "TEXT", nullable: true),
                Country = table.Column<string>(type: "TEXT", nullable: true),
                Source = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                Score = table.Column<int>(type: "INTEGER", nullable: false),
                EstimatedValue = table.Column<double>(type: "REAL", nullable: false),
                CampaignId = table.Column<int>(type: "INTEGER", nullable: true),
                OwnerId = table.Column<int>(type: "INTEGER", nullable: true),
                ConvertedCustomerId = table.Column<int>(type: "INTEGER", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                ConvertedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Leads", x => x.Id);
                table.ForeignKey(
                    name: "FK_Leads_Campaigns_CampaignId",
                    column: x => x.CampaignId,
                    principalTable: "Campaigns",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_Leads_Customers_ConvertedCustomerId",
                    column: x => x.ConvertedCustomerId,
                    principalTable: "Customers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_Leads_Employees_OwnerId",
                    column: x => x.OwnerId,
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "Projects",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Name = table.Column<string>(type: "TEXT", nullable: false),
                Description = table.Column<string>(type: "TEXT", nullable: true),
                Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                FinishDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                ManagerId = table.Column<int>(type: "INTEGER", nullable: true),
                CustomerId = table.Column<int>(type: "INTEGER", nullable: true),
                Budget = table.Column<double>(type: "REAL", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Projects", x => x.Id);
                table.ForeignKey(
                    name: "FK_Projects_Customers_CustomerId",
                    column: x => x.CustomerId,
                    principalTable: "Customers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_Projects_Resources_ManagerId",
                    column: x => x.ManagerId,
                    principalTable: "Resources",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "OrderDetails",
            columns: table => new
            {
                OrderId = table.Column<int>(type: "INTEGER", nullable: false),
                ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                UnitPrice = table.Column<double>(type: "REAL", nullable: false),
                Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                Discount = table.Column<double>(type: "REAL", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrderDetails", x => new { x.OrderId, x.ProductId });
                table.ForeignKey(
                    name: "FK_OrderDetails_Orders_OrderId",
                    column: x => x.OrderId,
                    principalTable: "Orders",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_OrderDetails_Products_ProductId",
                    column: x => x.ProductId,
                    principalTable: "Products",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "LeadActivities",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                LeadId = table.Column<int>(type: "INTEGER", nullable: false),
                Type = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                Subject = table.Column<string>(type: "TEXT", nullable: false),
                Notes = table.Column<string>(type: "TEXT", nullable: true),
                OccurredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                EmployeeId = table.Column<int>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LeadActivities", x => x.Id);
                table.ForeignKey(
                    name: "FK_LeadActivities_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_LeadActivities_Leads_LeadId",
                    column: x => x.LeadId,
                    principalTable: "Leads",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ProjectTasks",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                ParentId = table.Column<int>(type: "INTEGER", nullable: true),
                SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                Name = table.Column<string>(type: "TEXT", nullable: false),
                Start = table.Column<DateTime>(type: "TEXT", nullable: false),
                Finish = table.Column<DateTime>(type: "TEXT", nullable: false),
                DurationDays = table.Column<double>(type: "REAL", nullable: false),
                PercentComplete = table.Column<int>(type: "INTEGER", nullable: false),
                IsMilestone = table.Column<bool>(type: "INTEGER", nullable: false),
                IsSummary = table.Column<bool>(type: "INTEGER", nullable: false),
                Notes = table.Column<string>(type: "TEXT", nullable: true),
                FontFamily = table.Column<string>(type: "TEXT", nullable: true),
                FontSize = table.Column<double>(type: "REAL", nullable: true),
                IsBold = table.Column<bool>(type: "INTEGER", nullable: false),
                IsItalic = table.Column<bool>(type: "INTEGER", nullable: false),
                IsUnderline = table.Column<bool>(type: "INTEGER", nullable: false),
                ForegroundColor = table.Column<string>(type: "TEXT", nullable: true),
                BackgroundColor = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectTasks", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProjectTasks_ProjectTasks_ParentId",
                    column: x => x.ParentId,
                    principalTable: "ProjectTasks",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ProjectTasks_Projects_ProjectId",
                    column: x => x.ProjectId,
                    principalTable: "Projects",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "TaskAssignments",
            columns: table => new
            {
                TaskId = table.Column<int>(type: "INTEGER", nullable: false),
                ResourceId = table.Column<int>(type: "INTEGER", nullable: false),
                Units = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TaskAssignments", x => new { x.TaskId, x.ResourceId });
                table.ForeignKey(
                    name: "FK_TaskAssignments_ProjectTasks_TaskId",
                    column: x => x.TaskId,
                    principalTable: "ProjectTasks",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_TaskAssignments_Resources_ResourceId",
                    column: x => x.ResourceId,
                    principalTable: "Resources",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "TaskDependencies",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                PredecessorId = table.Column<int>(type: "INTEGER", nullable: false),
                SuccessorId = table.Column<int>(type: "INTEGER", nullable: false),
                Type = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                LagDays = table.Column<double>(type: "REAL", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TaskDependencies", x => x.Id);
                table.ForeignKey(
                    name: "FK_TaskDependencies_ProjectTasks_PredecessorId",
                    column: x => x.PredecessorId,
                    principalTable: "ProjectTasks",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_TaskDependencies_ProjectTasks_SuccessorId",
                    column: x => x.SuccessorId,
                    principalTable: "ProjectTasks",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Campaigns_OwnerId",
            table: "Campaigns",
            column: "OwnerId");

        migrationBuilder.CreateIndex(
            name: "IX_Campaigns_TargetCategoryId",
            table: "Campaigns",
            column: "TargetCategoryId");

        migrationBuilder.CreateIndex(
            name: "IX_Categories_Name",
            table: "Categories",
            column: "Name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Customers_Code",
            table: "Customers",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Customers_CompanyName",
            table: "Customers",
            column: "CompanyName");

        migrationBuilder.CreateIndex(
            name: "IX_Customers_Country",
            table: "Customers",
            column: "Country");

        migrationBuilder.CreateIndex(
            name: "IX_Employees_ReportsToId",
            table: "Employees",
            column: "ReportsToId");

        migrationBuilder.CreateIndex(
            name: "IX_LeadActivities_EmployeeId",
            table: "LeadActivities",
            column: "EmployeeId");

        migrationBuilder.CreateIndex(
            name: "IX_LeadActivities_LeadId",
            table: "LeadActivities",
            column: "LeadId");

        migrationBuilder.CreateIndex(
            name: "IX_Leads_CampaignId",
            table: "Leads",
            column: "CampaignId");

        migrationBuilder.CreateIndex(
            name: "IX_Leads_ConvertedCustomerId",
            table: "Leads",
            column: "ConvertedCustomerId");

        migrationBuilder.CreateIndex(
            name: "IX_Leads_OwnerId",
            table: "Leads",
            column: "OwnerId");

        migrationBuilder.CreateIndex(
            name: "IX_Leads_Status",
            table: "Leads",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_OrderDetails_ProductId",
            table: "OrderDetails",
            column: "ProductId");

        migrationBuilder.CreateIndex(
            name: "IX_Orders_CustomerId",
            table: "Orders",
            column: "CustomerId");

        migrationBuilder.CreateIndex(
            name: "IX_Orders_EmployeeId",
            table: "Orders",
            column: "EmployeeId");

        migrationBuilder.CreateIndex(
            name: "IX_Orders_OrderDate",
            table: "Orders",
            column: "OrderDate");

        migrationBuilder.CreateIndex(
            name: "IX_Orders_ShipViaId",
            table: "Orders",
            column: "ShipViaId");

        migrationBuilder.CreateIndex(
            name: "IX_Products_CategoryId",
            table: "Products",
            column: "CategoryId");

        migrationBuilder.CreateIndex(
            name: "IX_Products_Name",
            table: "Products",
            column: "Name");

        migrationBuilder.CreateIndex(
            name: "IX_Products_SupplierId",
            table: "Products",
            column: "SupplierId");

        migrationBuilder.CreateIndex(
            name: "IX_Projects_CustomerId",
            table: "Projects",
            column: "CustomerId");

        migrationBuilder.CreateIndex(
            name: "IX_Projects_ManagerId",
            table: "Projects",
            column: "ManagerId");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectTasks_ParentId",
            table: "ProjectTasks",
            column: "ParentId");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectTasks_ProjectId_ParentId_SortOrder",
            table: "ProjectTasks",
            columns: new[] { "ProjectId", "ParentId", "SortOrder" });

        migrationBuilder.CreateIndex(
            name: "IX_Resources_EmployeeId",
            table: "Resources",
            column: "EmployeeId");

        migrationBuilder.CreateIndex(
            name: "IX_Suppliers_CompanyName",
            table: "Suppliers",
            column: "CompanyName");

        migrationBuilder.CreateIndex(
            name: "IX_TaskAssignments_ResourceId",
            table: "TaskAssignments",
            column: "ResourceId");

        migrationBuilder.CreateIndex(
            name: "IX_TaskDependencies_PredecessorId_SuccessorId",
            table: "TaskDependencies",
            columns: new[] { "PredecessorId", "SuccessorId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_TaskDependencies_SuccessorId",
            table: "TaskDependencies",
            column: "SuccessorId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "LeadActivities");

        migrationBuilder.DropTable(
            name: "OrderDetails");

        migrationBuilder.DropTable(
            name: "TaskAssignments");

        migrationBuilder.DropTable(
            name: "TaskDependencies");

        migrationBuilder.DropTable(
            name: "Leads");

        migrationBuilder.DropTable(
            name: "Orders");

        migrationBuilder.DropTable(
            name: "Products");

        migrationBuilder.DropTable(
            name: "ProjectTasks");

        migrationBuilder.DropTable(
            name: "Campaigns");

        migrationBuilder.DropTable(
            name: "Shippers");

        migrationBuilder.DropTable(
            name: "Suppliers");

        migrationBuilder.DropTable(
            name: "Projects");

        migrationBuilder.DropTable(
            name: "Categories");

        migrationBuilder.DropTable(
            name: "Customers");

        migrationBuilder.DropTable(
            name: "Resources");

        migrationBuilder.DropTable(
            name: "Employees");
    }
}
