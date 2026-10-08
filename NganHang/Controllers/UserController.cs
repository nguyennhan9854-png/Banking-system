using Microsoft.AspNetCore.Mvc;
using NganHang.Models;
using NganHang.Services;

namespace NganHang.Controllers;

/// <summary>Người dùng, thông báo, cài đặt bảo mật và khôi phục dữ liệu mẫu.</summary>
[ApiController]
[Route("api")]
public class UserController : ControllerBase
{
    private readonly BankService _bank;

    public UserController(BankService bank) => _bank = bank;

    /// <summary>POST /api/notifications/{id}/read</summary>
    [HttpPost("notifications/{id}/read")]
    public IActionResult MarkRead(string id)
    {
        if (!_bank.MarkNotificationRead(id)) return NotFound(new ErrorDto("Không tìm thấy thông báo."));
        return NoContent();
    }

    /// <summary>POST /api/notifications/read-all</summary>
    [HttpPost("notifications/read-all")]
    public IActionResult MarkAllRead()
    {
        _bank.MarkAllNotificationsRead();
        return NoContent();
    }

    /// <summary>PUT /api/user/otp  body: { "enabled": true }</summary>
    [HttpPut("user/otp")]
    public ActionResult<UserProfile> SetOtp([FromBody] OtpRequest req) => _bank.SetOtp(req.Enabled);

    /// <summary>POST /api/system/reset – khôi phục dữ liệu mẫu</summary>
    [HttpPost("system/reset")]
    public IActionResult Reset()
    {
        _bank.Reset();
        return NoContent();
    }
}
