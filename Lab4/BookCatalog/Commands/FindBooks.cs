using System;
using System.Linq;
using BookCatalog.Data;
using BookCatalog.Models;

namespace BookCatalog.Commands
{
    public static class FindBooks
    {
        public static void Execute(string query)
        {
            using (var context = new BooksContext())
            {
                var books = context.Books.Where(b => b.Title.Contains(query) || b.Author.Contains(query)).ToList();
                foreach (var book in books)
                {
                    Console.WriteLine($"{book.ISBN}: {book.Title} by {book.Author}");
                }
            }
        }
    }
}
