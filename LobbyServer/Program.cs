using LobbyAPI;
using LobbyServer;
using LobbyServer.Repositories;
using LobbyServer.AuthService;
using MySqlConnector;
using SqlKata.Compilers;
using SqlKata.Execution;
using StackExchange.Redis;
using LobbyServer.BackgroundServices;
using LobbyServer.LobbyService;


var builder = WebApplication.CreateBuilder(args);


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
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddHostedService<AuthEmailWorker>();


builder.Services.AddScoped<ICharacterHelper, CharacterHelper>();
builder.Services.AddScoped<IGearHelper, GearHelper>();
builder.Services.AddScoped<ILobbyService, LobbyService>();


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerConfiguration();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();