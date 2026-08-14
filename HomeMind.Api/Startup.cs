using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using HomeMind.Api.Services;
using HomeMind.Business.Services;
using HomeMind.Common.Infrastructure;
using HomeMind.Common.Model.ViewModel.Common;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace HomeMind.Api
{
    public class Startup
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public Startup(IConfiguration configuration, IWebHostEnvironment environment)
        {
            _configuration = configuration;
            _environment = environment;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            TokenService.ValidateConfiguration(_configuration, !_environment.IsDevelopment());

            // 开发阶段允许任意前端端口访问，便于本地联调。
            services.AddCors(options => options.AddPolicy("Frontend", policy => policy
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod()));

            // 生产环境改回以下白名单策略，并在 appsettings 中维护允许的前端域名。
            // var allowedOrigins = _configuration.GetSection("Cors:AllowedOrigins")
            //     .GetChildren()
            //     .Select(section => section.Value)
            //     .Where(origin => !string.IsNullOrWhiteSpace(origin))
            //     .Select(origin => origin!)
            //     .ToArray();
            // services.AddCors(options => options.AddPolicy("Frontend", policy => policy
            //     .WithOrigins(allowedOrigins)
            //     .AllowAnyHeader()
            //     .AllowAnyMethod()));

            services.AddHomeMindData(_configuration);
            services.AddHomeMindBusinessServices();
            services.AddHostedService<AutomationWorker>();
            services.AddHostedService<HomeAssistantEventWorker>();
            services.AddHostedService<AgentRuntimeWorker>();
            services.AddHostedService<ClippingPipelineWorker>();
            services.AddHostedService<MemoryReviewWorker>();
            services.AddSingleton<TokenService>();
            services.AddSingleton<HomeMind.Common.Infrastructure.SecretProtector>();
            services.AddScoped<AccessTokenValidator>();
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
            services.AddControllers()
                .AddJsonOptions(o =>
                {
                    // 入参：JSON body 走小驼峰。
                    o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                })
                .AddMvcOptions(o =>
                {
                    // 出参：ApiResponse<T> 的 Data 字段强制使用 C# PascalCase（大驼峰）输出。
                    o.OutputFormatters.Insert(0, new PascalCaseApiResponseOutputFormatter());
                });
            services.Configure<ApiBehaviorOptions>(options =>
                options.InvalidModelStateResponseFactory = _ =>
                    new BadRequestObjectResult(ApiResponse<object>.Fail(ApiErrorCodes.ValidationFailed, "请求参数格式错误。")));
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "HomeMind 接口",
                    Version = "v1",
                    Description = "HomeMind 客户端接口文档，按基础设置、智能能力和效率工具分类。"
                });
                options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "HomeMind.Api.xml"));
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "请输入登录或刷新令牌接口返回的访问令牌。",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "HomeMind访问令牌"
                });
                options.OperationFilter<AuthorizeOperationFilter>();
                options.TagActionsBy(api => new[]
                {
                    api.ActionDescriptor.RouteValues["controller"] switch
                    {
                        "Auth" => "基础设置 / 身份认证",
                        "Experts" => "智能能力 / 专家与运行",
                        "ExpertFiles" => "智能能力 / 专家与运行",
                        "TeamRuns" => "智能能力 / 专家与运行",
                        "HousekeeperRuns" => "智能能力 / 专家与运行",
                        "Skills" => "智能能力 / 技能",
                        "AiConfig" => "智能能力 / AI 配置",
                        "SmartHome" => "智能家居 / 家庭空间",
                        "Scenario" => "智能家居 / 场景工作流",
                        "Connectors" => "智能家居 / 连接器管理",
                        "Family" => "家庭上下文 / 成员与知识",
                        "Steward" => "家庭上下文 / 管家协同",
                        "TenantMembers" => "家庭上下文 / 成员受控管理",
                        "TenantMemberInvitations" => "家庭上下文 / 成员邀请",
                        "TenantMemberInvitationAccept" => "家庭上下文 / 成员邀请",
                        "WebNavigation" => "Web / 导航偏好",
                        "Calendar" => "效率工具 / 日历",
                        "Todos" => "效率工具 / 待办",
                        "Life" => "个人生活 / 偏好收藏",
                        "LifeRuns" => "个人生活 / 专家运行",
                        "Conversations" => "智能能力 / 专家与运行",
                        "Memories" => "家庭上下文 / 学习记忆",
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
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "HomeMind 接口 v1");
                options.EnablePersistAuthorization();
            });

            app.UseRouting();
            app.UseCors("Frontend");
            app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
            {
                var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
                var isDatabaseError = error is MySqlConnector.MySqlException;
                context.Response.StatusCode = isDatabaseError ? 503 : 500;
                context.Response.ContentType = "application/json; charset=utf-8";
                await JsonSerializer.SerializeAsync(
                    context.Response.Body,
                    ApiResponse<object>.Fail(
                        isDatabaseError ? ApiErrorCodes.DependencyUnavailable : ApiErrorCodes.InternalError,
                        isDatabaseError ? "数据库服务暂时不可用。" : "服务器发生未预期错误。"),
                    new JsonSerializerOptions { PropertyNamingPolicy = null });
            }));
            app.UseStatusCodePages(async statusCodeContext =>
            {
                var response = statusCodeContext.HttpContext.Response;
                var message = response.StatusCode switch
                {
                    400 => "请求参数错误。",
                    401 => "未提供访问令牌，或访问令牌无效、过期或已失效。",
                    403 => "当前账号没有执行此操作的权限。",
                    404 => "请求的资源不存在。",
                    405 => "请求方法不被允许。",
                    _ => "请求处理失败。"
                };
                response.ContentType = "application/json; charset=utf-8";
                await JsonSerializer.SerializeAsync(
                    response.Body,
                    ApiResponse<object>.Fail(ApiErrorCodes.FromHttpStatus(response.StatusCode), message),
                    new JsonSerializerOptions { PropertyNamingPolicy = null });
            });
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
