using NorthrailCRM.Models;
using Microsoft.EntityFrameworkCore;

namespace NorthrailCRM.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Contact> Contacts => Set<Contact>();
}