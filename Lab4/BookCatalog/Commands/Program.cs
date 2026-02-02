using System;
using BookCatalog.Data;
using BookCatalog.Commands;

namespace BookCatalog
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var context = new BooksContext())
            {
                context.Database.EnsureCreated();

                Logger.Log("Application started");

                if (args.Length > 0)
                {
                    try
                    {
                        switch (args[0].ToLower())
                        {
                            case "import":
                                ImportBooks.Execute(args[1]);
                                break;
                            case "export":
                                ExportBooks.Execute(args[1]);
                                break;
                            case "add":
                                AddBook.Execute(args[1], args[2], args[3], args[4], args[5]);
                                break;
                            case "update":
                                UpdateBook.Execute(args[1], args[2], args[3], args[4], args[5]);
                                break;
                            case "delete":
                                DeleteBook.Execute(args[1]);
                                break;
                            case "find":
                                FindBooks.Execute(args[1]);
                                break;
                            case "import_ado":
                                var strategy = args.Length > 2 ? args[2] : "update";
                                var maxErrors = args.Length > 3 ? int.Parse(args[3]) : 0;
                                ImportBooksAdo.Execute(args[1], strategy, maxErrors);
                                break;
                            case "sync":
                                var syncStrategy = args.Length > 1 ? args[1] : "ask";
                                var syncMaxErrors = args.Length > 2 ? int.Parse(args[2]) : 5;
                                SyncBooks.Execute(syncStrategy, syncMaxErrors);
                                break;
                            default:
                                Console.WriteLine("Invalid command.");
                                ShowHelp();
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Command execution failed: {args[0]}", ex);
                        Console.WriteLine($"Error: {ex.Message}");
                    }
                }
                else
                {
                    ShowHelp();
                }
            }
        }

        static void ShowHelp()
        {
            Console.WriteLine("Available commands:");
            Console.WriteLine("  import <file>              - Import books using EF Core");
            Console.WriteLine("  import_ado <file> [strategy] [maxErrors] - Import using ADO.NET");
            Console.WriteLine("                               Strategies: update, skip, merge");
            Console.WriteLine("  export <format>            - Export to JSON or XML");
            Console.WriteLine("  add <isbn> <title> <author> <year> <pages> - Add new book");
            Console.WriteLine("  update <isbn> <title> <author> <year> <pages> - Update book");
            Console.WriteLine("  delete <isbn>              - Delete book by ISBN");
            Console.WriteLine("  find <query>               - Search books");
            Console.WriteLine("  sync [strategy] [maxErrors] - Synchronize DB with JSON file");
            Console.WriteLine("                               Strategies: ask, update_db, update_file, skip");
            Console.WriteLine("");
            Console.WriteLine("Examples:");
            Console.WriteLine("  dotnet run import_ado books.csv merge 5");
            Console.WriteLine("  dotnet run sync ask 3");
            Console.WriteLine("  dotnet run add \"123\" \"Book Title\" \"Author\" \"2020\" \"300\"");
        }
    }
}