using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Westwind.Globalization.Core.DbResourceDataManager;
using Westwind.Globalization.Core.DbResourceDataManager.DbResourceDataManagers;
using Westwind.Globalization.Core.DbResourceManager;
using Westwind.Globalization.Core.DbResourceSupportClasses;
using Westwind.Globalization.Core.Web.Utilities;

namespace Westwind.Globalization.Core.Web.Administration
{
    public static class LocalizationServiceExtensions
    {
        public static IServiceCollection AddWestwindGlobalization(this IServiceCollection services, IConfiguration config)
        {
            var configurationSection = config.GetSection(nameof(DbResourceConfiguration));
            var dbResourceConfiguration = configurationSection.Get<DbResourceConfiguration>();
            DbRes.Initialize(dbResourceConfiguration);
            
            services.AddMvcCore().AddApplicationPart(Assembly.GetExecutingAssembly());
            services.TryAddSingleton<IDbResourceDataManager, DbResourceSqlServerDataManager>();
            services.TryAddSingleton<DbResourceConfiguration>(dbResourceConfiguration);
            services.TryAddSingleton(factory => factory.GetRequiredService<IOptions<DbResourceConfiguration>>().Value);
            services.TryAddSingleton<JavaScriptResourceHandler>();
            return services;
        }
    }
}