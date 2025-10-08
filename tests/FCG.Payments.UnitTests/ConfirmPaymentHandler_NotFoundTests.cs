using FluentAssertions;
using Moq;
using Microsoft.Extensions.Configuration;
using FCG.Payments.Application.UseCases.Confirm;
using FCG.Payments.Application.Interfaces;
using FCG.Payments.Domain.Interfaces;

namespace FCG.Payments.UnitTests;

public class ConfirmPaymentHandler_NotFoundTests
{
    [Fact]
    public async Task Handle_Should_Return_False_When_Payment_Not_Found()
    {
        var repo = new Mock<IPaymentRepository>(MockBehavior.Strict);
        var eventStore = new Mock<IEventStore>(MockBehavior.Strict);
        var notifier = new Mock<INotificationPublisher>(MockBehavior.Strict);

        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Notifications:TopicArn"] = "arn:aws:sns:dummy"
            })
            .Build();

        var paymentId = Guid.NewGuid();
        var req = new ConfirmPaymentRequest(paymentId);

        repo.Setup(r => r.GetByIdAsync(paymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FCG.Payments.Domain.Entities.Payment?)null);

        var handler = new ConfirmPaymentHandler(repo.Object, eventStore.Object, notifier.Object, cfg);

        var result = await handler.Handle(req, CancellationToken.None);

        result.Should().BeFalse("pagamento inexistente deve retornar false");

        repo.Verify(r => r.GetByIdAsync(paymentId, It.IsAny<CancellationToken>()), Times.Once);
        repo.VerifyNoOtherCalls();
        eventStore.VerifyNoOtherCalls();
        notifier.VerifyNoOtherCalls();
    }
}
