using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LobbyServer
{
    //  ID와 DeviceID 입력창을 모두 만들어주는 범용 필터로 이름과 내용 변경
    public class CustomHeaderFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            operation.Parameters ??= new List<IOpenApiParameter>();

            // [첫 번째 헤더 입력창]: 유저 ID
            // 주의: 미들웨어의 request.Headers["ID"] 와 이름을 똑같이 맞췄습니다.
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "ID",
                In = ParameterLocation.Header,
                Required = false, // 필수 여부 (테스트 편의를 위해 일단 false)
                Description = "유저 ID",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String }
            });

            // 💡 [두 번째 헤더 입력창]: DeviceID 추가!
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "DeviceID",
                In = ParameterLocation.Header,
                Required = false,
                Description = "디바이스 고유 ID (예: device_abc_01)",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String }
            });
        }
    }

    public static class SwaggerConfig
    {
        public static void AddSwaggerConfiguration(this IServiceCollection services)
        {
            services.AddSwaggerGen(c =>
            {
                // 이름이 바뀐 커스텀 필터 등록
                c.OperationFilter<CustomHeaderFilter>();

                // (아래 인증 설정은 완벽하므로 그대로 유지합니다)
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "여기에 'Bearer {토큰}' 형식으로 입력하세요.",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });

                c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", document)] = []
                });
            });
        }
    }
}