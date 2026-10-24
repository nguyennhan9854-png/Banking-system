using Microsoft.Extensions.Logging.Abstractions;
using NganHang.Data;
using NganHang.Models;
using NganHang.Services;

namespace NganHang.Tests;

/// <summary>
/// TEST BIÊN (Nhân – tuần 3): giá trị nhỏ nhất/lớn nhất, nhiều lệnh cùng lúc,
/// tài khoản bị khóa, số tài khoản có khoảng trắng / ký tự lạ, file dữ liệu hỏng khi khởi động.
/// </summary>
public class EdgeCaseTests : IDisposable
{
    private readonly TestBank _bank = new();
    public void Dispose() => _bank.Dispose();

    private static TransferRequest Internal(string from, string to, long amount, string? note = null) =>
        new(from, to, null, null, null, amount, note);

    private static TransferRequest External(string from, long amount, string number = "0987 654 321",
        string name = "NGUYEN VAN A", string bank = "Vietcombank", string? note = null) =>
        new(from, null, number, name, bank, amount, note);

    /// <summary>Số dư sau giao dịch của từng tài khoản phải nối tiếp nhau đúng.</summary>
    private void AssertHistoryConsistent(string accountId)
    {
        var list = _bank.Service.GetTransactions(accountId, null);
        Assert.Equal(_bank.Balance(accountId), list[0].BalanceAfter);
        for (var i = 0; i < list.Count - 1; i++)
        {
            var change = list[i].Status == "success" ? list[i].SignedAmount : 0;
            Assert.Equal(list[i + 1].BalanceAfter, list[i].BalanceAfter - change);
        }
    }

    // ================= GIÁ TRỊ BIÊN =================

    [Theory(DisplayName = "EC01 Chuyển đúng mức tối thiểu 10.000 đ (và 10.001 đ) là hợp lệ")]
    [InlineData(10_000L)]
    [InlineData(10_001L)]
    public void Transfer_AtMinimum_IsAllowed(long amount)
    {
        var result = _bank.Service.Transfer(External(TestBank.Payment, amount));

        Assert.True(result.Ok, result.Error);
        Assert.Equal(3_250_000 - amount, _bank.Balance(TestBank.Payment));
    }

    [Fact(DisplayName = "EC02 Chuyển đúng mức tối đa 1 tỷ đ là hợp lệ khi đủ tiền")]
    public void Transfer_AtMaximum_IsAllowed()
    {
        Assert.True(_bank.Service.Deposit(TestBank.Payment, 1_000_000_000).Ok);

        var result = _bank.Service.Transfer(Internal(TestBank.Payment, TestBank.Saving, 1_000_000_000));

        Assert.True(result.Ok, result.Error);
        Assert.Equal(3_250_000, _bank.Balance(TestBank.Payment));
        Assert.Equal(1_012_580_000, _bank.Balance(TestBank.Saving));
    }

    [Theory(DisplayName = "EC03 Nạp tiền: 1 đ và 1 tỷ đ hợp lệ, 1 tỷ + 1 đ và long.MaxValue bị chặn (không tràn số)")]
    [InlineData(1L, true)]
    [InlineData(1_000_000_000L, true)]
    [InlineData(1_000_000_001L, false)]
    [InlineData(long.MaxValue, false)]
    public void Deposit_Boundaries(long amount, bool ok)
    {
        var result = _bank.Service.Deposit(TestBank.Payment, amount);

        Assert.Equal(ok, result.Ok);
        Assert.Equal(ok ? 3_250_000 + amount : 3_250_000, _bank.Balance(TestBank.Payment));
    }

    [Fact(DisplayName = "EC04 Rút/chuyển long.MaxValue bị chặn, số dư không bị tràn số")]
    public void HugeAmounts_AreRejectedWithoutOverflow()
    {
        Assert.False(_bank.Service.Withdraw(TestBank.Payment, long.MaxValue).Ok);
        Assert.False(_bank.Service.Transfer(External(TestBank.Payment, long.MaxValue)).Ok);
        Assert.False(_bank.Service.Transfer(External(TestBank.Payment, long.MinValue)).Ok);
        Assert.Equal(3_250_000, _bank.Balance(TestBank.Payment));
    }

    [Fact(DisplayName = "EC05 Đã dùng hết hạn mức thấu chi (-10 triệu): chuyển thêm 10.000 đ bị chặn, nạp vào thì dùng lại được")]
    public void OverdraftFullyUsed_ThenDepositRestores()
    {
        Assert.True(_bank.Service.Withdraw(TestBank.Payment, 13_250_000).Ok);   // số dư = -10.000.000
        Assert.Equal(-10_000_000, _bank.Balance(TestBank.Payment));

        Assert.False(_bank.Service.Transfer(External(TestBank.Payment, 10_000)).Ok);

        Assert.True(_bank.Service.Deposit(TestBank.Payment, 10_000).Ok);
        Assert.True(_bank.Service.Transfer(External(TestBank.Payment, 10_000)).Ok);
        Assert.Equal(-10_000_000, _bank.Balance(TestBank.Payment));
    }

