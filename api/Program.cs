var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var corsOrigins = builder.Configuration["Cors:SpaOrigins"]?
    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Spa", policy =>
    {
        if (corsOrigins.Length > 0)
        {
            policy.WithOrigins(corsOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
        else
        {
            policy.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

var app = builder.Build();

app.UseCors("Spa");
app.MapControllers();

app.Run();
