using GnazzosWorld.Core.Orders;
using GnazzosWorld.Core.Presentation;
using Xunit;

namespace GnazzosWorld.Core.Tests;

public sealed class SongRequestTests
{
    private static SongRequest Valid() => new()
    {
        Genre = "Folk", Occasion = "Anniversary", Story = "A trip to Long Lake",
        ContactName = "Demo Customer", Email = "demo@example.com"
    };
    private sealed class Store : IRequestDraftStore
    {
        public SongRequest? Value;
        public bool Fail;
        public SongRequest? Load() => Value;
        public void Save(SongRequest draft)
        { if (Fail) throw new IOException(); Value = draft; }
        public void Delete()
        { if (Fail) throw new IOException(); Value = null; }
    }
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [InlineData(4)] [InlineData(6)] [InlineData(8)]
    public void Supported_lengths_are_valid(int minutes)
        => Assert.Empty(SongRequestRules.Validate(Valid() with { Minutes = minutes }, 2));
    [Theory]
    [InlineData(-1)] [InlineData(0)] [InlineData(5)] [InlineData(9)]
    public void Unsupported_lengths_are_reported(int minutes)
        => Assert.Contains(SongRequestRules.Validate(Valid() with { Minutes = minutes }, 2),
            e => e.Field == "Length");
    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void Later_contact_is_not_required_early(int step)
        => Assert.Empty(SongRequestRules.Validate(Valid() with { Email = "", ContactName = "" }, step));
    [Fact]
    public void Final_step_revalidates_previous_steps()
        => Assert.Contains(SongRequestRules.Validate(Valid() with { Genre = " " }, 2), e => e.Field == "Genre");
    [Theory]
    [InlineData("not email")] [InlineData("Name <demo@example.com>")]
    public void Invalid_contact_is_rejected(string email)
        => Assert.Contains(SongRequestRules.Validate(Valid() with { Email = email }, 2), e => e.Field == "Email");
    [Fact]
    public void Oversized_story_is_reported()
        => Assert.Contains(SongRequestRules.Validate(Valid() with { Story = new string('x',4001) }, 2), e => e.Field == "Story");
    [Fact]
    public void Boundary_story_is_accepted()
        => Assert.Empty(SongRequestRules.Validate(Valid() with { Story = new string('x',4000) }, 2));
    [Fact]
    public void Instrumental_review_omits_hidden_notes_without_erasing_draft()
    {
        var draft = Valid() with { WithVocals = false, VocalNotes = "Warm baritone" };
        Assert.Equal("", draft.ForReview().VocalNotes);
        Assert.Equal("Warm baritone", draft.VocalNotes);
    }
    [Fact]
    public void Review_trims_outer_whitespace()
        => Assert.Equal("Folk", (Valid() with { Genre = " Folk " }).ForReview().Genre);
    [Fact]
    public void Invalid_next_stays_on_step_and_preserves_input()
    {
        var vm = new SongRequestViewModel(new Store());
        vm.Edit(Valid() with { Genre = "" });
        Assert.False(vm.Next()); Assert.Equal(0, vm.Step);
        Assert.Equal("A trip to Long Lake", vm.Draft.Story);
    }
    [Fact]
    public void Back_preserves_story()
    {
        var vm = new SongRequestViewModel(new Store()); vm.Edit(Valid());
        Assert.True(vm.Next()); vm.Back();
        Assert.Equal("A trip to Long Lake", vm.Draft.Story);
    }
    [Fact]
    public void Review_is_invalidated_by_edit()
    {
        var vm = new SongRequestViewModel(new Store()); vm.Edit(Valid());
        vm.Next(); vm.Next(); vm.Next(); Assert.NotNull(vm.Review);
        vm.Edit(vm.Draft with { Email = "bad" }); Assert.Null(vm.Review);
    }
    [Fact]
    public void Failed_save_keeps_input_and_previous_saved_draft()
    {
        var store = new Store { Value = Valid(), Fail = true };
        var vm = new SongRequestViewModel(store); vm.Edit(Valid() with { Story = "New story" });
        Assert.False(vm.Save()); Assert.True(vm.IsDirty);
        Assert.Equal("New story", vm.Draft.Story);
        Assert.Equal("A trip to Long Lake", store.Value!.Story);
    }
    [Fact]
    public void Explicit_save_can_restore_after_new_view_model()
    {
        var store = new Store(); var first = new SongRequestViewModel(store);
        first.Edit(Valid()); Assert.True(first.Save());
        var second = new SongRequestViewModel(store); second.Restore();
        Assert.Equal(Valid(), second.Draft);
    }
    [Fact]
    public void Restore_never_overwrites_unsaved_edits()
    {
        var vm = new SongRequestViewModel(new Store { Value = Valid() });
        vm.Edit(Valid() with { Story = "New" }); vm.Restore();
        Assert.Equal("New", vm.Draft.Story);
    }
    [Fact]
    public void Failed_discard_preserves_edits()
    {
        var vm = new SongRequestViewModel(new Store { Fail = true }); vm.Edit(Valid());
        vm.Discard(); Assert.Equal(Valid(), vm.Draft); Assert.True(vm.IsDirty);
    }
    [Fact]
    public void Discard_removes_saved_and_open_draft()
    {
        var store = new Store { Value = Valid() };
        var vm = new SongRequestViewModel(store); vm.Edit(Valid()); vm.Discard();
        Assert.Null(store.Value); Assert.Equal(new SongRequest(), vm.Draft);
    }
    [Theory]
    [InlineData(-1)] [InlineData(3)]
    public void Invalid_step_is_rejected(int step)
        => Assert.Throws<ArgumentOutOfRangeException>(() => SongRequestRules.Validate(Valid(),step));
}
