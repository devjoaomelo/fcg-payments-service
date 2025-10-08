using System.Security.Claims;
using FluentAssertions;
using Moq;
using FCG.Payments.Application.Interfaces;
using FCG.Payments.Application.UseCases.Create;
using FCG.Payments.Domain.Interfaces;

namespace FCG.Payments.UnitTests;

public class CreatePaymentHandler_Error_InvalidPriceTests
{
    [Fact]
    public async Task Handle_Should_Throw_When_GamePrice_Is_Null()
    {
        var repo = new Mock<IPaymentRepository>(MockBehavior.Strict);
        var gamesClient = new Mock<IGamesCatalogClient>(MockBehavior.Strict);
        var bus = new Mock<IMessageBus>(MockBehavior.Strict);
        var eventStore = new Mock<IEventStore>(MockBehavior.Strict);

        var handler = new CreatePaymentHandler(repo.Object, gamesClient.Object, bus.Object, eventStore.Object);

        var gameId = Guid.NewGuid();
        var req = new CreatePaymentRequest(gameId);
        var userId = Guid.NewGuid();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("sub", userId.ToString())
        }, "TestAuth"));

        var queueUrl = "https://sqs.local/payments-requested";

        gamesClient.Setup(c => c.GetPriceAsync(gameId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((decimal?)null);

        var act = async () => await handler.Handle(req, principal, queueUrl, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();

        repo.VerifyNoOtherCalls();
        bus.VerifyNoOtherCalls();
        eventStore.VerifyNoOtherCalls();
        gamesClient.Verify(c => c.GetPriceAsync(gameId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task Handle_Should_Throw_When_GamePrice_Is_NonPositive(int priceInt)
    {
        var repo = new Mock<IPaymentRepository>(MockBehavior.Strict);
        var gamesClient = new Mock<IGamesCatalogClient>(MockBehavior.Strict);
        var bus = new Mock<IMessageBus>(MockBehavior.Strict);
        var eventStore = new Mock<IEventStore>(MockBehavior.Strict);

        var handler = new CreatePaymentHandler(repo.Object, gamesClient.Object, bus.Object, eventStore.Object);

        var gameId = Guid.NewGuid();
        var req = new CreatePaymentRequest(gameId);
        var userId = Guid.NewGuid();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("sub", userId.ToString())
        }, "TestAuth"));

        var queueUrl = "https://sqs.local/payments-requested";

        decimal? price = priceInt;

        gamesClient.Setup(c => c.GetPriceAsync(gameId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(price);

        var act = async () => await handler.Handle(req, principal, queueUrl, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();

        repo.VerifyNoOtherCalls();
        bus.VerifyNoOtherCalls();
        eventStore.VerifyNoOtherCalls();
        gamesClient.Verify(c => c.GetPriceAsync(gameId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