    [Fact(DisplayName = "EC06 Nội dung đúng 100 ký tự và tên người nhận đúng 50 ký tự là hợp lệ, hơn 1 ký tự bị chặn")]
    public void NoteAndName_AtLengthLimit()
    {
        Assert.True(_bank.Service.Transfer(External(TestBank.Payment, 50_000, note: new string('a', 100))).Ok);
        Assert.True(_bank.Service.Transfer(External(TestBank.Payment, 50_000, name: new string('B', 50))).Ok);
        Assert.False(_bank.Service.Transfer(External(TestBank.Payment, 50_000, name: new string('B', 51))).Ok);
    }

    [Fact(DisplayName = "EC07 Nội dung có ký tự đặc biệt/HTML được lưu nguyên văn (giao diện tự escape khi hiển thị)")]
    public void Note_WithSpecialCharacters_IsStoredAsIs()
    {
        const string note = "<b>Trả nợ</b> & \"tiền ăn\" 😀";

        var result = _bank.Service.Transfer(External(TestBank.Payment, 50_000, note: note));

        Assert.True(result.Ok, result.Error);
        Assert.EndsWith(note, result.Value!.OutTransaction.Subtitle);
    }

    // ================= NHIỀU LỆNH CÙNG LÚC =================

    [Fact(DisplayName = "EC08 40 lệnh chuyển 1 triệu cùng lúc từ TK thanh toán: đúng 13 lệnh thành công, không vượt hạn mức thấu chi")]
    public void ConcurrentTransfers_NeverExceedOverdraft()
    {
        var results = new ServiceResult<TransferResult>[40];
        Parallel.For(0, 40, i => results[i] = _bank.Service.Transfer(External(TestBank.Payment, 1_000_000)));

        Assert.Equal(13, results.Count(r => r.Ok));                    // khả dụng 3.250.000 + 10.000.000
        Assert.Equal(3_250_000 - 13_000_000, _bank.Balance(TestBank.Payment));
        Assert.True(_bank.Balance(TestBank.Payment) >= -10_000_000);
        AssertHistoryConsistent(TestBank.Payment);
    }

    [Fact(DisplayName = "EC09 30 lệnh rút 500.000 cùng lúc từ TK tiết kiệm: đúng 25 lệnh thành công, số dư không âm")]
    public void ConcurrentWithdrawals_SavingNeverNegative()
    {
        var results = new ServiceResult<DepositWithdrawResult>[30];
        Parallel.For(0, 30, i => results[i] = _bank.Service.Withdraw(TestBank.Saving, 500_000));

        Assert.Equal(25, results.Count(r => r.Ok));                    // 12.580.000 / 500.000 = 25 lần
        Assert.Equal(80_000, _bank.Balance(TestBank.Saving));
        AssertHistoryConsistent(TestBank.Saving);
    }

    [Fact(DisplayName = "EC10 Chuyển qua lại 2 chiều cùng lúc giữa 2 tài khoản: tổng tiền không đổi, lịch sử khớp")]
    public void ConcurrentBothDirections_TotalConserved()
    {
        var total = _bank.Balance(TestBank.Payment) + _bank.Balance(TestBank.Saving);

        Parallel.For(0, 50, i =>
        {
            if (i % 2 == 0) _bank.Service.Transfer(Internal(TestBank.Payment, TestBank.Saving, 300_000));
            else _bank.Service.Transfer(Internal(TestBank.Saving, TestBank.Payment, 200_000));
        });

        Assert.Equal(total, _bank.Balance(TestBank.Payment) + _bank.Balance(TestBank.Saving));
        Assert.True(_bank.Balance(TestBank.Saving) >= 0);
        Assert.True(_bank.Balance(TestBank.Payment) >= -10_000_000);
        AssertHistoryConsistent(TestBank.Payment);
        AssertHistoryConsistent(TestBank.Saving);
    }

    // ================= TÀI KHOẢN BỊ KHÓA =================

    [Fact(DisplayName = "EC11 Tài khoản nguồn bị khóa: không chuyển được, số dư giữ nguyên")]
    public void LockedSource_CannotTransfer()
    {
        Assert.True(_bank.Service.SetLocked(TestBank.Payment, true).Ok);

        var result = _bank.Service.Transfer(Internal(TestBank.Payment, TestBank.Saving, 100_000));

        Assert.False(result.Ok);
        Assert.Contains("khóa", result.Error);
        Assert.Equal(3_250_000, _bank.Balance(TestBank.Payment));
        Assert.Equal(12_580_000, _bank.Balance(TestBank.Saving));
    }

    [Fact(DisplayName = "EC12 Tài khoản nhận bị khóa (nhập bằng STK): không chuyển được, không trừ tiền nguồn")]
    public void LockedRecipient_CannotReceive()
    {
        _bank.Service.SetLocked(TestBank.Saving, true);

        var result = _bank.Service.Transfer(External(TestBank.Payment, 100_000, number: "0123 4567 8888"));

        Assert.False(result.Ok);
        Assert.Contains("khóa", result.Error);
        Assert.Equal(3_250_000, _bank.Balance(TestBank.Payment));
    }

