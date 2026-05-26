using FluentAssertions;

using Memoria.Bot.Conversations;

using NSubstitute;

using Telegram.Bot;
using Telegram.Bot.Types;

namespace Memoria.Bot.UnitTests.Conversations;

public sealed class AddCardDialogHandlerTests
{
    private const long ChatId = 1234;

    private readonly ITelegramBotClient _client = Substitute.For<ITelegramBotClient>();
    private readonly InMemoryConversationStateStore _store = new();
    private readonly AddCardDialogHandler _sut;

    public AddCardDialogHandlerTests()
    {
        _sut = new AddCardDialogHandler(_client, _store);
    }

    private static Message MakeMessage(string text) => new()
    {
        Chat = new Chat { Id = ChatId },
        Text = text,
    };

    [Fact]
    public async Task WaitingForTitleAcceptsTitleAndAdvancesToWaitingForBody()
    {
        var initial = new AddCardDialogState(AddCardStep.WaitingForTitle);
        _store.Start(ChatId, initial);

        await _sut.HandleAsync(MakeMessage("PostgreSQL VACUUM"), initial, CancellationToken.None);

        _store.TryGet(ChatId, out var state).Should().BeTrue();
        var st = state.Should().BeOfType<AddCardDialogState>().Subject;
        st.Step.Should().Be(AddCardStep.WaitingForBody);
        st.Title.Should().Be("PostgreSQL VACUUM");
    }

    [Fact]
    public async Task WaitingForTitleRejectsEmpty()
    {
        var initial = new AddCardDialogState(AddCardStep.WaitingForTitle);
        _store.Start(ChatId, initial);

        await _sut.HandleAsync(MakeMessage("   "), initial, CancellationToken.None);

        _store.TryGet(ChatId, out var state).Should().BeTrue();
        state.Should().BeSameAs(initial, because: "rejected input keeps the same state");
    }

    [Fact]
    public async Task WaitingForTitleRejectsTooLong()
    {
        var initial = new AddCardDialogState(AddCardStep.WaitingForTitle);
        _store.Start(ChatId, initial);

        await _sut.HandleAsync(MakeMessage(new string('a', 201)), initial, CancellationToken.None);

        _store.TryGet(ChatId, out var state).Should().BeTrue();
        ((AddCardDialogState)state!).Title.Should().BeNull();
    }

    [Fact]
    public async Task WaitingForBodyAcceptsBodyAndAdvancesToWaitingForTags()
    {
        var state = new AddCardDialogState(AddCardStep.WaitingForBody, Title: "T");
        _store.Start(ChatId, state);

        await _sut.HandleAsync(MakeMessage("Body text"), state, CancellationToken.None);

        _store.TryGet(ChatId, out var updated).Should().BeTrue();
        var st = updated.Should().BeOfType<AddCardDialogState>().Subject;
        st.Step.Should().Be(AddCardStep.WaitingForTags);
        st.Body.Should().Be("Body text");
    }

    [Fact]
    public async Task WaitingForTagsExtractsTagsAndShowsPreview()
    {
        var state = new AddCardDialogState(AddCardStep.WaitingForTags, Title: "T", Body: "B");
        _store.Start(ChatId, state);

        await _sut.HandleAsync(MakeMessage("#foo #bar"), state, CancellationToken.None);

        _store.TryGet(ChatId, out var updated).Should().BeTrue();
        var st = updated.Should().BeOfType<AddCardDialogState>().Subject;
        st.Step.Should().Be(AddCardStep.Preview);
        st.Tags.Should().BeEquivalentTo(new[] { "foo", "bar" });
    }

    [Fact]
    public async Task WaitingForTagsSkipKeywordResultsInEmptyTags()
    {
        var state = new AddCardDialogState(AddCardStep.WaitingForTags, Title: "T", Body: "B");
        _store.Start(ChatId, state);

        await _sut.HandleAsync(MakeMessage("skip"), state, CancellationToken.None);

        _store.TryGet(ChatId, out var updated).Should().BeTrue();
        var st = updated.Should().BeOfType<AddCardDialogState>().Subject;
        st.Step.Should().Be(AddCardStep.Preview);
        st.Tags.Should().BeEmpty();
    }

    [Fact]
    public async Task PreviewStepIgnoresTextMessages()
    {
        var state = new AddCardDialogState(AddCardStep.Preview, Title: "T", Body: "B", Tags: Array.Empty<string>());
        _store.Start(ChatId, state);

        await _sut.HandleAsync(MakeMessage("anything"), state, CancellationToken.None);

        _store.TryGet(ChatId, out var stored).Should().BeTrue();
        stored.Should().BeSameAs(state, because: "Preview should only transition via callback");
    }
}
