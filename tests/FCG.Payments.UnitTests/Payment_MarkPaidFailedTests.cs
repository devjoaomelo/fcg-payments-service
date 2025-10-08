using System;
using FCG.Payments.Domain.Entities;
using FCG.Payments.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace FCG.Payments.UnitTests;

public class Payment_MarkPaidFailedTests
{
    [Fact]
    public void MarkPaid_Should_Set_Paid_And_Update_Timestamp()
    {
        var p = new Payment(Guid.NewGuid(), Guid.NewGuid(), 50m);
        p.MarkPaid();

        p.Status.Should().Be(PaymentStatus.Paid);
        p.UpdatedAtUtc.Should().NotBeNull();
        p.UpdatedAtUtc!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void MarkFailed_Should_Set_Failed_And_Update_Timestamp()
    {
        var p = new Payment(Guid.NewGuid(), Guid.NewGuid(), 50m);
        p.MarkFailed();

        p.Status.Should().Be(PaymentStatus.Failed);
        p.UpdatedAtUtc.Should().NotBeNull();
        p.UpdatedAtUtc!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}
