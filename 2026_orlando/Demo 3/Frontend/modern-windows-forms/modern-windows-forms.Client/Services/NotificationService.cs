namespace modern_windows_forms.Client.Services;

public enum ToastKind { Info, Success, Error }

/// <summary>A message for the shell's IgbToast (no action) or IgbSnackbar (with an action such as Undo).</summary>
public sealed record AppNotification(string Message, ToastKind Kind = ToastKind.Info, string? ActionText = null, Func<Task>? Action = null);

/// <summary>Raises toasts/snackbars that the MainLayout renders with IgbToast / IgbSnackbar.</summary>
public sealed class NotificationService
{
    public event Func<AppNotification, Task>? Raised;

    public Task ToastAsync(string message, ToastKind kind = ToastKind.Info) =>
        Raised?.Invoke(new AppNotification(message, kind)) ?? Task.CompletedTask;

    /// <summary>Snackbar with a single action, e.g. "Order 15247 shipped · Undo".</summary>
    public Task SnackbarAsync(string message, string actionText, Func<Task> action) =>
        Raised?.Invoke(new AppNotification(message, ToastKind.Info, actionText, action)) ?? Task.CompletedTask;
}
