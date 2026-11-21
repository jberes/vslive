namespace BusinessApp.Api.Models.Projects;

public enum ProjectStatus { Planned, Active, OnHold, Completed, Cancelled }

public enum DependencyType
{
    FinishToStart,
    StartToStart,
    FinishToFinish,
    StartToFinish
}

public class Project
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public ProjectStatus Status { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? FinishDate { get; set; }
    public int? ManagerId { get; set; }
    public Resource? Manager { get; set; }
    public int? CustomerId { get; set; }
    public Sales.Customer? Customer { get; set; }
    public decimal Budget { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<ProjectTask> Tasks { get; set; } = [];
}

/// <summary>
/// Gantt task. A task with children is a summary task; its Start/Finish/Duration/PercentComplete
/// are rolled up from its children by the server.
/// </summary>
public class ProjectTask
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    public int? ParentId { get; set; }
    public ProjectTask? Parent { get; set; }
    public List<ProjectTask> Children { get; set; } = [];

    /// <summary>Order among siblings (0-based).</summary>
    public int SortOrder { get; set; }
    public required string Name { get; set; }
    public DateTime Start { get; set; }
    public DateTime Finish { get; set; }
    /// <summary>Duration in days (Finish - Start).</summary>
    public double DurationDays { get; set; }
    /// <summary>0..100</summary>
    public int PercentComplete { get; set; }
    public bool IsMilestone { get; set; }
    public bool IsSummary { get; set; }
    public string? Notes { get; set; }

    // "Font" ribbon group formatting.
    public string? FontFamily { get; set; }
    public double? FontSize { get; set; }
    public bool IsBold { get; set; }
    public bool IsItalic { get; set; }
    public bool IsUnderline { get; set; }
    public string? ForegroundColor { get; set; }
    public string? BackgroundColor { get; set; }

    public List<TaskAssignment> Assignments { get; set; } = [];
    public List<TaskDependency> Predecessors { get; set; } = [];
    public List<TaskDependency> Successors { get; set; } = [];
}

public class TaskDependency
{
    public int Id { get; set; }
    public int PredecessorId { get; set; }
    public ProjectTask? Predecessor { get; set; }
    public int SuccessorId { get; set; }
    public ProjectTask? Successor { get; set; }
    public DependencyType Type { get; set; }
    public double LagDays { get; set; }
}

public class Resource
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }
    public decimal HourlyRate { get; set; }
    public int? EmployeeId { get; set; }
    public Sales.Employee? Employee { get; set; }
    public List<TaskAssignment> Assignments { get; set; } = [];
}

public class TaskAssignment
{
    public int TaskId { get; set; }
    public ProjectTask? Task { get; set; }
    public int ResourceId { get; set; }
    public Resource? Resource { get; set; }
    /// <summary>Allocation percent (100 = full time).</summary>
    public int Units { get; set; } = 100;
}
