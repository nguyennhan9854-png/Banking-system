using Microsoft.AspNetCore.Mvc;
using NganHang.Models;
using NganHang.Services;

namespace NganHang.Controllers;

/// <summary>Tài khoản: xem, nạp tiền, rút tiền, mở tài khoản mới.</summary>
[ApiController]
[Route("api/accounts")]
public class AccountsController : ControllerBase
{
    private readonly BankService _bank;

    public AccountsController(BankService bank) => _bank = bank;

    /// <summary>GET /api/accounts</summary>
    [HttpGet]
    public ActionResult<List<AccountDto>> GetAll() => _bank.GetAccounts();

    /// <summary>GET /api/accounts/{id}</summary>
    [HttpGet("{id}")]
    public ActionResult<AccountDto> Get(string id)
    {
        var acc = _bank.GetAccount(id);
        if (acc == null) return NotFound(new ErrorDto("Không tìm thấy tài khoản."));
        return acc;
    }

    /// <summary>POST /api/accounts/{id}/deposit  body: { "amount": 1000000 }</summary>
    [HttpPost("{id}/deposit")]
    public ActionResult<DepositWithdrawResult> Deposit(string id, [FromBody] AmountRequest req)
        => ToResponse(_bank.Deposit(id, req.Amount));

    /// <summary>POST /api/accounts/{id}/withdraw  body: { "amount": 5000000 }</summary>
    [HttpPost("{id}/withdraw")]
    public ActionResult<DepositWithdrawResult> Withdraw(string id, [FromBody] AmountRequest req)
        => ToResponse(_bank.Withdraw(id, req.Amount));

    /// <summary>
    /// POST /api/accounts  body: { "type": "saving", "termMonths": 6, "initialAmount": 5000000, "overdraftLimit": 0 }
    /// </summary>
    [HttpPost]
    public ActionResult<AccountDto> Open([FromBody] OpenAccountRequest req)
    {
        var result = _bank.OpenAccount(req);
        if (!result.Ok) return BadRequest(new ErrorDto(result.Error!));
        return CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value);
    }

    private ActionResult<T> ToResponse<T>(ServiceResult<T> result)
    {
        if (result.Ok) return new ActionResult<T>(result.Value!);
        if (result.NotFound) return new ActionResult<T>(NotFound(new ErrorDto(result.Error!)));
        return new ActionResult<T>(BadRequest(new ErrorDto(result.Error!)));
    }
}
