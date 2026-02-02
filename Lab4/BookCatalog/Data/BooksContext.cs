using Microsoft.EntityFrameworkCore;
using BookCatalog.Models;

namespace BookCatalog.Data
{
    public class BooksContext : DbContext
    {
        public DbSet<Book> Books { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=books.db");
        }
    }
}