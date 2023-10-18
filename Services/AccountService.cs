using Microsoft.EntityFrameworkCore;
using PaintyTest.ApplicationContexts;
using PaintyTest.Models;
using PaintyTest.Wrappers;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace PaintyTest.Services
{
    public class AccountService : IAccountService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ITokenService _tokenService;
        public AccountService(ApplicationDbContext context, IConfiguration configuration,
            ITokenService tokenService, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _configuration = configuration;
            _tokenService = tokenService;
            _httpContextAccessor = httpContextAccessor;
        }



        public async Task<ResultWrapper<bool>> Register(AccountBio loginInfo)
        {
            #region Проверка данных
            if (loginInfo == null)
                throw new ArgumentException();

            if (string.IsNullOrWhiteSpace(loginInfo.Login) || string.IsNullOrWhiteSpace(loginInfo.Password))
                throw new ArgumentException();

            if (_context.Accounts.Any(x => x.Login.Equals(loginInfo.Login)))
                throw new ArgumentException();

            #endregion

            #region Хэширование пароля
            var passwordAsBytes = Encoding.ASCII.GetBytes(loginInfo.Password);

            var hashedPassword = await HMACSHA256.HashDataAsync(Encoding.ASCII.GetBytes(_configuration.GetValue<string>("SecretHSHKey")),
                new MemoryStream(passwordAsBytes));

            loginInfo.Password = Convert.ToHexString(hashedPassword);
            #endregion

            Account account = new()
            {
                Login = loginInfo.Login,
                Password = loginInfo.Password,
                Name = loginInfo.Name,
                Surname = loginInfo.Surname
            };
            _context.Accounts.Add(account);
            await _context.SaveChangesAsync();

            return new ResultWrapper<bool>()
            {
                Data = true,
                Status = System.Net.HttpStatusCode.OK,
            };
        }

        public async Task<ResultWrapper<string>> Authorization(LoginInfo loginInfo)
        {
            #region Получение хэша пароля
            var passwordAsBytes = Encoding.ASCII.GetBytes(loginInfo.Password);

            var hashedPassword = await HMACSHA256.HashDataAsync(Encoding.ASCII.GetBytes(_configuration.GetValue<string>("SecretHSHKey")),
                new MemoryStream(passwordAsBytes));

            var hashAsString = Convert.ToHexString(hashedPassword);
            #endregion

            if (_context.Accounts.Any(x => x.Login == loginInfo.Login && x.Password == hashAsString))
            {
                var token = _tokenService
                    .GenerateToken(_context.Accounts.FirstOrDefault(x => x.Login == loginInfo.Login && x.Password == hashAsString).AccountId);

                if (token is null)
                    throw new Exception();

                return new ResultWrapper<string>()
                {
                    Status = System.Net.HttpStatusCode.OK,
                    Data = token
                };

            }
            throw new UnauthorizedAccessException();
        }

        public async Task<ResultWrapper<bool>> EditPassword(string password, string oldPassword)
        {
            var selfId = _httpContextAccessor.HttpContext.Items["accountId"];
            if (selfId is null)
                throw new UnauthorizedAccessException();

            var account = await _context.Accounts.Include(x => x.Friends)
                .FirstOrDefaultAsync(x => x.AccountId == (int)selfId);

            if (account is null)
                throw new Exception();


            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(oldPassword))
                throw new ArgumentException();

            #region Проверка старого пароля
            var oldPasswordAsBytes = Encoding.ASCII.GetBytes(oldPassword);

            var oldHashedPassword = await HMACSHA256.HashDataAsync(Encoding.ASCII.GetBytes(_configuration.GetValue<string>("SecretHSHKey")),
                new MemoryStream(oldPasswordAsBytes));

            var hashAsString = Convert.ToHexString(oldHashedPassword);

            if(account.Password != hashAsString)
                throw new ArgumentException();
            #endregion


            #region Получение хэша нового пароля
            var passwordAsBytes = Encoding.ASCII.GetBytes(password);

            var hashedPassword = await HMACSHA256.HashDataAsync(Encoding.ASCII.GetBytes(_configuration.GetValue<string>("SecretHSHKey")),
                new MemoryStream(passwordAsBytes));

            hashAsString = Convert.ToHexString(hashedPassword);
            #endregion

            account.Password = hashAsString;
            _context.Update(account);
            await _context.SaveChangesAsync();
            return new ResultWrapper<bool>() 
            { 
                Data = true,
                Status = System.Net.HttpStatusCode.OK,
            };

        }

        public async Task<ResultWrapper<bool>> EditAccountInfo(string name, string surname)
        {
            var selfId = _httpContextAccessor.HttpContext.Items["accountId"];
            if (selfId is null)
                throw new UnauthorizedAccessException();

            var account = await _context.Accounts.Include(x => x.Friends)
                .FirstOrDefaultAsync(x => x.AccountId == (int)selfId);

            if (account is null)
                throw new Exception();

            #region Проверка данных

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(surname))
                throw new ArgumentException();
            #endregion

            account.Name = name;
            account.Surname = surname;
            _context.Update(account);
            await _context.SaveChangesAsync();
            return new ResultWrapper<bool>()
            {
                Data = true,
                Status = System.Net.HttpStatusCode.OK,
            };
        }

        public async Task<ResultWrapper<bool>> AddFriend(int friendId)
        {
            var selfId = _httpContextAccessor.HttpContext.Items["accountId"];
            if (selfId is null)
                throw new UnauthorizedAccessException();

            var account = await _context.Accounts.Include(x => x.Friends)
                .FirstOrDefaultAsync(x => x.AccountId == (int)selfId);

            if (account is null)
                throw new Exception();

            if (account.AccountId == friendId)
                throw new ArgumentException();

            if (account.Friends.Any(x => x.AccountId == friendId))
                throw new ArgumentException();

            var friend = await _context.Accounts.FirstOrDefaultAsync(x => x.AccountId == friendId);

            if (friend is null)
                throw new KeyNotFoundException();

            account.Friends.Add(friend);
            _context.Accounts.Update(account);
            await _context.SaveChangesAsync();
            return new ResultWrapper<bool>()
            {
                Data = true,
                Status = System.Net.HttpStatusCode.OK,
            };

        }

        public async Task<ResultWrapper<bool>> RemoveFriend(int friendId)
        {
            var selfId = _httpContextAccessor.HttpContext.Items["accountId"];

            if (selfId is null)
                throw new UnauthorizedAccessException();

            var account = await _context.Accounts.Include(x => x.Friends)
                .FirstOrDefaultAsync(x => x.AccountId == (int)selfId);

            if (account is null)
                throw new Exception();

            if (account.AccountId == friendId)
                throw new ArgumentException();

            if (!account.Friends.Any(x => x.AccountId == friendId))
                throw new KeyNotFoundException();

            var friend = await _context.Accounts.FirstOrDefaultAsync(x => x.AccountId == friendId);

            if (friend is null)
                throw new KeyNotFoundException();


            account.Friends.Remove(friend);
            _context.Accounts.Update(account);
            await _context.SaveChangesAsync();

            return new ResultWrapper<bool>()
            {
                Data = true,
                Status = System.Net.HttpStatusCode.OK,
            };

        }
    }
}
