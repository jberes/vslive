namespace TeamCollaboration.Services;

public sealed class WorkspaceState
{
    public event Action? Changed;

    public string? SelectedTaskList { get; set; }

    public List<DashboardItem> Dashboards { get; } =
    [
        new("Artists Analysis", "Jason Anderson", "assets/figma/marketing-dashboard.png", false),
        new("For evaluation", "Martina Roberts", "assets/figma/sales-dashboard.png", true),
        new("Stylescape Results", "Caroline Sanders", "assets/figma/marketing-dashboard.png", false)
    ];

    public List<TaskGroup> TaskGroups { get; } =
    [
        new("Developer Tools", "Release 2020.2", "General",
        [
            new("Download any Ultimate product and take notes on the download process",
                "Product download process experience", "assets/figma/developer-avatar-1.png"),
            new("Report about the downloaded ultimate products to the Marketing Team",
                "Product download process experience", "assets/figma/developer-avatar-2.png")
        ]),
        new("App Builder", "September release - Sprint 1", "App Builder",
        [
            new("Research new sample app", "New sample application", "assets/figma/builder-avatar-1.png"),
            new("Create visual design for new sample app", "New sample application", "assets/figma/builder-avatar-2.png"),
            new("Implementation for new sample app", "New sample application", "assets/figma/builder-avatar-3.png"),
            new("Tweaks in List component", "App Builder UI Components\\", "assets/figma/builder-avatar-4.png"),
            new("QE of new App Builder components", "App Builder UI Components", "assets/figma/builder-avatar-5.png"),
            new("Implementation of Chips component", "App Builder UI Components", "assets/figma/builder-avatar-6.png")
        ])
    ];

    public void NotifyChanged() => Changed?.Invoke();
}

public sealed class DashboardItem(string title, string author, string image, bool isFavorite)
{
    public string Title { get; } = title;
    public string Author { get; } = author;
    public string Image { get; } = image;
    public bool IsFavorite { get; set; } = isFavorite;
}

public sealed class TaskGroup(string name, string subtitle, string listName, List<CollaborationTask> tasks)
{
    public string Name { get; } = name;
    public string Subtitle { get; } = subtitle;
    public string ListName { get; } = listName;
    public List<CollaborationTask> Tasks { get; } = tasks;
}

public sealed class CollaborationTask(string title, string description, string avatar)
{
    public string Title { get; set; } = title;
    public string Description { get; set; } = description;
    public string Avatar { get; } = avatar;
}
