using FluentAssertions;

using Memoria.Bot.Conversations;

namespace Memoria.Bot.UnitTests.Conversations;

public sealed class InMemoryConversationStateStoreTests
{
    private readonly InMemoryConversationStateStore _store = new();

    [Fact]
    public void TryGetReturnsFalseWhenNoState()
    {
        var found = _store.TryGet(chatId: 42, out var state);
        found.Should().BeFalse();
        state.Should().BeNull();
    }

    [Fact]
    public void StartThenTryGetReturnsState()
    {
        var initial = new AddCardDialogState(AddCardStep.WaitingForTitle);
        _store.Start(chatId: 1, initial);

        var found = _store.TryGet(1, out var state);
        found.Should().BeTrue();
        state.Should().BeSameAs(initial);
    }

    [Fact]
    public void UpdateReplacesState()
    {
        var initial = new AddCardDialogState(AddCardStep.WaitingForTitle);
        _store.Start(chatId: 7, initial);

        var updated = initial with { Step = AddCardStep.WaitingForBody, Title = "Topic" };
        _store.Update(7, updated);

        _store.TryGet(7, out var state).Should().BeTrue();
        state.Should().BeSameAs(updated);
    }

    [Fact]
    public void ClearRemovesState()
    {
        _store.Start(99, new AddCardDialogState(AddCardStep.WaitingForTitle));
        _store.Clear(99).Should().BeTrue();
        _store.TryGet(99, out _).Should().BeFalse();
        _store.Clear(99).Should().BeFalse(because: "Clear is idempotent and reports whether state existed");
    }

    [Fact]
    public async Task ConcurrentStartCallsDoNotConflict()
    {
        var tasks = Enumerable.Range(0, 100)
            .Select(i => Task.Run(() =>
                _store.Start(chatId: i, new AddCardDialogState(AddCardStep.WaitingForTitle))))
            .ToArray();

        await Task.WhenAll(tasks);

        for (var i = 0; i < 100; i++)
        {
            _store.TryGet(i, out var s).Should().BeTrue();
            s.Should().NotBeNull();
        }
    }
}
