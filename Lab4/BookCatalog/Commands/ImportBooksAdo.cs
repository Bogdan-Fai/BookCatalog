using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Xml.Schema;
using System.Xml;
using CsvHelper;
using System.Globalization;
using Microsoft.Data.Sqlite;
using System.Xml.Serialization;
using BookCatalog.Models;

namespace BookCatalog.Commands
{
    public static class ImportBooksAdo
    {
        private static int _errorCount = 0;
        private static int _maxErrors = 0;
        private static string _duplicateStrategy = "update";

        public static void Execute(string filePath, string strategy = "update", int maxErrors = 0)
        {
            _errorCount = 0;
            _maxErrors = maxErrors;
            _duplicateStrategy = strategy.ToLower();

            Logger.Log($"Starting ADO.NET import from: {filePath}");
            Logger.Log($"Strategy: {_duplicateStrategy}, Max errors: {_maxErrors}");

            if (!File.Exists(filePath))
            {
                Logger.LogError($"File not found: {filePath}");
                return;
            }

            List<Book> books = new List<Book>();
            int successCount = 0;
            int totalProcessed = 0;

            try
            {
                string extension = Path.GetExtension(filePath).ToLower();

                if (extension == ".csv")
                {
                    books = ReadCsvFile(filePath);
                }
                else if (extension == ".xml")
                {
                    books = ReadXmlFile(filePath);
                }
                else
                {
                    Logger.LogError("Unsupported file format. Only CSV and XML are supported.");
                    return;
                }

                Logger.Log($"Found {books.Count} books in file");

                // Импорт в базу данных с использованием транзакций
                using (var connection = new SqliteConnection("Data Source=books.db"))
                {
                    connection.Open();
                    
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            foreach (var book in books)
                            {
                                totalProcessed++;
                                if (InsertBook(connection, book))
                                {
                                    successCount++;
                                }

                                // Проверка порога ошибок
                                if (_maxErrors > 0 && _errorCount >= _maxErrors)
                                {
                                    Logger.LogError($"Maximum error threshold ({_maxErrors}) reached. Rolling back transaction.");
                                    transaction.Rollback();
                                    Logger.LogOperation("ADO.NET Import - ROLLED BACK", totalProcessed, successCount, _errorCount);
                                    return;
                                }
                            }

                            transaction.Commit();
                            Logger.LogOperation("ADO.NET Import - COMMITTED", totalProcessed, successCount, _errorCount);
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            Logger.LogError("Transaction rolled back due to error", ex);
                            Logger.LogOperation("ADO.NET Import - ROLLED BACK", totalProcessed, successCount, _errorCount);
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Import failed", ex);
            }
        }

        private static List<Book> ReadCsvFile(string filePath)
        {
            var books = new List<Book>();

            try
            {
                using (var reader = new StreamReader(filePath))
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    var records = csv.GetRecords<dynamic>();

                    foreach (var record in records)
                    {
                        try
                        {
                            var book = new Book
                            {
                                ISBN = record.ISBN?.ToString() ?? "",
                                Title = record.Title?.ToString() ?? "",
                                Author = record.Author?.ToString() ?? "",
                                Year = ParseNullableInt(record.Year?.ToString()),
                                Pages = ParseNullableInt(record.Pages?.ToString())
                            };

                            // Базовая валидация
                            if (!string.IsNullOrEmpty(book.ISBN) && !string.IsNullOrEmpty(book.Title))
                            {
                                books.Add(book);
                            }
                            else
                            {
                                Logger.LogWarning($"Skipping invalid book record: {record.ISBN}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Error parsing CSV record: {record.ISBN}", ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error reading CSV file", ex);
                throw;
            }

            return books;
        }

        private static List<Book> ReadXmlFile(string filePath)
        {
            var books = new List<Book>();

            try
            {
                // Валидация XSD
                if (!ValidateXmlWithXsd(filePath, "books.xsd"))
                {
                    Logger.LogError("XML validation failed against XSD schema");
                    return books;
                }

                var serializer = new XmlSerializer(typeof(BooksXml));

                using (var reader = new StreamReader(filePath))
                {
                    var wrapper = serializer.Deserialize(reader) as BooksXml;
                    books = wrapper?.Items ?? new List<Book>();
                }

            }
            catch (Exception ex)
            {
                Logger.LogError("Error reading XML file", ex);
                throw;
            }

            return books;
        }

        private static bool ValidateXmlWithXsd(string xmlFilePath, string xsdFilePath)
        {
            try
            {
                if (!File.Exists(xsdFilePath))
                {
                    Logger.LogWarning("XSD schema file not found, skipping validation");
                    return true;
                }

                var settings = new XmlReaderSettings();
                settings.Schemas.Add(null, xsdFilePath);
                settings.ValidationType = ValidationType.Schema;

                var validationErrors = new List<string>();
                settings.ValidationEventHandler += (sender, e) =>
                {
                    validationErrors.Add($"{e.Severity}: {e.Message}");
                };

                using (var reader = XmlReader.Create(xmlFilePath, settings))
                {
                    while (reader.Read()) { }
                }

                if (validationErrors.Count > 0)
                {
                    foreach (var error in validationErrors)
                    {
                        Logger.LogError($"XML Validation Error: {error}");
                    }
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("XSD validation failed", ex);
                return false;
            }
        }

        private static bool InsertBook(SqliteConnection connection, Book book)
        {
            try
            {
                using (var command = connection.CreateCommand())
                {
                    string sql = _duplicateStrategy switch
                    {
                        "skip" => @"
                            INSERT OR IGNORE INTO Books (ISBN, Title, Author, Year, Pages)
                            VALUES (@isbn, @title, @author, @year, @pages)",
                        "merge" => @"
                            INSERT INTO Books (ISBN, Title, Author, Year, Pages)
                            VALUES (@isbn, @title, @author, @year, @pages)
                            ON CONFLICT(ISBN) DO UPDATE SET
                                Title = COALESCE(excluded.Title, Books.Title),
                                Author = COALESCE(excluded.Author, Books.Author),
                                Year = COALESCE(excluded.Year, Books.Year),
                                Pages = COALESCE(excluded.Pages, Books.Pages)",
                        _ => @"
                            INSERT INTO Books (ISBN, Title, Author, Year, Pages)
                            VALUES (@isbn, @title, @author, @year, @pages)
                            ON CONFLICT(ISBN) DO UPDATE SET
                                Title = excluded.Title,
                                Author = excluded.Author,
                                Year = excluded.Year,
                                Pages = excluded.Pages"
                    };

                    command.CommandText = sql;

                    // Параметризованные запросы для безопасности
                    command.Parameters.AddWithValue("@isbn", book.ISBN);
                    command.Parameters.AddWithValue("@title", book.Title);
                    command.Parameters.AddWithValue("@author", book.Author);
                    command.Parameters.AddWithValue("@year", book.Year ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@pages", book.Pages ?? (object)DBNull.Value);

                    int rowsAffected = command.ExecuteNonQuery();
                    
                    if (rowsAffected > 0)
                    {
                        Logger.Log($"Book processed: {book.ISBN} - {book.Title} (Strategy: {_duplicateStrategy})");
                        return true;
                    }
                    else
                    {
                        Logger.LogWarning($"Book skipped (duplicate): {book.ISBN} - {book.Title}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                _errorCount++;
                Logger.LogError($"Failed to insert book: {book.ISBN} - {book.Title}", ex);
                return false;
            }
        }

        private static int? ParseNullableInt(string value)
        {
            if (string.IsNullOrEmpty(value) || !int.TryParse(value, out int result))
                return null;
            return result;
        }
    }
}