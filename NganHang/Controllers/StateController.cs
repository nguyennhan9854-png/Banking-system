using Microsoft.AspNetCore.Mvc;
using NganHang.Models;
using NganHang.Services;

namespace NganHang.Controllers;

/// <summary>Dữ liệu tổng hợp cho giao diện và biểu đồ.</summary>
[ApiController]
[Route("api")]
public class StateController : ControllerBase
{
    private readonly BankService _bank;

    public StateController(BankService bank) => _bank = bank;

    /// <summary>GET /api/state – người dùng, tài khoản, giao dịch (kèm số dư sau GD), thông báo.</summary>
    [HttpGet("state")]
    public ActionResult<StateDto> GetState() => _bank.GetState();

    /// <summary>
    /// GET /api/balance-history?accountIds=acc-saving,acc-payment&amp;from=2026-05-01&amp;to=2026-10-01&amp;mode=monthly
    /// </summary>
    [HttpGet("balance-history")]
    public ActionResult<List<BalancePointDto>> GetBalanceHistory(
        [FromQuery] string accountIds,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] string mode = "monthly")
    {
        var ids = (accountIds ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        var result = _bank.GetBalanceHistory(ids, from, to, mode);
        if (result.Ok) return result.Value!;
        if (result.NotFound) return NotFound(new ErrorDto(result.Error!));
        return BadRequest(new ErrorDto(result.Error!));
    }
}
