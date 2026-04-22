using LobbyServer.AuthService;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LobbyServer
{
    public class RedisAuthorizeAttribute : Attribute, IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var request = context.HttpContext.Request;

            // 1. 헤더에서 토큰과 UID 추출
            // 서버 측 안전한 헤더 추출 예시
            string token = request.Headers["Authorization"].ToString()?.Replace("Bearer ", "").Trim() ?? string.Empty;
            string id = request.Headers["ID"].ToString();
            string deviceID = request.Headers["DeviceID"].ToString();

            // 2. 값이 없으면 컨트롤러 진입 차단 (401 리턴)
            if (string.IsNullOrEmpty(token))
            {
                context.Result = new UnauthorizedObjectResult(new { Success = false, Message = "인증 정보가 누락되었습니다." });
                return; // 여기서 리턴하면 컨트롤러로 안 넘어감!
            }
           
            // 3. DI 컨테이너에서 IAuthTokenHelper 가져오기
            var authTokenHelper = context.HttpContext.RequestServices.GetRequiredService<IAuthTokenHelper>();

            // 4. Redis 검증 로직 실행!
            var authToken = await authTokenHelper.GetTokenAsync(id, token);

            if (authToken == null || authToken.DeviceId != deviceID)
            {
                // 토큰이 틀렸거나, 만료됐거나, 다른 기기에서 로그인해서 밀려난 경우 차단
                context.Result = new UnauthorizedObjectResult(new { Success = false, Message = "유효하지 않거나 만료된 세션입니다." });
                return;
            }

            // 5. 검증 통과! 다음 단계(컨트롤러의 실제 함수)로 진입해라!
            await next();
        }
    }
}