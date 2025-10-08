
using FCG.Payments.Domain.Entities;
using FCG.Payments.Domain.Enums;
using FluentAssertions;

namespace FCG.Payments.UnitTests;

public class Payment_ConfirmTests
{
    [Fact]
    public void Confirm_Should_Set_Paid_And_Update_Timestamp()
    {
        var p = new Payment(Guid.NewGuid(), Guid.NewGuid(), 100m);

        var changed = p.Confirm();

        changed.Should().BeTrue();
        p.Status.Should().Be(PaymentStatus.Paid);
        p.UpdatedAtUtc.Should().NotBeNull();
        p.UpdatedAtUtc!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Confirm_Should_Be_Idempotent_When_Already_Paid()
    {
        var p = new Payment(Guid.NewGuid(), Guid.NewGuid(), 100m);
        p.Confirm(); 

        var before = p.UpdatedAtUtc;
        var changedAgain = p.Confirm(); 

        changedAgain.Should().BeFalse();
        p.Status.Should().Be(PaymentStatus.Paid);
        p.UpdatedAtUtc.Should().Be(before);
    }
}
