using FluentValidation;
using MediatR;
using TakeAuction.Api.Common.Messaging;

namespace TakeAuction.Api.UnitTests.Common;

public sealed class ValidationBehaviorTests
{
    [Fact]
    public async Task Passes_the_request_through_when_no_validator_is_registered()
    {
        var behavior = new ValidationBehavior<SampleRequest, string>([]);

        var result = await behavior.Handle(new SampleRequest("ok"), Next, CancellationToken.None);

        Assert.Equal("handled", result);
    }

    [Fact]
    public async Task Passes_the_request_through_when_it_is_valid()
    {
        var behavior = new ValidationBehavior<SampleRequest, string>([new SampleValidator()]);

        var result = await behavior.Handle(new SampleRequest("ok"), Next, CancellationToken.None);

        Assert.Equal("handled", result);
    }

    [Fact]
    public async Task Throws_before_reaching_the_handler_when_the_request_is_invalid()
    {
        var behavior = new ValidationBehavior<SampleRequest, string>([new SampleValidator()]);
        var handlerWasCalled = false;

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            behavior.Handle(
                new SampleRequest(""),
                _ =>
                {
                    handlerWasCalled = true;
                    return Task.FromResult("handled");
                },
                CancellationToken.None));

        Assert.False(handlerWasCalled);
        Assert.Contains(exception.Errors, error => error.PropertyName == nameof(SampleRequest.Value));
    }

    [Fact]
    public async Task Aggregates_failures_from_every_validator()
    {
        var behavior = new ValidationBehavior<SampleRequest, string>(
            [new SampleValidator(), new SecondSampleValidator()]);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            behavior.Handle(new SampleRequest(""), Next, CancellationToken.None));

        Assert.Equal(2, exception.Errors.Count());
    }

    private static Task<string> Next(CancellationToken cancellationToken) => Task.FromResult("handled");

    public sealed record SampleRequest(string Value) : IRequest<string>;

    private sealed class SampleValidator : AbstractValidator<SampleRequest>
    {
        public SampleValidator() => RuleFor(request => request.Value).NotEmpty();
    }

    private sealed class SecondSampleValidator : AbstractValidator<SampleRequest>
    {
        public SecondSampleValidator() => RuleFor(request => request.Value).MinimumLength(3);
    }
}
