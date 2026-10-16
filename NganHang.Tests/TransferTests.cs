using Microsoft.Extensions.Logging.Abstractions;
using NganHang.Data;
using NganHang.Models;
using NganHang.Services;

namespace NganHang.Tests;

/// <summary>
/// Kiểm thử CHUYỂN TIỀN: chuyển nội bộ, chuyển ra ngoài, chuyển vượt hạn mức,
/// số dư âm, lịch sử giao dịch và lưu file.
/// </summary>
public class TransferTests : IDisposable
{
    private readonly TestBank _bank = new();
    public void Dispose() => _bank.Dispose();

    /// <summary>Chuyển tới người nhận ở ngân hàng khác.</summary>
    private static TransferRequest External(string from, long amount, string? note = null) =>
        new(from, null, "0987 654 321", "NGUYEN VAN A", "Vietcombank", amount, note);

    /// <summary>Chuyển giữa 2 tài khoản của chính mình.</summary>
    private static TransferRequest Internal(string from, string to, long amount) =>
        new(from, to, null, null, null, amount, null);

    // ---------------- Chuyển hợp lệ ----------------

    [Fact(DisplayName = "Chuyển nội bộ: trừ tài khoản nguồn, cộng tài khoản nhận, tổng tài sản không đổi")]
    public void Internal_Valid_MovesMoneyBetweenAccounts()
    {
        var totalBefore = _bank.Balance(TestBank.Saving) + _bank.Balance(TestBank.Payment);

        var result = _bank.Service.Transfer(Internal(TestBank.Payment, TestBank.Saving, 1_000_000));

        Assert.True(result.Ok, result.Error);
        Assert.Equal(2_250_000, _bank.Balance(TestBank.Payment));
        Assert.Equal(13_580_000, _bank.Balance(TestBank.Saving));
        Assert.Equal(totalBefore, _bank.Balance(TestBank.Saving) + _bank.Balance(TestBank.Payment));
        Assert.NotNull(result.Value!.To);
        Assert.NotNull(result.Value.InTransaction);
    }

    [Fact(DisplayName = "Chuyển nội bộ: tạo 2 giao dịch (chuyển đi + nhận) với số dư sau giao dịch đúng")]
    public void Internal_Valid_CreatesTwoTransactions()
    {
        var before = _bank.TransactionCount();

        var result = _bank.Service.Transfer(Internal(TestBank.Payment, TestBank.Saving, 1_000_000)).Value!;

        Assert.Equal(before + 2, _bank.TransactionCount());
        Assert.Equal("transfer_out", result.OutTransaction.Type);
        Assert.Equal(TestBank.Payment, result.OutTransaction.AccountId);
        Assert.Equal(2_250_000, result.OutTransaction.BalanceAfter);
        Assert.Equal("transfer_in", result.InTransaction!.Type);
        Assert.Equal(TestBank.Saving, result.InTransaction.AccountId);
        Assert.Equal(13_580_000, result.InTransaction.BalanceAfter);
    }

    [Fact(DisplayName = "Chuyển ra ngân hàng khác: chỉ trừ tài khoản nguồn, ghi 1 giao dịch")]
    public void External_Valid_OnlyDebitsSource()
    {
        var before = _bank.TransactionCount();

        var result = _bank.Service.Transfer(External(TestBank.Payment, 500_000, "Tra tien"));

        Assert.True(result.Ok, result.Error);
        Assert.Equal(2_750_000, _bank.Balance(TestBank.Payment));
        Assert.Equal(12_580_000, _bank.Balance(TestBank.Saving));
        Assert.Equal(before + 1, _bank.TransactionCount());
        Assert.Null(result.Value!.InTransaction);
        Assert.Equal("Chuyển tiền đến NGUYEN VAN A", result.Value.OutTransaction.Title);
        Assert.Contains("Vietcombank", result.Value.OutTransaction.Subtitle);
        Assert.Contains("Tra tien", result.Value.OutTransaction.Subtitle);
    }

    [Fact(DisplayName = "Nhập STK là tài khoản của mình thì tự xử lý như chuyển nội bộ")]
    public void External_ToOwnAccountNumber_IsTreatedAsInternal()
    {
        var req = new TransferRequest(TestBank.Payment, null, "0123 4567 8888", null, null, 1_000_000, null);

        var result = _bank.Service.Transfer(req);

        Assert.True(result.Ok, result.Error);
        Assert.Equal(13_580_000, _bank.Balance(TestBank.Saving));
        Assert.NotNull(result.Value!.InTransaction);
    }

