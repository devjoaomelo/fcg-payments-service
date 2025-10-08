using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;

using FCG.Payments.Application.Interfaces;
using FCG.Payments.Application.UseCases.Create;
using FCG.Payments.Domain.Interfaces;

namespace FCG.Payments.UnitTests;

public class CreatePaymentHandler_Error_QueueUrlTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_Should_Throw_When_QueueUrl_Is_NullOrWhiteSpace(string? badQueueUrl)
    {
        var paymentsRepo = new Mock<IPaymentRepository>(MockBehavior.Strict);
        var gamesClient = new Mock<IGamesCatalogClient>(MockBehavior.Strict);
        var bus = new Mock<IMessageBus>(MockBehavior.Strict);
        var eventStore = new Mock<IEventStore>(MockBehavior.Strict);

        var handler = new CreatePaymentHandler(
            paymentsRepo.Object, gamesClient.Object, bus.Object, eventStore.Object);

        var gameId = Guid.NewGuid();
        var req = new CreatePaymentRequest(gameId);

        var userId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("sub", userId.ToString()),
            new Claim(ClaimTypes.Role, "Customer")
        }, "TestAuth"));

        var act = async () => await handler.Handle(req, principal, badQueueUrl!, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("*Queue URL not configured*");

        paymentsRepo.VerifyNoOtherCalls();
        gamesClient.VerifyNoOtherCalls();
        bus.VerifyNoOtherCalls();
        eventStore.VerifyNoOtherCalls();
    }
}
