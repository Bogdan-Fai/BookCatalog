using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BookCatalog.Data;
using BookCatalog.Models;

namespace BookCatalog.Commands
{
    public static class SyncBooks
    {
        private static string _conflictStrategy = "ask";
        private static int _maxErrors = 5;
        private static int _errorCount = 0;

        public static void Execute(string strategy = "ask", int maxErrors = 5)
        {
            _conflictStrategy = strategy.ToLower();
            _maxErrors = maxErrors;
            _errorCount = 0;

            Logger.Log($"Starting database synchronization with JSON file");
            Logger.Log($"Conflict strategy: {_conflictStrategy}, Max errors: {_maxErrors}");

            try
            {
                // Чтение данных из базы данных
                List<Book> dbBooks;
                using (var context = new BooksContext())
                {
                    dbBooks = context.Books.ToList();
                }

                // Чтение данных из JSON файла
                List<Book> jsonBooks = ReadJsonFile("books.json");

                // Поиск различий
                var differences = FindDifferences(dbBooks, jsonBooks);

                // Логирование различий
                LogDifferences(differences);

                // Разрешение конфликтов
                if (differences.HasConflicts)
                {
                    ResolveConflicts(differences);
                }
                else
                {
                    Logger.LogSuccess("No conflicts found - databases are synchronized");
                }

                if (_errorCount > 0)
                {
                    Logger.LogWarning($"Synchronization completed with {_errorCount} errors");
                }
                else
                {
                    Logger.LogSuccess("Synchronization completed successfully");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Synchronization failed", ex);
            }
        }

        private static List<Book> ReadJsonFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Logger.LogWarning($"JSON file not found: {filePath}");
                    return new List<Book>();
                }

                var jsonString = File.ReadAllText(filePath);
                var books = JsonSerializer.Deserialize<List<Book>>(jsonString) ?? new List<Book>();
                
