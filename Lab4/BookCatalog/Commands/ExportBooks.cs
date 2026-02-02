using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Text.Json;
using BookCatalog.Data;
using BookCatalog.Models;

namespace BookCatalog.Commands
{
    public static class ExportBooks
    {
        public static void Execute(string format)
        {
            using (var context = new BooksContext())
            {
                var books = context.Books.ToList();
                if (format.ToLower() == "json")
                {
                    var json = JsonSerializer.Serialize(books);
                    File.WriteAllText("books.json", json);
                }
                else if (format.ToLower() == "xml")
                {
                    var serializer = new XmlSerializer(typeof(List<Book>));
                    using (var writer = new StreamWriter("books.xml"))
                    {
                        serializer.Serialize(writer, books);
                    }
                }
                else
                {
                    Console.WriteLine("Unsupported format.");
                }
            }
        }
    }
}
