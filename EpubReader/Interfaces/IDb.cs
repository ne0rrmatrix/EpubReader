using EpubReader.Models;

namespace EpubReader.Interfaces;

/// <summary>
/// Defines methods for managing books and settings in a database.
/// </summary>
/// <remarks>This interface provides methods to retrieve, save, update, and remove books and settings.
/// Implementations should handle the persistence and retrieval of these entities.</remarks>
public interface IDb
{
	/// <summary>
	/// Retrieves a book from the collection that matches the specified book.
	/// </summary>
	/// <param name="book">The book to search for in the collection. Must not be null.</param>
	/// <returns>The matching <see cref="Book"/> if found; otherwise, <see langword="null"/>.</returns>
	Book? GetBook(Book book);

	/// <summary>
	/// Retrieves a list of all available books.
	/// </summary>
	/// <returns>A list of <see cref="Book"/> objects representing all books in the collection.  Returns <see langword="null"/> if
	/// no books are available.</returns>
	List<Book>? GetAllBooks();

	/// <summary>
	/// Retrieves the current application settings.
	/// </summary>
	/// <returns>The current <see cref="Settings"/> object if available; otherwise, <see langword="null"/>.</returns>
	Settings? GetSettings();

	/// <summary>
	/// Saves the specified book data to the storage system.
	/// </summary>
	/// <remarks>This method persists the book information, including title, author, and publication details, to the
	/// underlying storage. Ensure that the book object is fully populated before calling this method.</remarks>
	/// <param name="book">The book object containing the data to be saved. Cannot be null.</param>
	void SaveBookData(Book book);

	/// <summary>
	/// Saves the specified settings to the persistent storage.
	/// </summary>
	/// <param name="settings">The settings to be saved. Cannot be null.</param>
	void SaveSettings(Settings settings);

	/// <summary>
	/// Removes all settings from the current configuration.
	/// </summary>
	/// <remarks>This method clears all existing settings, resetting the configuration to its default state. Use
	/// this method with caution, as it will permanently delete all settings.</remarks>
	void RemoveAllSettings();

	/// <summary>
	/// Removes all books from the collection.
	/// </summary>
	/// <remarks>This method clears the entire collection of books, leaving it empty.  Use this method with caution
	/// as it will permanently remove all entries.</remarks>
	void RemoveAllBooks();

	/// <summary>
	/// Removes the specified book from the collection.
	/// </summary>
	/// <remarks>This method removes the first occurrence of the specified book from the collection.  If the book is
	/// not found, the collection remains unchanged.</remarks>
	/// <param name="book">The book to be removed. Cannot be null.</param>
	void RemoveBook(Book book);

	/// <summary>
	/// Updates the bookmark for the specified book.
	/// </summary>
	/// <param name="book">The book for which the bookmark is to be updated. Cannot be null.</param>
	void UpdateBookMark(Book book);
}
