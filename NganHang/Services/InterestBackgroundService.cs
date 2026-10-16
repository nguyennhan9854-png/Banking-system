namespace NganHang.Services;

/// <summary>
/// Dịch vụ chạy nền: khi server khởi động và sau mỗi giờ sẽ kiểm tra
/// các tài khoản tiết kiệm đã đến kỳ (tròn 1 tháng từ lần cộng lãi trước) để TỰ ĐỘNG cộng lãi.
/// Không cần ai bấm nút; mỗi kỳ chỉ cộng một lần.
/// </summary>
public class InterestBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    private readonly BankService _bank;
    private readonly ILogger<InterestBackgroundService> _logger;

    public InterestBackgroundService(BankService bank, ILogger<InterestBackgroundService> logger)
    {
        _bank = bank;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield(); // không chặn lúc server đang khởi động
        using var timer = new PeriodicTimer(Interval);
        try
        {
            do
            {
                RunOnce();
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // server đang tắt
        }
    }

    private void RunOnce()
    {
        try
        {
            var result = _bank.ApplyDueInterest();
            if (result.Transactions.Count > 0)
            {
                _logger.LogInformation("Tự động cộng lãi {Count} kỳ, tổng {Amount}",
                    result.Transactions.Count, BankService.Vnd(result.TotalAmount));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tự động cộng lãi");
        }
    }
}
