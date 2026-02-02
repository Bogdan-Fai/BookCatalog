using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Serialization;
using CsvHelper;
using System.Globalization;
using BookCatalog.Data;
using BookCatalog.Models;

namespace BookCatalog.Commands
{
    public static class ImportBooks
    {
        public static void Execute(string filePath)
        {
            try
            {
                Logger.Log($"Starting EF Core import from: {filePath}");

                if (!File.Exists(filePath))
                {
                    Logger.LogError($"File not found: {filePath}");
                    return;
                }

                if (Path.HasExtension(filePath) && Path.GetExtension(filePath).ToLower() == ".csv")
                {
                    using (var reader = new StreamReader(filePath))
                    using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                    {
                        var records = csv.GetRecords<Book>().ToList();
                        using (var context = new BooksContext())
                        {
                            context.Books.AddRange(records);
                            context.SaveChanges();
                            Logger.LogSuccess($"Imported {records.Count} books from CSV");
                        }
                    }
                }
                else if (Path.HasExtension(filePath) && Path.GetExtension(filePath).ToLower() == ".xml")
                {
                    var serializer = new XmlSerializer(typeof(List<Book>));
                    using (var reader = new StreamReader(filePath))
                    {
                        var books = (List<Book>?)serializer.Deserialize(reader) ?? new List<Book>();
                        using (var context = new BooksContext())
                        {
                            context.Books.AddRange(books);
                            context.SaveChanges();
                            Logger.LogSuccess($"Imported {books.Count} books from XML");
                        }
                    }
                }
                else
                {
                    Logger.LogError("Unsupported file format. Only CSV and XML are supported.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Import failed", ex);
            }
        }
    }
}