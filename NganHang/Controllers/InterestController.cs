using Microsoft.AspNetCore.Mvc;
using NganHang.Models;
using NganHang.Services;

namespace NganHang.Controllers;

/// <summary>Cộng lãi cho tài khoản tiết kiệm.</summary>
[ApiController]
[Route("api/interest")]
public class InterestController : ControllerBase
{
    private readonly BankService _bank;

    public InterestController(BankService bank) => _bank = bank;

    /// <summary>
    /// POST /api/interest/run – chạy ngay đợt cộng lãi tự động (giống dịch vụ chạy nền).
    /// Chỉ cộng các kỳ đã đến hạn; gọi nhiều lần không bị cộng trùng.
    /// </summary>
    [HttpPost("run")]
    public ActionResult<InterestRunResult> Run() => _bank.ApplyDueInterest();

    /// <summary>
    /// POST /api/interest/{accountId}/apply – nhận ngay tiền lãi tạm tính của một tài khoản tiết kiệm.
    /// </summary>
    [HttpPost("{accountId}/apply")]
    public ActionResult<InterestRunResult> Apply(string accountId)
    {
        var result = _bank.ApplyAccruedInterest(accountId);
        if (result.Ok) return result.Value!;
        if (result.NotFound) return NotFound(new ErrorDto(result.Error!));
        return BadRequest(new ErrorDto(result.Error!));
    }
}
