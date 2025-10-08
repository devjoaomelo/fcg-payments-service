using System;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;

using FCG.Payments.Application.Interfaces;
using FCG.Payments.Application.UseCases.Create;
using FCG.Payments.Domain.Entities;
using FCG.Payments.Domain.Enums;
using FCG.Payments.Domain.Interfaces;

namespace FCG.Payments.UnitTests;

public class CreatePaymentHandler_Success_Tests
{
    [Fact]
    public async Task Handle_Should_Create_Persist_And_Publish_When_Valid()
    {
        // arrange
        var paymentsRepo = new Mock<IPaymentRepository>(MockBehavior.Strict);
        var gamesClient = new Mock<IGamesCatalogClient>(MockBehavior.Strict);
        var bus = new Mock<IMessageBus>(MockBehavior.Strict);
        var eventStore = new Mock<IEventStore>(MockBehavior.Strict);

        var handler = new CreatePaymentHandler(
            paymentsRepo.Object, gamesClient.Object, bus.Object, eventStore.Object);

        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var price = 199.90m;
        var req = new CreatePaymentRequest(gameId);
        var queueUrl = "https://sqs.local/queue/payments-requested";

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("sub", userId.ToString()),
            new Claim(ClaimTypes.Role, "Customer")
        }, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        gamesClient.Setup(c => c.GetPriceAsync(gameId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(price);

        Payment? capturedPayment = null;
        paymentsRepo.Setup(r => r.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
                    .Callback<Payment, CancellationToken>((p, _) => capturedPayment = p)
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

        string? bus_QueueUrl = null;
        object? bus_Payload = null;

        bus.Setup(b => b.PublishAsync(
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
           .Callback<string, object, CancellationToken>((url, payload, _) =>
           {
               bus_QueueUrl = url;
               bus_Payload = payload;
           })
           .Returns(Task.CompletedTask);

        var res = await handler.Handle(req, principal, queueUrl, CancellationToken.None);

        res.Should().NotBeNull();
        res.UserId.Should().Be(userId);
        res.GameId.Should().Be(gameId);
        res.Amount.Should().Be(price);
        res.Status.Should().Be(PaymentStatus.Pending);
        res.Id.Should().NotBeEmpty();
        res.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        capturedPayment.Should().NotBeNull();
        capturedPayment!.Id.Should().Be(res.Id);
        capturedPayment.UserId.Should().Be(userId);
        capturedPayment.GameId.Should().Be(gameId);
        capturedPayment.Amount.Should().Be(price);
        capturedPayment.Status.Should().Be(PaymentStatus.Pending);

        evt_Id.Should().Be(res.Id);
        evt_Name.Should().Be("PaymentRequested");
        evt_Payload.Should().NotBeNull();

        var evtJson = JsonSerializer.Serialize(evt_Payload);
        using var evtDoc = JsonDocument.Parse(evtJson);
        evtDoc.RootElement.GetProperty("paymentId").GetGuid().Should().Be(res.Id);
        evtDoc.RootElement.GetProperty("amount").GetDecimal().Should().Be(price);

        bus_QueueUrl.Should().Be(queueUrl);
        bus_Payload.Should().NotBeNull();

        var busJson = JsonSerializer.Serialize(bus_Payload);
        using var busDoc = JsonDocument.Parse(busJson);
        busDoc.RootElement.GetProperty("paymentId").GetGuid().Should().Be(res.Id);
        busDoc.RootElement.GetProperty("userId").GetGuid().Should().Be(userId);

        gamesClient.Verify(c => c.GetPriceAsync(gameId, It.IsAny<CancellationToken>()), Times.Once);
        paymentsRepo.Verify(r => r.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Once);
        eventStore.Verify(es => es.AppendAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
        bus.Verify(b => b.PublishAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);

        gamesClient.VerifyNoOtherCalls();
        paymentsRepo.VerifyNoOtherCalls();
        eventStore.VerifyNoOtherCalls();
        bus.VerifyNoOtherCalls();
    }
}
