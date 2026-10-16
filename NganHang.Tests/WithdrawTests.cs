namespace NganHang.Tests;

/// <summary>
/// Kiểm thử RÚT TIỀN: số dư âm và hạn mức thấu chi.
/// </summary>
public class WithdrawTests : IDisposable
{
    private readonly TestBank _bank = new();
    public void Dispose() => _bank.Dispose();

    [Fact(DisplayName = "Tiết kiệm: rút vượt số dư bị chặn, số dư không bị âm")]
    public void Saving_WithdrawMoreThanBalance_IsRejected()
    {
        var result = _bank.Service.Withdraw(TestBank.Saving, 12_580_001);

        Assert.False(result.Ok);
        Assert.StartsWith("Số dư không đủ", result.Error);
        Assert.Equal(12_580_000, _bank.Balance(TestBank.Saving));
    }

    [Fact(DisplayName = "Tiết kiệm: rút đúng bằng số dư thì số dư về 0, không âm")]
    public void Saving_WithdrawExactBalance_LeavesZero()
    {
        var result = _bank.Service.Withdraw(TestBank.Saving, 12_580_000);

        Assert.True(result.Ok);
        Assert.Equal(0, _bank.Balance(TestBank.Saving));
    }

    [Fact(DisplayName = "Thanh toán: rút vượt số dư nhưng trong hạn mức thấu chi thì cho phép số dư âm")]
    public void Payment_WithdrawWithinOverdraft_AllowsNegativeBalance()
    {
        var result = _bank.Service.Withdraw(TestBank.Payment, 5_000_000);

        Assert.True(result.Ok);
        Assert.True(result.Value!.UsesOverdraft);
        Assert.Equal(-1_750_000, _bank.Balance(TestBank.Payment));
        Assert.Equal(-1_750_000, result.Value.Transaction.BalanceAfter);
    }

    [Fact(DisplayName = "Thanh toán: rút đúng bằng số dư + hạn mức thì hợp lệ (số dư = -hạn mức)")]
    public void Payment_WithdrawExactlyAvailable_IsAllowed()
    {
        var result = _bank.Service.Withdraw(TestBank.Payment, 13_250_000);

        Assert.True(result.Ok);
        Assert.Equal(-10_000_000, _bank.Balance(TestBank.Payment));
    }

    [Fact(DisplayName = "Thanh toán: rút vượt hạn mức thấu chi bị chặn, không ghi giao dịch")]
    public void Payment_WithdrawOverOverdraft_IsRejected()
    {
        var before = _bank.TransactionCount();

        var result = _bank.Service.Withdraw(TestBank.Payment, 13_250_001);

        Assert.False(result.Ok);
        Assert.Equal("Số tiền rút vượt quá hạn mức khả dụng.", result.Error);
        Assert.Equal(3_250_000, _bank.Balance(TestBank.Payment));
        Assert.Equal(before, _bank.TransactionCount());
    }

    [Fact(DisplayName = "Thanh toán: đã âm thì chỉ được rút tiếp phần hạn mức còn lại")]
    public void Payment_AlreadyNegative_OnlyRemainingOverdraftAvailable()
    {
        Assert.True(_bank.Service.Withdraw(TestBank.Payment, 5_000_000).Ok);   // còn -1.750.000

        Assert.False(_bank.Service.Withdraw(TestBank.Payment, 8_250_001).Ok);  // vượt 1 đồng
        Assert.True(_bank.Service.Withdraw(TestBank.Payment, 8_250_000).Ok);   // vừa đủ
        Assert.Equal(-10_000_000, _bank.Balance(TestBank.Payment));
        Assert.Equal(0, _bank.Service.GetAccount(TestBank.Payment)!.AvailableBalance);
    }

    [Theory(DisplayName = "Số tiền rút không hợp lệ (0 hoặc âm) bị chặn")]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(-5_000_000L)]
    public void Withdraw_InvalidAmount_IsRejected(long amount)
    {
        var result = _bank.Service.Withdraw(TestBank.Payment, amount);

        Assert.False(result.Ok);
        Assert.Equal(3_250_000, _bank.Balance(TestBank.Payment));
    }

    [Fact(DisplayName = "Rút từ tài khoản không tồn tại trả về NotFound")]
    public void Withdraw_UnknownAccount_ReturnsNotFound()
    {
        var result = _bank.Service.Withdraw("khong-co", 100_000);

        Assert.False(result.Ok);
        Assert.True(result.NotFound);
    }
}
