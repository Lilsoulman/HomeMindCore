using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using HomeMind.Api.Services;
using System;
using System.IO;

namespace HomeMind.Api
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<MySqlConnectionFactory>();
            services.AddSingleton<TokenService>();
            services.AddSingleton<SecretProtector>();
            services.AddSingleton<AccessTokenValidator>();
            services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
            services.AddHttpClient();
            services.AddAuthentication(HomeMindAuthenticationDefaults.Scheme)
                .AddScheme<AuthenticationSchemeOptions, BearerAuthenticationHandler>(HomeMindAuthenticationDefaults.Scheme, _ => { });
            services.AddAuthorization(options =>
            {
                var authenticatedPolicy = new AuthorizationPolicyBuilder(HomeMindAuthenticationDefaults.Scheme)
                    .RequireAuthenticatedUser()
                    .Build();
                options.DefaultPolicy = authenticatedPolicy;
                options.FallbackPolicy = authenticatedPolicy;
                foreach (var permission in PermissionNames.All)
                {
                    options.AddPolicy(permission, policy => policy
                        .AddAuthenticationSchemes(HomeMindAuthenticationDefaults.Scheme)
                        .RequireAuthenticatedUser()
                        .AddRequirements(new PermissionRequirement(permission)));
                }
            });
            services.AddControllers();
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "HomeMind API",
                    Version = "v1",
                    Description = "HomeMind 客户端接口文档，按基础设置、AI 能力和效率工具分类。"
                });
                options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "HomeMind.Api.xml"));
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "Enter the access token returned by login or refresh. Do not include the Bearer prefix.",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "HomeMind access token"
                });
                options.OperationFilter<AuthorizeOperationFilter>();
                options.TagActionsBy(api => new[]
                {
                    api.ActionDescriptor.RouteValues["controller"] switch
                    {
                        "Auth" => "基础设置 / 身份认证",
                        "Experts" => "AI / 专家与运行",
                        "Skills" => "AI / 技能",
                        "Calendar" => "效率工具 / 日历",
                        "Todos" => "效率工具 / 待办",
                        _ => "未分类"
                    }
                });
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "HomeMind API v1");
                options.EnablePersistAuthorization();
            });

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", context =>
                {
                    context.Response.Redirect("/swagger/index.html");
                    return System.Threading.Tasks.Task.CompletedTask;
                }).AllowAnonymous();
                endpoints.MapControllers();
            });
        }
    }
}
