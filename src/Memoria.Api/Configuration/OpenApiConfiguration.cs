using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;

namespace Memoria.Api.Configuration;

/// <summary>
/// Swagger / OpenAPI. UI монтируется только в Development — для прод-окружения
/// /swagger закрыт (см. README → Stage 13). Bearer security scheme регистрируется
/// всегда, чтобы spec был полным.
/// </summary>
internal static class OpenApiConfiguration
{
    public static IServiceCollection AddOpenApiServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "Memoria API", Version = "v1" });

            c.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "JWT access token, issued by /api/v1/auth/* endpoints.",
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = JwtBearerDefaults.AuthenticationScheme,
                        },
                    },
                    Array.Empty<string>()
                },
            });
        });

        return services;
    }

    public static WebApplication UseOpenApiUi(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Memoria API v1"));
        }
        return app;
    }
}
