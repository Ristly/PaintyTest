using PaintyTest.Wrappers;

namespace PaintyTest.Services;

public interface ITokenService
{
    public string GenerateToken(int id);
    ResultWrapper<bool> ValidateToken(string token, out int accountId);
}
