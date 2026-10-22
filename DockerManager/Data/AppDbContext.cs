using DockerManager.Models;
using Microsoft.EntityFrameworkCore;

namespace DockerManager.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
    public DbSet<LogCheckHistory> LogCheckHistories => Set<LogCheckHistory>();
}
