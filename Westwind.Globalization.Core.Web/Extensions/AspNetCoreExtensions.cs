using System.IO;
using Microsoft.AspNetCore.Hosting;

namespace Westwind.Globalization.Core.Web.Extensions
{
    public static class AspNetCoreExtensions
    {
        public static string MapPath(this IHostingEnvironment environment, string outputBasePath)
        {
            if (outputBasePath.StartsWith("~"))
                outputBasePath = outputBasePath.Substring(1);
            if (outputBasePath.StartsWith("/") || outputBasePath.StartsWith("\\"))
                outputBasePath = outputBasePath.Substring(1);
            return Path.Combine(environment.ContentRootPath, outputBasePath);
        }
    }
}