                Logger.Log($"Read {books.Count} books from JSON file");
                return books;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error reading JSON file: {filePath}", ex);
                return new List<Book>();
            }
        }

        private static SyncDifferences FindDifferences(List<Book> dbBooks, List<Book> jsonBooks)
        {
            var differences = new SyncDifferences();

            // Создаем словари для быстрого поиска по ISBN
            var dbDict = dbBooks.ToDictionary(b => b.ISBN);
            var jsonDict = jsonBooks.ToDictionary(b => b.ISBN);

            // Книги только в базе данных
            differences.OnlyInDatabase = dbBooks.Where(b => !jsonDict.ContainsKey(b.ISBN)).ToList();

            // Книги только в JSON файле
            differences.OnlyInJson = jsonBooks.Where(b => !dbDict.ContainsKey(b.ISBN)).ToList();

            // Конфликтующие книги (есть в обоих, но с разными данными)
            differences.Conflicting = new List<BookConflict>();

            foreach (var isbn in dbDict.Keys.Intersect(jsonDict.Keys))
            {
                var dbBook = dbDict[isbn];
                var jsonBook = jsonDict[isbn];

                if (!AreBooksEqual(dbBook, jsonBook))
                {
                    differences.Conflicting.Add(new BookConflict
                    {
                        ISBN = isbn,
                        DatabaseVersion = dbBook,
                        JsonVersion = jsonBook,
                        ChangedFields = GetChangedFields(dbBook, jsonBook)
                    });
                }
            }

            differences.HasConflicts = differences.OnlyInDatabase.Count > 0 || 
                                     differences.OnlyInJson.Count > 0 || 
                                     differences.Conflicting.Count > 0;

            return differences;
        }

        private static bool AreBooksEqual(Book book1, Book book2)
        {
            return book1.ISBN == book2.ISBN &&
                   book1.Title == book2.Title &&
                   book1.Author == book2.Author &&
                   book1.Year == book2.Year &&
                   book1.Pages == book2.Pages;
        }

        private static List<string> GetChangedFields(Book dbBook, Book jsonBook)
        {
            var changedFields = new List<string>();

            if (dbBook.Title != jsonBook.Title) changedFields.Add("Title");
            if (dbBook.Author != jsonBook.Author) changedFields.Add("Author");
            if (dbBook.Year != jsonBook.Year) changedFields.Add("Year");
            if (dbBook.Pages != jsonBook.Pages) changedFields.Add("Pages");

            return changedFields;
        }

        private static void LogDifferences(SyncDifferences differences)
        {
            Logger.Log($"Synchronization differences found:");
            Logger.Log($"  - Books only in database: {differences.OnlyInDatabase.Count}");
            Logger.Log($"  - Books only in JSON file: {differences.OnlyInJson.Count}");
            Logger.Log($"  - Conflicting books: {differences.Conflicting.Count}");

            // Детальное логирование
            foreach (var book in differences.OnlyInDatabase)
            {
                Logger.Log($"    Only in DB: {book.ISBN} - {book.Title}");
            }

            foreach (var book in differences.OnlyInJson)
            {
                Logger.Log($"    Only in JSON: {book.ISBN} - {book.Title}");
            }

            foreach (var conflict in differences.Conflicting)
            {
                Logger.Log($"    Conflict: {conflict.ISBN}");
                Logger.Log($"      DB: {conflict.DatabaseVersion.Title} by {conflict.DatabaseVersion.Author}");
                Logger.Log($"      JSON: {conflict.JsonVersion.Title} by {conflict.JsonVersion.Author}");
                Logger.Log($"      Changed fields: {string.Join(", ", conflict.ChangedFields)}");
            }
        }

        private static void ResolveConflicts(SyncDifferences differences)
        {
            int totalResolved = 0;

            using (var context = new BooksContext())
            {
                using (var transaction = context.Database.BeginTransaction())
                {
                    try
                    {
                        // Стратегия разрешения конфликтов
                        if (_conflictStrategy == "ask")
                        {
                            totalResolved = ResolveInteractive(context, differences);
                        }
                        else
                        {
                            totalResolved = ResolveAutomatic(context, differences, _conflictStrategy);
                        }

                        // Проверка порога ошибок
                        if (_maxErrors > 0 && _errorCount >= _maxErrors)
                        {
                            Logger.LogError($"Maximum error threshold ({_maxErrors}) reached. Rolling back transaction.");
                            transaction.Rollback();
                            Logger.LogOperation("Sync - ROLLED BACK", differences.TotalConflicts, totalResolved, _errorCount);
                            return;
                        }

                        // Сохраняем изменения
                        if (totalResolved > 0)
                        {
                            int saved = context.SaveChanges();
                            transaction.Commit();
                            Logger.LogSuccess($"Resolved {saved} conflicts successfully");
                            Logger.LogOperation("Sync - COMMITTED", differences.TotalConflicts, totalResolved, _errorCount);
                        }
                        else
                        {
                            transaction.Commit();
                            Logger.Log("No conflicts to resolve");
                        }
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        Logger.LogError("Transaction rolled back due to error", ex);
                        Logger.LogOperation("Sync - ROLLED BACK", differences.TotalConflicts, totalResolved, _errorCount);
                        throw;
                    }
                }
            }

            // Обновляем JSON файл с текущим состоянием базы данных
            UpdateJsonFile();
        }

        private static int ResolveAutomatic(BooksContext context, SyncDifferences differences, string strategy)
        {
            int resolved = 0;

            Logger.Log($"Applying automatic strategy: {strategy}");

            switch (strategy)
            {
                case "update_db":
                    // Обновляем БД данными из JSON
                    foreach (var book in differences.OnlyInJson)
                    {
                        try
                        {
                            context.Books.Add(book);
                            Logger.Log($"Adding book from JSON: {book.ISBN} - {book.Title}");
                            resolved++;
                        }
                        catch (Exception ex)
                        {
                            _errorCount++;
                            Logger.LogError($"Failed to add book: {book.ISBN}", ex);
                        }
                    }

                    foreach (var conflict in differences.Conflicting)
                    {
                        try
                        {
                            var dbBook = context.Books.First(b => b.ISBN == conflict.ISBN);
                            
                            // Обновляем поля
                            dbBook.Title = conflict.JsonVersion.Title;
                            dbBook.Author = conflict.JsonVersion.Author;
                            dbBook.Year = conflict.JsonVersion.Year;
                            dbBook.Pages = conflict.JsonVersion.Pages;

                            Logger.Log($"Updating book from JSON: {conflict.ISBN}");
                            resolved++;
                        }
                        catch (Exception ex)
                        {
                            _errorCount++;
                            Logger.LogError($"Failed to update book: {conflict.ISBN}", ex);
                        }
                    }
                    break;

                case "update_file":
                    // Обновляем JSON данными из БД (просто обновим файл в конце)
                    Logger.Log("JSON file will be updated with database data");
                    break;

                case "skip":
                    Logger.Log("Skipping all conflicts - no changes made");
                    break;
            }

            return resolved;
        }

        private static int ResolveInteractive(BooksContext context, SyncDifferences differences)
        {
            int resolved = 0;

            Logger.Log("Starting interactive conflict resolution:");

            // Книги только в JSON
            foreach (var book in differences.OnlyInJson)
            {
                Console.WriteLine($"\nBook found only in JSON: {book.ISBN} - {book.Title} by {book.Author}");
                Console.Write("Add to database? (y/n/skip all): ");
                var response = Console.ReadLine()?.ToLower();

                if (response == "y")
                {
                    try
                    {
                        context.Books.Add(book);
                        Logger.Log($"Adding book from JSON: {book.ISBN} - {book.Title}");
                        resolved++;
                    }
                    catch (Exception ex)
                    {
                        _errorCount++;
                        Logger.LogError($"Failed to add book: {book.ISBN}", ex);
                    }
                }
                else if (response == "skip all")
                {
                    Logger.Log("Skipping all remaining conflicts");
                    break;
                }
            }

            // Конфликтующие книги
            foreach (var conflict in differences.Conflicting)
            {
                if (_maxErrors > 0 && _errorCount >= _maxErrors)
                {
                    Logger.LogWarning("Error threshold reached, stopping interactive resolution");
                    break;
                }

                Console.WriteLine($"\nConflict detected for: {conflict.ISBN}");
                Console.WriteLine($"Database: {conflict.DatabaseVersion.Title} by {conflict.DatabaseVersion.Author}");
                Console.WriteLine($"JSON:     {conflict.JsonVersion.Title} by {conflict.JsonVersion.Author}");
                Console.WriteLine($"Changed fields: {string.Join(", ", conflict.ChangedFields)}");
                Console.Write("Choose action - [d]atabase version, [j]son version, [s]kip, skip [a]ll: ");
                
                var response = Console.ReadLine()?.ToLower();

                try
                {
                    var dbBook = context.Books.First(b => b.ISBN == conflict.ISBN);

                    switch (response)
                    {
                        case "d":
                            // Keep database version (do nothing)
                            Logger.Log($"Keeping database version for: {conflict.ISBN}");
                            break;
                        case "j":
                            // Update with JSON version
                            dbBook.Title = conflict.JsonVersion.Title;
                            dbBook.Author = conflict.JsonVersion.Author;
                            dbBook.Year = conflict.JsonVersion.Year;
                            dbBook.Pages = conflict.JsonVersion.Pages;
                            Logger.Log($"Updated with JSON version for: {conflict.ISBN}");
                            resolved++;
                            break;
                        case "s":
                            Logger.Log($"Skipped conflict for: {conflict.ISBN}");
                            break;
                        case "a":
                            Logger.Log("Skipping all remaining conflicts");
                            return resolved;
                        default:
                            Logger.LogWarning($"Invalid choice, skipping: {conflict.ISBN}");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _errorCount++;
                    Logger.LogError($"Failed to resolve conflict for: {conflict.ISBN}", ex);
                }
            }

            return resolved;
        }

        private static void UpdateJsonFile()
        {
            try
            {
                using (var context = new BooksContext())
                {
                    var books = context.Books.ToList();
                    var json = JsonSerializer.Serialize(books, new JsonSerializerOptions 
                    { 
                        WriteIndented = true 
                    });
                    
                    File.WriteAllText("books.json", json);
                    Logger.LogSuccess("Updated books.json with current database state");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to update JSON file", ex);
            }
        }
    }

    // Вспомогательные классы для хранения различий
    public class SyncDifferences
    {
        public List<Book> OnlyInDatabase { get; set; } = new List<Book>();
        public List<Book> OnlyInJson { get; set; } = new List<Book>();
        public List<BookConflict> Conflicting { get; set; } = new List<BookConflict>();
        public bool HasConflicts { get; set; }
        
        public int TotalConflicts => OnlyInDatabase.Count + OnlyInJson.Count + Conflicting.Count;
    }

    public class BookConflict
    {
        public string ISBN { get; set; } = string.Empty;
        public Book DatabaseVersion { get; set; } = new Book();
        public Book JsonVersion { get; set; } = new Book();
        public List<string> ChangedFields { get; set; } = new List<string>();
    }
}