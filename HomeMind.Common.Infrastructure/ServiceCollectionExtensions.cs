using HomeMind.Common.IRepository;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pomelo.EntityFrameworkCore.MySql;

namespace HomeMind.Common.Infrastructure;

/// <summary>注册数据库上下文、通用仓储和工作单元。</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHomeMindData(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("HomeMind")
            ?? throw new InvalidOperationException("缺少 HomeMind 数据库连接配置。");

        services.AddDbContext<HomeMindDbContext>(options => 
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), 
                b => b.MigrationsAssembly("HomeMind.Api")));
        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        return services;
    }
}
