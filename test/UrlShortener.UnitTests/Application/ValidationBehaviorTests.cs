using Application.Abstractions.Contracts;
using Application.Pipelines;
using FluentValidation;

namespace UrlShortener.UnitTests.Application;

public sealed class ValidationBehaviorTests
{
    public sealed record Request(string Url, int Limit) : ICommand<string>;

    [Fact]
    public async Task No_validators_calls_the_handler_once_and_returns_its_response()
    {
        var calls = 0;
        var behavior = new ValidationBehavior<Request, string>([]);

        var response = await behavior.Handle(new Request("https://example.com", 1), _ =>
        {
            calls++;
            return Task.FromResult("created");
        }, CancellationToken.None);

        Assert.Equal("created", response);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Valid_request_reaches_the_handler()
    {
        var validator = new InlineValidator<Request>();
        validator.RuleFor(x => x.Url).NotEmpty();
        var behavior = new ValidationBehavior<Request, string>([validator]);

        var response = await behavior.Handle(new Request("https://example.com", 1),
            _ => Task.FromResult("created"), CancellationToken.None);

        Assert.Equal("created", response);
    }

    [Fact]
    public async Task Async_rules_are_awaited_and_receive_the_caller_token()
    {
        using var cancellation = new CancellationTokenSource();
        var receivedToken = CancellationToken.None;
        var validator = new InlineValidator<Request>();
        validator.RuleFor(x => x.Url).MustAsync(async (url, token) =>
        {
            await Task.Yield();
            receivedToken = token;
            return false;
        });
        var behavior = new ValidationBehavior<Request, string>([validator]);
        var handlerCalled = false;

        var exception = await Assert.ThrowsAsync<ValidationException>(() => behavior.Handle(
            new Request("https://example.com", 1), _ =>
            {
                handlerCalled = true;
                return Task.FromResult("unexpected");
            }, cancellation.Token));

        Assert.Single(exception.Errors);
        Assert.Equal(cancellation.Token, receivedToken);
        Assert.False(handlerCalled);
    }

    [Fact]
    public async Task Cancelled_validation_does_not_call_the_handler()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var validator = new InlineValidator<Request>();
        validator.RuleFor(x => x.Url).NotEmpty();
        var behavior = new ValidationBehavior<Request, string>([validator]);
        var handlerCalled = false;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => behavior.Handle(
            new Request("https://example.com", 1), _ =>
            {
                handlerCalled = true;
                return Task.FromResult("unexpected");
            }, cancellation.Token));

        Assert.False(handlerCalled);
    }

    [Fact]
    public async Task Invalid_request_collects_failures_from_all_validators_and_does_not_call_handler()
    {
        var urlValidator = new InlineValidator<Request>();
        urlValidator.RuleFor(x => x.Url).NotEmpty();
        var limitValidator = new InlineValidator<Request>();
        limitValidator.RuleFor(x => x.Limit).GreaterThan(0);
        var behavior = new ValidationBehavior<Request, string>([urlValidator, limitValidator]);
        var called = false;

        var exception = await Assert.ThrowsAsync<ValidationException>(() => behavior.Handle(
            new Request("", 0), _ => { called = true; return Task.FromResult("unexpected"); }, CancellationToken.None));

        Assert.False(called);
        Assert.Equal(["Url", "Limit"], exception.Errors.Select(x => x.PropertyName));
    }
}
