using System;
using System.Linq;
using BookCatalog.Data;
using BookCatalog.Models;

namespace BookCatalog.Commands
{
    public static class DeleteBook
    {
        public static void Execute(string isbn)
        {
            using (var context = new BooksContext())
            {
                var book = context.Books.FirstOrDefault(b => b.ISBN == isbn);
                if (book != null)
                {
                    context.Books.Remove(book);
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