    // ---------------- Số dư âm / hạn mức thấu chi ----------------

    [Fact(DisplayName = "Thanh toán: chuyển trong hạn mức thấu chi thì cho phép số dư âm")]
    public void Payment_TransferWithinOverdraft_AllowsNegativeBalance()
    {
        var result = _bank.Service.Transfer(External(TestBank.Payment, 5_000_000));

        Assert.True(result.Ok, result.Error);
        Assert.True(result.Value!.UsesOverdraft);
        Assert.Equal(-1_750_000, _bank.Balance(TestBank.Payment));
    }

    [Fact(DisplayName = "Thanh toán: chuyển vượt hạn mức thấu chi bị chặn, số dư và lịch sử giữ nguyên")]
    public void Payment_TransferOverOverdraft_IsRejected()
    {
        var before = _bank.TransactionCount();

        var result = _bank.Service.Transfer(External(TestBank.Payment, 13_250_001));

        Assert.False(result.Ok);
        Assert.Equal("Số tiền chuyển vượt quá hạn mức khả dụng.", result.Error);
        Assert.Equal(3_250_000, _bank.Balance(TestBank.Payment));
        Assert.Equal(before, _bank.TransactionCount());
    }

    [Fact(DisplayName = "Thanh toán: chuyển đúng bằng số dư + hạn mức thì hợp lệ")]
    public void Payment_TransferExactlyAvailable_IsAllowed()
    {
        var result = _bank.Service.Transfer(External(TestBank.Payment, 13_250_000));

        Assert.True(result.Ok, result.Error);
        Assert.Equal(-10_000_000, _bank.Balance(TestBank.Payment));
    }

    [Fact(DisplayName = "Chuyển nội bộ vượt hạn mức: cả 2 tài khoản đều không đổi")]
    public void Internal_OverOverdraft_ChangesNothing()
    {
        var result = _bank.Service.Transfer(Internal(TestBank.Payment, TestBank.Saving, 20_000_000));

        Assert.False(result.Ok);
        Assert.Equal(3_250_000, _bank.Balance(TestBank.Payment));
        Assert.Equal(12_580_000, _bank.Balance(TestBank.Saving));
    }

    [Fact(DisplayName = "Tiết kiệm: chuyển vượt số dư bị chặn, số dư không bị âm")]
    public void Saving_TransferMoreThanBalance_IsRejected()
    {
        var result = _bank.Service.Transfer(Internal(TestBank.Saving, TestBank.Payment, 12_580_001));

        Assert.False(result.Ok);
        Assert.StartsWith("Số dư không đủ", result.Error);
        Assert.Equal(12_580_000, _bank.Balance(TestBank.Saving));
        Assert.Equal(3_250_000, _bank.Balance(TestBank.Payment));
    }

    [Fact(DisplayName = "Tiết kiệm: chuyển hết số dư thì về 0, không âm")]
    public void Saving_TransferWholeBalance_LeavesZero()
    {
        var result = _bank.Service.Transfer(Internal(TestBank.Saving, TestBank.Payment, 12_580_000));

        Assert.True(result.Ok, result.Error);
        Assert.Equal(0, _bank.Balance(TestBank.Saving));
        Assert.Equal(15_830_000, _bank.Balance(TestBank.Payment));
    }

    // ---------------- Dữ liệu không hợp lệ ----------------

    [Theory(DisplayName = "Số tiền chuyển không hợp lệ bị chặn (0, âm, dưới 10.000, trên 1 tỷ)")]
    [InlineData(0L)]
    [InlineData(-100_000L)]
    [InlineData(9_999L)]
    [InlineData(1_000_000_001L)]
    public void Transfer_InvalidAmount_IsRejected(long amount)
    {
        var result = _bank.Service.Transfer(External(TestBank.Payment, amount));

        Assert.False(result.Ok);
        Assert.Equal(3_250_000, _bank.Balance(TestBank.Payment));
    }

    [Fact(DisplayName = "Không được chuyển vào chính tài khoản nguồn")]
    public void Transfer_ToSameAccount_IsRejected()
    {
        var byId = _bank.Service.Transfer(Internal(TestBank.Payment, TestBank.Payment, 100_000));
        var byNumber = _bank.Service.Transfer(new TransferRequest(TestBank.Payment, null, "012345679999", null, null, 100_000, null));

        Assert.False(byId.Ok);
        Assert.False(byNumber.Ok);
        Assert.Equal("Không thể chuyển tiền vào chính tài khoản nguồn.", byId.Error);
        Assert.Equal(3_250_000, _bank.Balance(TestBank.Payment));
    }

