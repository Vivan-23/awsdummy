using awsdummy.model;
using Microsoft.EntityFrameworkCore;

namespace awsdummy.Data
{
    public class DataContext : DbContext
    {
        public DataContext(DbContextOptions<DataContext> options) : base(options)
        {
        }
        public DbSet<tenant> SamlSettings { get; set; }
    }
}
