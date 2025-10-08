using System;
using FCG.Payments.Domain.Entities;
using FCG.Payments.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace FCG.Payments.UnitTests;

public class Payment_ConstructionTests
{
    [Fact]
    public void Ctor_Should_Create_Payment_When_Valid()
    {
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        decimal amount = 199.90m;

        var p = new Payment(userId, gameId, amount);

        p.Should().NotBeNull();
        p.Id.Should().NotBe(Guid.Empty);
        p.UserId.Should().Be(userId);
        p.GameId.Should().Be(gameId);
        p.Amount.Should().Be(amount);
        p.Status.Should().Be(PaymentStatus.Pending);
        p.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        p.UpdatedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Ctor_Should_Throw_When_UserId_Is_Empty()
    {
        var gameId = Guid.NewGuid();
        var act = () => new Payment(Guid.Empty, gameId, 10m);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_Should_Throw_When_GameId_Is_Empty()
    {
        var userId = Guid.NewGuid();
        var act = () => new Payment(userId, Guid.Empty, 10m);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    [InlineData(-10)]
    public void Ctor_Should_Throw_When_Amount_Not_Positive(decimal badAmount)
    {
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var act = () => new Payment(userId, gameId, badAmount);
        act.Should().Throw<ArgumentException>();
    }
}
