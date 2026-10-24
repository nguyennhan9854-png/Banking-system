using Microsoft.AspNetCore.Mvc;
using NganHang.Models;
using NganHang.Services;

namespace NganHang.Controllers;

/// <summary>Báo cáo thống kê giao dịch theo kỳ (tháng / quý / ngày).</summary>
[ApiController]
[Route("api/statistics")]
public class StatisticsController : ControllerBase
{
    private readonly BankService _bank;

    public StatisticsController(BankService bank) => _bank = bank;

    /// <summary>
    /// GET /api/statistics?from=2026-01-01&amp;to=2026-10-24&amp;groupBy=month&amp;accountId=acc-saving
    /// groupBy: month | quarter | day. Bỏ trống accountId để thống kê tất cả tài khoản.
    /// </summary>
    [HttpGet]
    public ActionResult<StatisticsDto> Get(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] string groupBy = "month",
        [FromQuery] string? accountId = null)
    {
        var result = _bank.GetStatistics(from, to, groupBy, accountId);
        if (result.Ok) return result.Value!;
        if (result.NotFound) return NotFound(new ErrorDto(result.Error!));
        return BadRequest(new ErrorDto(result.Error!));
    }
}
