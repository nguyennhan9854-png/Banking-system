using Microsoft.AspNetCore.Mvc;
using NganHang.Models;
using NganHang.Services;

namespace NganHang.Controllers;

/// <summary>Chuyển tiền (nội bộ giữa tài khoản của mình hoặc tới người nhận khác).</summary>
[ApiController]
[Route("api/transfers")]
public class TransfersController : ControllerBase
{
    private readonly BankService _bank;

    public TransfersController(BankService bank) => _bank = bank;

    /// <summary>
    /// POST /api/transfers
    /// Nội bộ:  { "fromAccountId": "acc-payment", "toAccountId": "acc-saving", "amount": 1000000, "note": "Gửi tiết kiệm" }
    /// Ra ngoài: { "fromAccountId": "acc-payment", "toAccountNumber": "0987654321", "toName": "NGUYEN VAN A",
    ///            "toBank": "Vietcombank", "amount": 500000, "note": "Tra tien" }
    /// </summary>
    [HttpPost]
    public ActionResult<TransferResult> Create([FromBody] TransferRequest req)
    {
        var result = _bank.Transfer(req);
        if (result.Ok) return result.Value!;
        if (result.NotFound) return NotFound(new ErrorDto(result.Error!));
        return BadRequest(new ErrorDto(result.Error!));
    }
}
