using FluentAssertions;
using Moq;
using Microsoft.Extensions.Configuration;
using FCG.Payments.Application.UseCases.Confirm;
using FCG.Payments.Application.Interfaces;
using FCG.Payments.Application.Events;
using FCG.Payments.Domain.Entities;
using FCG.Payments.Domain.Enums;
using FCG.Payments.Domain.Interfaces;

namespace FCG.Payments.UnitTests;

public class ConfirmPaymentHandler_Success_Tests
{
    [Fact]
    public async Task Handle_Should_Confirm_Update_AppendEvent_And_Notify()
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

        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var payment = new Payment(userId, gameId, 150m); 
        var request = new ConfirmPaymentRequest(payment.Id);

        repo.Setup(r => r.GetByIdAsync(payment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        repo.Setup(r => r.UpdateAsync(payment, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Guid evt_Id = Guid.Empty;
        string? evt_Name = null;
        object? evt_Payload = null;

        eventStore.Setup(es => es.AppendAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, string, object, CancellationToken>((id, name, payload, _) =>
            {
                evt_Id = id;
                evt_Name = name;
                evt_Payload = payload;
            })
            .Returns(Task.CompletedTask);

        string? publishedTopic = null;
        PaymentConfirmed? publishedEvent = null;

        notifier.Setup(n => n.PublishAsync(
                It.IsAny<string>(),
                It.IsAny<PaymentConfirmed>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, PaymentConfirmed, CancellationToken>((topic, ev, _) =>
            {
                publishedTopic = topic;
                publishedEvent = ev;
            })
            .Returns(Task.CompletedTask);

        var handler = new ConfirmPaymentHandler(repo.Object, eventStore.Object, notifier.Object, cfg);

        var result = await handler.Handle(request, CancellationToken.None);

        result.Should().BeTrue();

        payment.Status.Should().Be(PaymentStatus.Paid);
        payment.UpdatedAtUtc.Should().NotBeNull();
        payment.UpdatedAtUtc!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        repo.Verify(r => r.GetByIdAsync(payment.Id, It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.UpdateAsync(payment, It.IsAny<CancellationToken>()), Times.Once);
        repo.VerifyNoOtherCalls();

        evt_Id.Should().Be(payment.Id);
        evt_Name.Should().Be("PaymentConfirmed");
        evt_Payload.Should().NotBeNull();

        var t = evt_Payload!.GetType();
        t.GetProperty("paymentId")!.GetValue(evt_Payload)!.Should().Be(payment.Id);
        t.GetProperty("userId")!.GetValue(evt_Payload)!.Should().Be(payment.UserId);
        t.GetProperty("gameId")!.GetValue(evt_Payload)!.Should().Be(payment.GameId);
        t.GetProperty("amount")!.GetValue(evt_Payload)!.Should().Be(payment.Amount);

        var confirmedAtFromEventStore = (DateTime)t.GetProperty("confirmedAtUtc")!.GetValue(evt_Payload)!;
        confirmedAtFromEventStore.Should().BeCloseTo(payment.UpdatedAtUtc!.Value, TimeSpan.FromSeconds(5));

        eventStore.Verify(es => es.AppendAsync(
            payment.Id, "PaymentConfirmed", It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
        eventStore.VerifyNoOtherCalls();

        publishedTopic.Should().Be("arn:aws:sns:us-east-1:123456789012:fcg-payments-events");
        publishedEvent.Should().NotBeNull();
        publishedEvent!.paymentId.Should().Be(payment.Id);
        publishedEvent.userId.Should().Be(payment.UserId);
        publishedEvent.gameId.Should().Be(payment.GameId);
        publishedEvent.amount.Should().Be(payment.Amount);

        publishedEvent.confirmedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        notifier.Verify(n => n.PublishAsync(
            "arn:aws:sns:us-east-1:123456789012:fcg-payments-events",
            It.IsAny<PaymentConfirmed>(),
            It.IsAny<CancellationToken>()), Times.Once);
        notifier.VerifyNoOtherCalls();
    }
}
