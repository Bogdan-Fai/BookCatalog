using System;
using BookCatalog.Data;
using BookCatalog.Models;

namespace BookCatalog.Commands
{
    public static class AddBook
    {
        public static void Execute(string isbn, string title, string author, string year, string pages)
        {
            try
            {
                Logger.Log($"Adding new book: ISBN={isbn}, Title={title}, Author={author}");

                var book = new Book
                {
                    ISBN = isbn,
                    Title = title,
                    Author = author,
                    Year = string.IsNullOrEmpty(year) ? (int?)null : int.Parse(year),
                    Pages = string.IsNullOrEmpty(pages) ? (int?)null : int.Parse(pages)
                };

                using (var context = new BooksContext())
                {
                    context.Books.Add(book);
                    context.SaveChanges();
                }

                Logger.LogSuccess($"Book added successfully: {isbn} - {title}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to add book: ISBN={isbn}", ex);
                throw; // Перебрасываем исключение для обработки на верхнем уровне
            }
        }
    }
}