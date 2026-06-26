using LobbyServer.Helper;
using LobbyServer.Models;
using LobbyServer.Repositories;
using MySqlConnector;
using System.Security.Cryptography;



namespace LobbyServer.Services
{
    public interface IAuthService
    {
        Task<UsingIDResponse> UsingIDCheckAsync(UsingIDRequest request);
        Task<EmailAuthResponse> EmailAuthAsync(EmailAuthRequest request);
        Task<RegistResponse> RegisterAsync(RegistRequest request);
        Task<LoginResponse> LoginAsync(LoginRequest request);
        Task<LogoutResponse> LogoutAsync(LogoutRequest request);
    }

    public class AuthService : IAuthService
    {
        private readonly IUserRespository _userRepository;
        private readonly IAuthTokenHelper _authTokenHelper;
        private readonly IPasswordHelper _passwordHelper;
        private readonly IEmailAuthHelper _emailAuthHelper;
        private readonly IUserHelper _userHelper;

        public AuthService(IUserRespository userRepository, IAuthTokenHelper authTokenHelper, IPasswordHelper passwordHelper, IEmailAuthHelper emailAuthHelper, IUserHelper userHelper)
        {
            _userRepository = userRepository;
            _authTokenHelper = authTokenHelper;
            _passwordHelper = passwordHelper;
            _emailAuthHelper = emailAuthHelper;
            _userHelper = userHelper;
        }

        public async Task<UsingIDResponse> UsingIDCheckAsync(UsingIDRequest request)
        {
            string id = request.ID;
            var existingUserByUserID = await _userRepository.GetUserByIDAsync(id);
            if (existingUserByUserID != null)
            {
                return new UsingIDResponse {Success = false };
            }

            return new UsingIDResponse { Success = true };
        }

        public async Task<EmailAuthResponse> EmailAuthAsync(EmailAuthRequest request)
        {
            string email = request.Email;
            bool success = false;

            string authToken = GenerateUniqueToken();
            var dto = new EmailAuthDTO
            {
                Email = email,
                AuthToken = authToken,
                RetryCount = 0,
            };

            success = await _emailAuthHelper.RequestEmailVerificationAsync(dto);

            return new EmailAuthResponse { Success = success };
        }

        public async Task<RegistResponse> RegisterAsync(RegistRequest request)
        {
            string id = request.ID;
            string password = request.Password;
            string email = request.Email;
            string emailAuthToken = request.EmailAuthToken;

            var existingUserByUserID = await _userRepository.GetUserByIDAsync(id);
            if (existingUserByUserID != null)
            {
                return new RegistResponse { State = RegistResponse.ResultState.UsingID };
            }

            //이메일토큰 확인
            var emailAuthResult = await _emailAuthHelper.VerifyEmailTokenAsync(email, emailAuthToken);
            if(emailAuthResult != IEmailAuthHelper.Result.Success)
            {
                return new RegistResponse
                {
                    State = RegistResponse.ResultState.InvalidEmailAuthToken,
                    EmailTokenDeleted = emailAuthResult == IEmailAuthHelper.Result.Deleted ? true : false
                };
            }
           
            // 이메일 중복 확인
        //   var existingUserByEmail = await _userRepository.GetByEmailAsync(email);
        //   if (existingUserByEmail != null)
        //   {
        //       return new RegistResponse { State = RegistResponse.ResultState.UsingEmail };
        //   }

            // 비밀번호 유효성 검사
            if (string.IsNullOrEmpty(password) || password.Length < 8)
            {
                return new RegistResponse { State = RegistResponse.ResultState.InvalidPassword };
            }

            try
            {
                // 비밀번호 해싱
                var (passwordHash, salt) = _passwordHelper.HashPassword(password);

                // 새 사용자 생성
                var newUser = new User
                {
                    ID = id,
                    Email = email,
                    PasswordHash = passwordHash,
                    Salt = salt,
                    NickName = "_" + id
                };

                // 데이터베이스에 저장
                long userId = await _userRepository.CreateIDAsync(newUser);
                if (userId <= 0)
                {
                    return new RegistResponse { State = RegistResponse.ResultState.UsingID };
                }

                await _userHelper.ApplyNewAccountRewardsAsync(userId);

                return new RegistResponse { State = RegistResponse.ResultState.Success };
            }
            catch (MySqlException ex) when (ex.Number == 1062) // Unique Constraint Violation Error Code = 1062
            {
                return new RegistResponse { State = RegistResponse.ResultState.UsingID };
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex, "사용자 등록 중 오류 발생. Username: {Username}", username);
                return new RegistResponse { State = RegistResponse.ResultState.Unknown };
            }
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            try
            {
                string id = request.ID;
                string password = request.Password;
                string devideID = request.DeviceID;

                // 사용자 조회
                var user = await _userRepository.GetUserByIDAsync(id);

                if (user == null)
                {
                    return new LoginResponse { State = LoginResponse.ResultState.InvalidID, Token = null };
                }

                // 계정 상태 확인
                if (user.Status != "active")
                {
                    return new LoginResponse { State = LoginResponse.ResultState.InvalidState, Token = null };
                }

                // 비밀번호 검증
                bool isPasswordValid = _passwordHelper.VerifyPassword(password, user.PasswordHash, user.Salt);

                if (!isPasswordValid)
                {
                    return new LoginResponse { State = LoginResponse.ResultState.InvalidPassowrd, Token = null };
                }

                // 마지막 로그인 시간 업데이트
                await _userRepository.UpdateLastLoginAsync(user.UID, DateTime.UtcNow);
               
                // 인증 토큰 생성
                var tokenString = GenerateUniqueToken();
                var token = await _authTokenHelper.CreateTokenAsync(user, devideID, tokenString);

                if (token == null)
                {
                    return new LoginResponse { State = LoginResponse.ResultState.InvalidToken, Token = null };
                }

                await _userHelper.SetUserData(user);

                return new LoginResponse { State = LoginResponse.ResultState.Success, User = UserSafeModel.FromUser(user), Token = token };
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex, "로그인 중 오류 발생. Username: {Username}", username);
                return new LoginResponse { State = LoginResponse.ResultState.Unknown, Token = null };
            }
        }

        public async Task<LogoutResponse> LogoutAsync(LogoutRequest request)
        {
            try
            {
                bool isLogout = await _authTokenHelper.RevokeTokenAsync(request.ID);
                return new LogoutResponse { Success = isLogout };
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex, "로그아웃 중 오류 발생. Token: {Token}", token);
                return new LogoutResponse { Success = false };
            }
        }

        private string GenerateUniqueToken()
        {
            // 128비트(16바이트) 랜덤 값 생성
            byte[] randomBytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }

            // Base64url 형식(URL 안전한 Base64)으로 인코딩
            string base64 = Convert.ToBase64String(randomBytes);
            string base64url = base64.Replace('+', '-').Replace('/', '_').Replace("=", "");

            return base64url;
        }
    }
}
