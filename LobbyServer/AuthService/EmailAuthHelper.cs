using LobbyAPI;
using MailKit.Security;
using MimeKit;
using System.Text.Json;
using static LobbyServer.AuthService.IEmailAuthHelper;

namespace LobbyServer.AuthService
{
    public interface IEmailAuthHelper
    {
        public enum Result
        {
            Success,
            Fail,
            Deleted
        }
        Task<bool> RequestEmailVerificationAsync(EmailAuthDTO dto);
        Task<Result> VerifyEmailTokenAsync(string email, string token);
        Task SendEmailViaSmtpAsync(string toEmail, string authCode);
    }

    public class EmailAuthHelper : IEmailAuthHelper
    {
        private readonly IConfiguration _config;
        private readonly IRedisHelper _redisHelper;
        private const string QueueKey = "EmailQueue";
        private const int EmailTokenExpiryMinutes = 5;
        private const int EmailTokenRetryCount = 3;

        public EmailAuthHelper(IConfiguration config, IRedisHelper redis)
        {
            _config = config;
            _redisHelper = redis;
        }

        public async Task<bool> RequestEmailVerificationAsync(EmailAuthDTO dto)
        {
            if (!System.Net.Mail.MailAddress.TryCreate(dto.Email, out _))
            {
                // 올바른 이메일 형식이 아니면 큐나 Redis에 넣지 않고 즉시 거절!
                return false;
            }

            string redisKey = $"email:authtoken:{dto.Email}";
            var authData = new Dictionary<string, string>
            {
            { "Email", dto.Email },
            { "Token", dto.AuthToken },
            { "RetryCount", dto.RetryCount.ToString() } // 초기 재시도 횟수
            };

            await _redisHelper.SetHashFieldsAsync(redisKey, authData, expiry: TimeSpan.FromMinutes(EmailTokenExpiryMinutes));

            var jsonMessage = JsonSerializer.Serialize(dto);
            await _redisHelper.EnqueueKeyValueAsync(QueueKey, jsonMessage);

            return true;
        }

        public async Task<Result> VerifyEmailTokenAsync(string email, string token)
        {
            string redisKey = $"email:authtoken:{email}";

            var hashData = await _redisHelper.GetAllHashFieldsAsync(redisKey);

            // 2. 데이터가 없으면 null 반환
            if (hashData == null || hashData.Count == 0)
            {
                return Result.Fail;
            }

            string savedToken = hashData.TryGetValue("Token", out var outputToken) ? outputToken : string.Empty;
            int savedRetryCount = hashData.TryGetValue("RetryCount", out var outputRetryCount) && int.TryParse(outputRetryCount, out int count) ? count : EmailTokenRetryCount;

            if (savedToken != token)
            {
                if (savedRetryCount >= EmailTokenRetryCount)
                {
                    await _redisHelper.RemoveDataByKeyAsync(redisKey);
                    return Result.Deleted;
                }

                await _redisHelper.IncrementHashFieldAsync(redisKey, "RetryCount", 1);
                return Result.Fail;
            }

            return Result.Success;
        }

        public async Task SendEmailViaSmtpAsync(string toEmail, string authCode)
        {
            // 1. 메일 내용(MimeMessage) 만들기
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(
                _config["EmailSettings:SenderName"],
                _config["EmailSettings:SenderEmail"]));
            message.To.Add(new MailboxAddress("", toEmail));
            message.Subject = "[게임서버] 회원가입 이메일 인증 번호입니다.";

            // HTML 형식으로 본문 꾸미기
            message.Body = new TextPart("html")
            {
                Text = $@"
                <h3>회원가입 인증 번호</h3>
                <p>요청하신 인증 번호는 <strong>{authCode}</strong> 입니다.</p>
                <p>{EmailTokenExpiryMinutes}분 안에 입력해 주세요.</p>"
            };

            // 2. MailKit을 이용해 SMTP 서버로 발송하기
            using var client = new MailKit.Net.Smtp.SmtpClient();
            try
            {
                // Gmail SMTP 서버 연결 (TLS 암호화 사용)
                await client.ConnectAsync(_config["EmailSettings:SmtpServer"], int.Parse(_config["EmailSettings:SmtpPort"]), SecureSocketOptions.StartTls);

                // Gmail 계정 인증
                await client.AuthenticateAsync(_config["EmailSettings:SenderEmail"], _config["EmailSettings:Password"]);

                // 메일 전송
                await client.SendAsync(message);
            }
            catch (Exception ex)
            {
                // 포트폴리오 포인트: 외부 네트워크 작업은 실패할 수 있으므로 반드시 예외 처리를 합니다.
                Console.WriteLine($"이메일 발송 실패: {ex.Message}");
                throw;
            }
            finally
            {
                // 연결 해제
                await client.DisconnectAsync(true);
            }
        }
    }

    public class EmailAuthDTO
    {
        public string Email { get; set; } = string.Empty;
        public string AuthToken { get; set; } = string.Empty;
        public int RetryCount { get; set; } = 0;
    }
}
