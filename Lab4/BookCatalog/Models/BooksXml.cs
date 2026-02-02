using System.Collections.Generic;
using System.Xml.Serialization;

namespace BookCatalog.Models
{
    [XmlRoot("Books")]
    public class BooksXml
    {
        [XmlElement("Book")]
        public List<Book> Items { get; set; } = new();
    }
}