    [Theory(DisplayName = "Chuyển ra ngoài thiếu thông tin người nhận bị chặn")]
    [InlineData("12345", "NGUYEN VAN A", "ACB")]        // STK quá ngắn
    [InlineData("0987654321", "", "ACB")]               // thiếu tên
    [InlineData("0987654321", "NGUYEN VAN A", "")]      // thiếu ngân hàng
    public void External_MissingRecipientInfo_IsRejected(string number, string name, string bank)
    {
        var result = _bank.Service.Transfer(new TransferRequest(TestBank.Payment, null, number, name, bank, 100_000, null));

        Assert.False(result.Ok);
        Assert.Equal(3_250_000, _bank.Balance(TestBank.Payment));
    }

    [Fact(DisplayName = "Nội dung chuyển khoản quá 100 ký tự bị chặn")]
    public void Transfer_NoteTooLong_IsRejected()
    {
        var result = _bank.Service.Transfer(External(TestBank.Payment, 100_000, new string('a', 101)));

        Assert.False(result.Ok);
    }

    [Fact(DisplayName = "Tài khoản nguồn hoặc nhận không tồn tại trả về NotFound")]
    public void Transfer_UnknownAccount_ReturnsNotFound()
    {
        var unknownFrom = _bank.Service.Transfer(External("khong-co", 100_000));
        var unknownTo = _bank.Service.Transfer(Internal(TestBank.Payment, "khong-co", 100_000));

        Assert.True(unknownFrom.NotFound);
        Assert.True(unknownTo.NotFound);
        Assert.Equal(3_250_000, _bank.Balance(TestBank.Payment));
    }

    // ---------------- Lịch sử giao dịch ----------------

    [Fact(DisplayName = "Sau nhiều giao dịch, số dư sau giao dịch của từng tài khoản vẫn khớp, không cộng lẫn")]
    public void History_BalanceAfter_IsConsistentPerAccount()
    {
        _bank.Service.Deposit(TestBank.Saving, 2_000_000);
        _bank.Service.Transfer(Internal(TestBank.Payment, TestBank.Saving, 1_000_000));
        _bank.Service.Transfer(External(TestBank.Payment, 5_000_000));
        _bank.Service.Withdraw(TestBank.Saving, 300_000);
        _bank.Service.Transfer(Internal(TestBank.Saving, TestBank.Payment, 700_000));

        foreach (var acc in _bank.Service.GetAccounts())
        {
            var list = _bank.Service.GetTransactions(acc.Id, null); // mới nhất trước
            Assert.Equal(acc.Balance, list[0].BalanceAfter);
            for (var i = 0; i < list.Count - 1; i++)
            {
                Assert.Equal(list[i + 1].BalanceAfter, list[i].BalanceAfter - list[i].SignedAmount);
            }
        }
    }

    [Fact(DisplayName = "Giao dịch chuyển tiền mới nhất nằm đầu lịch sử (hiển thị trên Trang chủ)")]
    public void History_NewTransferAppearsFirst()
    {
        var result = _bank.Service.Transfer(External(TestBank.Payment, 200_000)).Value!;

        var recent = _bank.Service.GetTransactions(null, 4);
        Assert.Equal(result.OutTransaction.Id, recent[0].Id);
        Assert.Equal(-200_000, recent[0].SignedAmount);
    }

    [Fact(DisplayName = "Giao dịch được lưu vào file: mở lại dữ liệu vẫn còn số dư và lịch sử")]
    public void History_IsSavedToFile()
    {
        var outId = _bank.Service.Transfer(Internal(TestBank.Payment, TestBank.Saving, 1_000_000)).Value!.OutTransaction.Id;

        // Tạo service mới đọc lại cùng file (giống như tắt server rồi chạy lại)
        var reopened = new BankService(new BankRepository(_bank.FilePath, NullLogger<BankRepository>.Instance));

        Assert.Equal(2_250_000, reopened.GetAccount(TestBank.Payment)!.Balance);
        Assert.Equal(13_580_000, reopened.GetAccount(TestBank.Saving)!.Balance);
        Assert.Contains(reopened.GetTransactions(null, null), t => t.Id == outId);
    }
}
