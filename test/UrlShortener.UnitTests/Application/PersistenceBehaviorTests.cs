using Application.Abstractions.Contracts;
using Application.Pipelines;

namespace UrlShortener.UnitTests.Application;

public sealed class PersistenceBehaviorTests
{
    private sealed record Command : ICommand<string>;
    private sealed record TransactionalCommand : ITransactionalCommand<string>;
    private sealed record Query : IQuery<string>;

    [Fact]
    public async Task Ordinary_command_saves_after_the_handler_and_forwards_cancellation()
    {
        var work = new RecordingUnitOfWork();
        using var cancellation = new CancellationTokenSource();
        var behavior = new SaveChangesBehavior<Command, string>(work);

        var response = await behavior.Handle(new Command(), _ =>
        {
            work.Calls.Add("handler");
            return Task.FromResult("created");
        }, cancellation.Token);

        Assert.Equal("created", response);
        Assert.Equal(["handler", "save"], work.Calls);
        Assert.Equal(cancellation.Token, work.SavedToken);
    }

    [Fact]
    public async Task Query_does_not_save_or_open_a_transaction()
    {
        var work = new RecordingUnitOfWork();
        var save = new SaveChangesBehavior<Query, string>(work);
        var transaction = new TransactionBehavior<Query, string>(work);

        var response = await transaction.Handle(new Query(),
            outerToken => save.Handle(new Query(), _ => Task.FromResult("found"), CancellationToken.None), CancellationToken.None);

        Assert.Equal("found", response);
        Assert.Empty(work.Calls);
    }

    [Fact]
    public async Task Failed_ordinary_command_does_not_save_and_preserves_exception()
    {
        var work = new RecordingUnitOfWork();
        var expected = new InvalidOperationException("handler failed");
        var behavior = new SaveChangesBehavior<Command, string>(work);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => behavior.Handle(
            new Command(), _ => throw expected, CancellationToken.None));

        Assert.Same(expected, actual);
        Assert.Empty(work.Calls);
    }

    [Fact]
    public async Task Transactional_command_saves_once_then_commits_and_disposes()
    {
        var work = new RecordingUnitOfWork();
        var command = new TransactionalCommand();
        var save = new SaveChangesBehavior<TransactionalCommand, string>(work);
        var transaction = new TransactionBehavior<TransactionalCommand, string>(work);

        var response = await transaction.Handle(command, _ => save.Handle(command, _ =>
        {
            work.Calls.Add("handler");
            return Task.FromResult("created");
        }, CancellationToken.None), CancellationToken.None);

        Assert.Equal("created", response);
        Assert.Equal(["begin", "handler", "save", "commit", "dispose"], work.Calls);
    }

    [Theory]
    [InlineData("handler")]
    [InlineData("save")]
    [InlineData("commit")]
    public async Task Transaction_failure_rolls_back_disposes_and_preserves_exception(string failureAt)
    {
        var expected = new InvalidOperationException("deliberate failure");
        var work = new RecordingUnitOfWork { FailureAt = failureAt, Failure = expected };
        var behavior = new TransactionBehavior<TransactionalCommand, string>(work);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => behavior.Handle(new TransactionalCommand(), _ =>
        {
            work.Record("handler");
            return Task.FromResult("created");
        }, CancellationToken.None));

        Assert.Same(expected, actual);
        string[] expectedCalls = failureAt switch
        {
            "handler" => ["begin", "handler", "rollback", "dispose"],
            "save" => ["begin", "handler", "save", "rollback", "dispose"],
            _ => ["begin", "handler", "save", "commit", "rollback", "dispose"]
        };
        Assert.Equal(expectedCalls, work.Calls);
    }

    [Fact]
    public async Task Existing_transaction_is_owned_by_the_outer_caller()
    {
        var work = new RecordingUnitOfWork { HasActiveTransaction = true };
        var behavior = new TransactionBehavior<TransactionalCommand, string>(work);

        var response = await behavior.Handle(new TransactionalCommand(), _ => Task.FromResult("created"), CancellationToken.None);

        Assert.Equal("created", response);
        Assert.Empty(work.Calls);
    }

    // Only a call recorder for pipeline orchestration. PostgreSQL and Redis are
    // exercised by the integration project; neither has a fake implementation.
    private sealed class RecordingUnitOfWork : IUnitOfWork, ITransaction
    {
        public List<string> Calls { get; } = [];
        public bool HasActiveTransaction { get; init; }
        public CancellationToken SavedToken { get; private set; }
        public string? FailureAt { get; init; }
        public Exception? Failure { get; init; }

        public void Record(string operation)
        {
            Calls.Add(operation);
            if (operation == FailureAt) throw Failure!;
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SavedToken = cancellationToken;
            Record("save");
            return Task.FromResult(1);
        }

        public Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            Record("begin");
            return Task.FromResult<ITransaction>(this);
        }

        public Task CommitAsync(CancellationToken cancellationToken = default) { Record("commit"); return Task.CompletedTask; }
        public Task RollbackAsync(CancellationToken cancellationToken = default) { Record("rollback"); return Task.CompletedTask; }
        public ValueTask DisposeAsync() { Record("dispose"); return ValueTask.CompletedTask; }
    }
}
