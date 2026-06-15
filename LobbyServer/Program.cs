using LobbyAPI;
using LobbyServer;
using LobbyServer.Repositories;
using LobbyServer.Services;
using MySqlConnector;
using SqlKata.Compilers;
using SqlKata.Execution;
using StackExchange.Redis;
using LobbyServer.BackgroundServices;
using LobbyServer.Hubs;
using LobbyServer.Helper;
using LobbyServer.Services;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options =>
{
    options.RespectBrowserAcceptHeader = true;
})
.AddProtoBufNet();

builder.Services.AddControllers();


string redisConnectionString = builder.Configuration["RedisConnection"] ?? "127.0.0.1:6379,abortConnect=false";
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnectionString));
builder.Services.AddSingleton<IRedisHelper, RedisHelper>();


string connectionString = builder.Configuration.GetConnectionString("DBConnection");
builder.Services.AddScoped<QueryFactory>(sp => {
    var connection = new MySqlConnection(connectionString);
    var compiler = new MySqlCompiler();
    return new QueryFactory(connection, compiler);
});

builder.Services.AddScoped<IUserRespository, UserRespository>();
builder.Services.AddScoped<IPasswordHelper, PasswordHelper>();
builder.Services.AddScoped<IAuthTokenHelper, AuthTokenHelper>();
builder.Services.AddScoped<IEmailAuthHelper, EmailAuthHelper>();
builder.Services.AddScoped<IUserHelper, UserHelper>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddHostedService<AuthEmailWorker>();


builder.Services.AddScoped<ILobbyRespository, LobbyRespository>();
builder.Services.AddScoped<ICharacterHelper, CharacterHelper>();
builder.Services.AddScoped<IInventoryHelper, InventoryHelper>();
builder.Services.AddScoped<IMatchingHelper, MatchingHelper>();
builder.Services.AddScoped<IRankingHelper, RankingHelper>();
builder.Services.AddScoped<IRankingService, RankingService>();
builder.Services.AddScoped<ILobbyService, LobbyService>();
builder.Services.AddScoped<IShopRespository, ShopRespository>();
builder.Services.AddScoped<IShopService, ShopService>();
builder.Services.AddHostedService<MatchingRequestWorker>();
builder.Services.AddHostedService<MatchResponseWorker>();
builder.Services.AddHostedService<MatchResultWorker>();
builder.Services.AddHostedService<RankingRefreshWorker>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerConfiguration();



builder.Services.AddSignalR();

var app = builder.Build();

app.UseCors(policy => policy
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()
    .SetIsOriginAllowed(_ => true)); 

app.UseRouting();


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapHub<SignalRHub>("/signalRHub");
app.MapControllers();

app.Run();