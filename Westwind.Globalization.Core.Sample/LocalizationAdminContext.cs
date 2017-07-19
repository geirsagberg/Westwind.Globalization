using Microsoft.EntityFrameworkCore;

namespace Westwind.Globalization.Core.Sample
{
    public class LocalizationAdminContext : DbContext
    {
        public LocalizationAdminContext(DbContextOptions options) : base(options)
        {
        }
    }
}