using FluentAssertions;
using Moq;
using Microsoft.Extensions.Configuration;

using FCG.Payments.Application.UseCases.Confirm;
using FCG.Payments.Application.Interfaces;
using FCG.Payments.Domain.Entities;
using FCG.Payments.Domain.Enums;
using FCG.Payments.Domain.Interfaces;

namespace FCG.Payments.UnitTests;

public class ConfirmPaymentHandler_AlreadyPaidTests
{
    [Fact]
    public async Task Handle_Should_Return_True_And_Do_Nothing_When_Already_Paid()
    {
        var repo = new Mock<IPaymentRepository>(MockBehavior.Strict);
        var eventStore = new Mock<IEventStore>(MockBehavior.Strict);
        var notifier = new Mock<INotificationPublisher>(MockBehavior.Strict);

        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Notifications:TopicArn"] = "arn:aws:sns:us-east-1:123456789012:fcg-payments-events"
            })
            .Build();

        var payment = new Payment(Guid.NewGuid(), Guid.NewGuid(), 123m);
        payment.MarkPaid(); 

        var req = new ConfirmPaymentRequest(payment.Id);

        repo.Setup(r => r.GetByIdAsync(payment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var handler = new ConfirmPaymentHandler(repo.Object, eventStore.Object, notifier.Object, cfg);

        var result = await handler.Handle(req, CancellationToken.None);

        result.Should().BeTrue(); 
        payment.Status.Should().Be(PaymentStatus.Paid);

        repo.Verify(r => r.GetByIdAsync(payment.Id, It.IsAny<CancellationToken>()), Times.Once);
        repo.VerifyNoOtherCalls();
        eventStore.VerifyNoOtherCalls();
        notifier.VerifyNoOtherCalls();
    }
}
