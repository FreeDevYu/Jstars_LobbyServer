using LobbyServer.Helper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LobbyServer
{
    public class RedisAuthorizeAttribute : Attribute, IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var request = context.HttpContext.Request;

            // 1. 헤더 추출 (대소문자 무시를 위해 TryGetValue 권장)
            request.Headers.TryGetValue("Authorization", out var authHeader);
            request.Headers.TryGetValue("UID", out var uidHeader);
            request.Headers.TryGetValue("DeviceID", out var deviceIdHeader);

            string token = authHeader.ToString().Replace("Bearer ", "").Trim();
            string uid = uidHeader.ToString();
            string deviceID = deviceIdHeader.ToString();

            // 2. 값 유무 확인 및 로그 (디버깅용)
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(uid))
            {
                // 로그를 남겨서 어떤 헤더가 누락되었는지 확인하세요.
                context.Result = new UnauthorizedObjectResult(new { Success = false, Message = $"인증 누락 (UID:{uid}, Token:{!string.IsNullOrEmpty(token)})" });
                return;
            }

            var authTokenHelper = context.HttpContext.RequestServices.GetRequiredService<IAuthTokenHelper>();
            var authToken = await authTokenHelper.GetTokenAsync(uid, token);

            // 3. 상세 비교 로그 (필요시)
            if (authToken == null)
            {
                context.Result = new UnauthorizedObjectResult(new { Success = false, Message = "세션이 만료되었습니다. (Redis에 토큰 없음)" });
                return;
            }

            if (authToken.DeviceId != deviceID)
            {
                context.Result = new UnauthorizedObjectResult(new
                {
                    Success = false,
                    Message = $"기기 정보 불일치. 클라:{deviceID} / 서버:{authToken.DeviceId}"
                });
                return;
            }

            await next();
        }
    }
}