using Microsoft.Extensions.Logging.Abstractions;
using NganHang.Data;
using NganHang.Services;

namespace NganHang.Tests;

/// <summary>
/// Tạo một "ngân hàng" riêng cho từng test: dữ liệu mẫu ghi vào file tạm, test xong thì xóa.
/// Dữ liệu mẫu ban đầu:
///   - acc-saving : Tài khoản tiết kiệm, số dư 12.580.000 đ, không có thấu chi
///   - acc-payment: Tài khoản thanh toán, số dư 3.250.000 đ, hạn mức thấu chi 10.000.000 đ
/// </summary>
public sealed class TestBank : IDisposable
{
    public const string Saving = "acc-saving";
    public const string Payment = "acc-payment";

    public string FilePath { get; }
    public BankRepository Repository { get; }
    public BankService Service { get; }

    /// <summary>Điểm cắm lỗi giả lập (dùng cho test rollback).</summary>
    public FakeFaults Faults { get; } = new();

    public TestBank()
    {
        FilePath = Path.Combine(Path.GetTempPath(), "nganhang-test-" + Guid.NewGuid().ToString("N") + ".json");
        Repository = new BankRepository(FilePath, NullLogger<BankRepository>.Instance, Faults);
        Service = new BankService(Repository, Faults);
    }

    /// <summary>Số dư hiện tại của tài khoản.</summary>
    public long Balance(string accountId) => Service.GetAccount(accountId)!.Balance;

    /// <summary>Tổng số giao dịch đang lưu.</summary>
    public int TransactionCount() => Service.GetTransactions(null, null).Count;

    /// <summary>Mở lại dữ liệu từ file như khi khởi động lại server (không có lỗi giả lập).</summary>
    public BankService Reopen() =>
        new(new BankRepository(FilePath, NullLogger<BankRepository>.Instance));

    public void Dispose()
    {
        foreach (var f in new[] { FilePath, FilePath + ".tmp" })
        {
            try { File.Delete(f); } catch { /* bỏ qua */ }
        }
        try
        {
            var folder = Path.GetDirectoryName(FilePath)!;
            foreach (var f in Directory.GetFiles(folder, Path.GetFileNameWithoutExtension(FilePath) + "*.corrupt-*"))
                File.Delete(f);
        }
        catch { /* bỏ qua */ }
    }
}

/// <summary>
/// Bản giả của IFaultInjector: khi thao tác đi qua điểm lỗi đã đăng ký thì ném IOException
/// (giống ổ đĩa bị lỗi / server sập giữa chừng).
/// </summary>
public sealed class FakeFaults : IFaultInjector
{
    private readonly object _lock = new();
    private readonly Dictionary<string, int> _remaining = new();

    /// <summary>Các điểm lỗi thao tác đã đi qua (để kiểm tra test thật sự chạm tới điểm lỗi).</summary>
    public List<string> Hits { get; } = new();

    /// <summary>Gây lỗi <paramref name="times"/> lần tiếp theo đi qua điểm lỗi.</summary>
    public void FailAt(string point, int times = 1)
    {
        lock (_lock) _remaining[point] = times;
    }

    /// <summary>Lần nào đi qua điểm lỗi cũng bị lỗi.</summary>
    public void FailAlways(string point) => FailAt(point, int.MaxValue);

    /// <summary>Hết lỗi (hệ thống chạy lại bình thường).</summary>
    public void Clear()
    {
        lock (_lock) _remaining.Clear();
    }

    public bool WasHit(string point)
    {
        lock (_lock) return Hits.Contains(point);
    }

    public void Hit(string point)
    {
        lock (_lock)
        {
            Hits.Add(point);
            if (!_remaining.TryGetValue(point, out var left) || left <= 0) return;
            if (left != int.MaxValue) _remaining[point] = left - 1;
        }
        throw new IOException("Lỗi giả lập tại " + point);
    }
}