    [Fact(DisplayName = "EC13 Tài khoản bị khóa: nạp và rút đều bị chặn; mở khóa thì dùng lại bình thường")]
    public void LockedAccount_DepositWithdrawBlocked_UntilUnlocked()
    {
        _bank.Service.SetLocked(TestBank.Payment, true);
        Assert.Equal("locked", _bank.Service.GetAccount(TestBank.Payment)!.Status);

        Assert.False(_bank.Service.Deposit(TestBank.Payment, 100_000).Ok);
        Assert.False(_bank.Service.Withdraw(TestBank.Payment, 100_000).Ok);
        Assert.Equal(3_250_000, _bank.Balance(TestBank.Payment));

        Assert.True(_bank.Service.SetLocked(TestBank.Payment, false).Ok);
        Assert.True(_bank.Service.Deposit(TestBank.Payment, 100_000).Ok);
        Assert.Equal(3_350_000, _bank.Balance(TestBank.Payment));
    }

    [Fact(DisplayName = "EC14 Khóa tài khoản không tồn tại trả về NotFound")]
    public void LockUnknownAccount_NotFound()
    {
        var result = _bank.Service.SetLocked("acc-khong-co", true);

        Assert.False(result.Ok);
        Assert.True(result.NotFound);
    }

    // ================= SỐ TÀI KHOẢN NHẬP TAY =================

    [Theory(DisplayName = "EC15 STK có khoảng trắng, gạch, chấm vẫn nhận ra là tài khoản tiết kiệm của mình")]
    [InlineData("0123 4567 8888")]
    [InlineData("012345678888")]
    [InlineData("0123-4567-8888")]
    [InlineData("  0123.4567.8888  ")]
    public void AccountNumber_WithSeparators_IsRecognizedAsInternal(string number)
    {
        var result = _bank.Service.Transfer(External(TestBank.Payment, 500_000, number: number, name: "", bank: ""));

        Assert.True(result.Ok, result.Error);
        Assert.Equal(TestBank.Saving, result.Value!.To!.Id);
        Assert.Equal(13_080_000, _bank.Balance(TestBank.Saving));
    }

    [Theory(DisplayName = "EC16 STK rỗng, toàn chữ, quá ngắn hoặc quá dài bị chặn")]
    [InlineData("")]
    [InlineData("abcdefgh")]
    [InlineData("12 34")]
    [InlineData("12345678901234567890")]   // 20 chữ số
    public void AccountNumber_Invalid_IsRejected(string number)
    {
        var result = _bank.Service.Transfer(External(TestBank.Payment, 500_000, number: number));

        Assert.False(result.Ok);
        Assert.Contains("Số tài khoản", result.Error);
        Assert.Equal(3_250_000, _bank.Balance(TestBank.Payment));
    }

    // ================= FILE DỮ LIỆU KHI KHỞI ĐỘNG =================

    [Theory(DisplayName = "EC17 File dữ liệu hỏng khi khởi động: tạo lại dữ liệu mẫu và GIỮ bản sao file hỏng")]
    [InlineData("{ day khong phai json")]
    [InlineData("")]
    [InlineData("{\"version\":1,\"accounts\":[]}")]
    [InlineData("{\"version\":99,\"accounts\":[{\"id\":\"x\",\"type\":\"payment\"}]}")]
    public void CorruptFile_IsBackedUpAndReseeded(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), "nganhang-corrupt-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, content);
        string? backup = null;
        try
        {
            var repo = new BankRepository(path, NullLogger<BankRepository>.Instance);
            backup = repo.CorruptBackupPath;
            var service = new BankService(repo);

            Assert.NotNull(backup);
            Assert.Equal(content, File.ReadAllText(backup!));          // file hỏng vẫn còn để kiểm tra lại
            Assert.Equal(12_580_000, service.GetAccount(TestBank.Saving)!.Balance);
            Assert.Equal(2, new BankService(new BankRepository(path, NullLogger<BankRepository>.Instance)).GetAccounts().Count);
        }
        finally
        {
            try { File.Delete(path); } catch { /* bỏ qua */ }
            if (backup != null) try { File.Delete(backup); } catch { /* bỏ qua */ }
        }
    }

    [Fact(DisplayName = "EC18 File tạm .tmp còn sót do tắt máy ngang: khởi động vẫn đọc đúng file chính")]
    public void LeftoverTempFile_DoesNotBreakStartup()
    {
        Assert.True(_bank.Service.Deposit(TestBank.Payment, 750_000).Ok);
        File.WriteAllText(_bank.FilePath + ".tmp", "{ ghi do dang");

        var reopened = _bank.Reopen();

        Assert.Equal(4_000_000, reopened.GetAccount(TestBank.Payment)!.Balance);
        Assert.True(reopened.Deposit(TestBank.Payment, 1).Ok);         // lần lưu sau ghi đè file tạm cũ
    }

    [Fact(DisplayName = "EC19 File tốt thì không tạo bản sao file hỏng")]
    public void GoodFile_NoCorruptBackup()
    {
        var repo = new BankRepository(_bank.FilePath, NullLogger<BankRepository>.Instance);

        Assert.Null(repo.CorruptBackupPath);
    }
}
