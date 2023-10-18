using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PaintyTest.Models;
using PaintyTest.Services;
using PaintyTest.Wrappers;

namespace PaintyTest.Controllers;

[Route("api/[controller]")]
[ApiController]
public class RegisterController : ControllerBase
{
    private readonly IAccountService _accountService;
    public RegisterController(IAccountService accountService)
    {
        _accountService = accountService;
    }

    [HttpPost("registerAccount")]
    public async Task<IActionResult> RegisterAccount(Account account)
        => new JsonResult(await _accountService.Register(account));
}
