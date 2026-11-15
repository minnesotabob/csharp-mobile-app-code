using System.Windows.Input;

namespace GnazzosWorld.Core.Presentation;

// A deliberately small teaching command; no platform dependency.
public sealed class AsyncCommand(
    Func<Task> action, Func<bool> enabled, Action<Exception> onError) : ICommand
{
    private bool running;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => !running && enabled();
    public void Refresh() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    public async Task ExecuteAsync()
    {
        if (!CanExecute(null)) return;
        running = true;
        Refresh();
        try { await action(); }
        catch (Exception ex) { onError(ex); }
        finally { running = false; Refresh(); }
    }
    public async void Execute(object? parameter) => await ExecuteAsync();
}
