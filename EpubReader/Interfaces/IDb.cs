using EpubReader.Models;

namespace EpubReader.Interfaces;

public interface IDb
{
	Book? GetBook(Book book);
	List<Book>? GetAllBooks();
	Settings? GetSettings();
	void SaveBookData(Book book);
	void SaveSettings(Settings settings);
	void RemoveAllSettings();
	void RemoveAllBooks();
	void RemoveBook(Book book);
}
