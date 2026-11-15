using GnazzosWorld.Core.Orders;

namespace GnazzosWorld.Core.Presentation;

// One shared instance preserves edits across navigation within this process.
public sealed class SongRequestViewModel(IRequestDraftStore store) : ObservableObject
{
    private bool restored;
    public SongRequest Draft { get; private set; } = new();
    public int Step { get; private set; }
    public string Status { get; private set; } = "Draft has not been saved.";
    public string Errors { get; private set; } = "";
    public SongRequest? Review { get; private set; }
    public bool IsDirty { get; private set; }

    public void Edit(SongRequest value)
    {
        Draft = value; Review = null; IsDirty = true;
        Status = "Unsaved changes."; Errors = ""; Changed();
    }
    public bool Next()
    {
        var errors = SongRequestRules.Validate(Draft, Step);
        Errors = string.Join("\n", errors.Select(e => e.Message));
        if (errors.Count != 0) { Changed(); return false; }
        if (Step < 2) Step++;
        else Review = Draft.ForReview();
        Changed(); return true;
    }
    public void Back()
    {
        if (Step > 0) Step--;
        Review = null; Errors = ""; Changed();
    }
    public bool Save()
    {
        try
        {
            store.Save(Draft); IsDirty = false;
            Status = "Draft saved on this device."; Changed(); return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Status = "Could not save. Your edits are still open; try again.";
            Changed(); return false;
        }
    }
    public void Restore()
    {
        if (IsDirty || restored) return; // Never replace current edits on a later appearance.
        try
        {
            Draft = store.Load() ?? new(); Step = 0; Review = null; restored = true;
            Status = "Saved draft loaded, if available.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or System.Text.Json.JsonException)
        { Status = "Could not read the draft. The saved file was kept."; }
        Changed();
    }
    public void Discard()
    {
        try
        {
            store.Delete(); Draft = new(); Step = 0; Review = null;
            IsDirty = false; Errors = ""; Status = "Draft discarded.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { Status = "Could not discard the saved draft. Try again."; }
        Changed();
    }
    private void Changed()
    {
        foreach (var name in new[] { nameof(Draft), nameof(Step), nameof(Status),
            nameof(Errors), nameof(Review), nameof(IsDirty) }) Notify(name);
    }
}
