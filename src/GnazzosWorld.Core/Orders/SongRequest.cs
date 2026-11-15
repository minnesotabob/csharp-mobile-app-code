using System.Net.Mail;

namespace GnazzosWorld.Core.Orders;

public sealed record SongRequest
{
    public int Minutes { get; init; } = 3;
    public int Quantity { get; init; } = 1;
    public bool WithVocals { get; init; } = true;
    public string Genre { get; init; } = "";
    public string Occasion { get; init; } = "";
    public bool AllowExplicit { get; init; }
    public string VocalNotes { get; init; } = "";
    public string Story { get; init; } = "";
    public string ContactName { get; init; } = "";
    public string Email { get; init; } = "";

    // Hidden vocal values stay in the draft, but never in an instrumental request.
    public SongRequest ForReview() => this with
    {
        Genre = Genre.Trim(), Occasion = Occasion.Trim(),
        Story = Story.Trim(), ContactName = ContactName.Trim(),
        Email = Email.Trim(),
        VocalNotes = WithVocals ? VocalNotes.Trim() : ""
    };
}

public sealed record RequestError(string Field, string Message);

public static class SongRequestRules
{
    public static IReadOnlyList<RequestError> Validate(SongRequest draft, int step)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (step is < 0 or > 2)
            throw new ArgumentOutOfRangeException(nameof(step));
        var errors = new List<RequestError>();
        void Text(string field, string value, int maximum, bool required = true)
        {
            if (required && string.IsNullOrWhiteSpace(value))
                errors.Add(new(field, $"Enter {field}."));
            else if (value.Length > maximum)
                errors.Add(new(field, $"{field} must be {maximum} characters or fewer."));
        }
        if (draft.Minutes is not (1 or 2 or 3 or 4 or 6 or 8))
            errors.Add(new("Length", "Choose 1, 2, 3, 4, 6, or 8 minutes."));
        if (draft.Quantity is < 1 or > 3)
            errors.Add(new("Quantity", "Choose one, two, or three songs with the same specification."));
        Text("Genre", draft.Genre, 80);
        Text("Occasion", draft.Occasion, 120);
        if (draft.WithVocals) Text("Vocal notes", draft.VocalNotes, 500, false);
        if (step >= 1) Text("Story", draft.Story, 4000);
        if (step >= 2)
        {
            Text("Contact name", draft.ContactName, 120);
            Text("Email", draft.Email, 254);
            if (!string.IsNullOrWhiteSpace(draft.Email) &&
                (!MailAddress.TryCreate(draft.Email.Trim(), out var address) ||
                 address.Address != draft.Email.Trim()))
                errors.Add(new("Email", "Enter one email address without a display name."));
        }
        return errors;
    }
}

public interface IRequestDraftStore
{
    SongRequest? Load();
    void Save(SongRequest draft);
    void Delete();
}
