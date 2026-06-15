using Humanizer;
using LobbyServer.Helper;
using Microsoft.Extensions.DependencyInjection; // IServiceScopeFactory를 위해 필요
using StackExchange.Redis;
using System.Text.Json;

namespace LobbyServer.BackgroundServices
{

    public class AuthEmailWorker : BackgroundService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IServiceScopeFactory _scopeFactory;
        private const string QueueKey = "EmailQueue";

        public AuthEmailWorker(IConnectionMultiplexer redis, IServiceScopeFactory scopeFactory)
        {
            _redis = redis;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var db = _redis.GetDatabase();

            // 서버가 종료 요청(stoppingToken)을 받을 때까지 무한 루프
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // 1. 큐의 왼쪽에서 데이터 꺼내기 (LPOP)
                    var redisValue = await db.ListLeftPopAsync(QueueKey);

                    if (redisValue.HasValue)
                    {
                        // 2. 데이터가 있으면 역직렬화 후 메일 발송
                        var message = JsonSerializer.Deserialize<EmailAuthDTO>(redisValue.ToString());

                        if (message != null)
                        {
                            using (var scope = _scopeFactory.CreateScope())
                            {
                                // 3. 생성된 scope 안에서 필요한 Scoped 서비스(Helper)를 꺼내옵니다.
                                var emailHelper = scope.ServiceProvider.GetRequiredService<IEmailAuthHelper>();

                                // 4. 꺼내온 Helper를 이용해 메일을 발송합니다.
                                await emailHelper.SendEmailViaSmtpAsync(message.Email, message.AuthToken);
                            }
                        }
                    }
                    else
                    {
                        await Task.Delay(1000, stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Worker Error: {ex.Message}");

                    // 필요하다면 여기서 메일을 다시 큐에 넣는 (Retry) 로직 추가 가능
                    await Task.Delay(2000, stoppingToken);
                }
            }
        }
    }
}
