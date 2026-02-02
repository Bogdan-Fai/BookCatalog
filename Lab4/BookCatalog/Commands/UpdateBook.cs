using System;
using System.Linq;
using BookCatalog.Data;
using BookCatalog.Models;

namespace BookCatalog.Commands
{
    public static class UpdateBook
    {
        public static void Execute(string isbn, string title, string author, string year, string pages)
        {
            using (var context = new BooksContext())
            {
                var book = context.Books.FirstOrDefault(b => b.ISBN == isbn);
                if (book != null)
                {
                    book.Title = title;
                    book.Author = author;
                    book.Year = string.IsNullOrEmpty(year) ? (int?)null : int.Parse(year);
                    book.Pages = string.IsNullOrEmpty(pages) ? (int?)null : int.Parse(pages);
                    context.SaveChanges();
                }
                else
                {
                    Console.WriteLine("Book not found.");
                }
            }
        }
    }
}
