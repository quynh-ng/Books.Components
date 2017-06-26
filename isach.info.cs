#region Related components
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;

using Newtonsoft.Json.Linq;

using net.vieapps.Components.Utility;
#endregion

namespace net.vieapps.books.Components
{
	public static class ISach
	{

		#region Parse & Get information of a listing (bookself)
		public static BookSelf InitializeBookSelf(string folder)
		{
			if (!string.IsNullOrWhiteSpace(folder) && !Directory.Exists(folder))
				Directory.CreateDirectory(folder);

			string filePath = (string.IsNullOrWhiteSpace(folder) ? "" : folder + "\\") + "isach.info.status.json";
			JObject json = File.Exists(filePath)
										? JObject.Parse(Utility.ReadTextFile(filePath))
										: new JObject()
										{
											{ "TotalPages", 0 },
											{ "CurrentPage", 0 },
											{ "Category", ISach.Categories[0] },
											{ "Char", "0" },
											{ "LastActivity", DateTime.Now },
										};

			BookSelf bookself = new BookSelf();
			bookself.TotalPages = Convert.ToInt32((json["TotalPages"] as JValue).Value);
			bookself.CurrentPage = Convert.ToInt32((json["CurrentPage"] as JValue).Value);

			string category = (json["Category"] as JValue).Value.ToString();
			string @char = ISach.LargeCategories.Contains(category) ? (json["Char"] as JValue).Value.ToString() : null;

			if (bookself.TotalPages < 1)
				bookself.CurrentPage = 0;
			else if (bookself.CurrentPage >= bookself.TotalPages)
			{
				int catIndex = ISach.Categories.IndexOf(category);
				if (!string.IsNullOrWhiteSpace(@char))
				{
					if (@char[0].Equals('0'))
						@char = "A";
					else if (@char[0] < 'Z')
					{
						char ch = @char[0];
						ch++;
						@char = ch.ToString();
					}
					else
					{
						catIndex++;
						@char = null;
					}
				}
				else
				{
					catIndex++;
					@char = null;
				}
				category = catIndex < ISach.Categories.Count ? ISach.Categories[catIndex] : null;
				@char = string.IsNullOrWhiteSpace(category)
								? null
								: string.IsNullOrWhiteSpace(@char) && ISach.LargeCategories.Contains(category) ? "0" : @char;
				bookself.CurrentPage = 0;
				bookself.TotalPages = 0;
			}

			bookself.CurrentPage++;
			bookself.UrlPattern = string.IsNullOrWhiteSpace(category)
					? null
					: string.IsNullOrWhiteSpace(@char)
						? "http://isach.info/mobile/story.php?list=story&category={0}&order=created_date&page={1}"
						: "http://isach.info/mobile/story.php?list=story&category={0}&order=created_date&char={1}&page={2}";
			bookself.UrlParameters = new List<string>();
			if (category != null)
				bookself.UrlParameters.Add(category);
			if (@char != null)
				bookself.UrlParameters.Add(@char);

			return bookself;
		}

		public static void FinaIizeBookSelf(BookSelf bookself, string folder)
		{
			if (!string.IsNullOrWhiteSpace(folder) && !Directory.Exists(folder))
				Directory.CreateDirectory(folder);

			string filePath = (string.IsNullOrWhiteSpace(folder) ? "" : folder + "\\") + "isach.info.status.json";
			JObject json = File.Exists(filePath)
										? JObject.Parse(Utility.ReadTextFile(filePath))
										: new JObject()
										{
											{ "TotalPages", 0 },
											{ "CurrentPage", 0 },
											{ "Category", ISach.Categories[0] },
											{ "Char", "0" },
											{ "LastActivity", DateTime.Now },
										};

			if (bookself.TotalPages > 0)
				json["TotalPages"] = bookself.TotalPages;
			if (bookself.CurrentPage > 0)
				json["CurrentPage"] = bookself.CurrentPage;
			json["LastActivity"] = DateTime.Now;

			int start = bookself.Url.IndexOf("&category="), end = -1;
			if (start > 0)
			{
				start += 10;
				end = bookself.Url.IndexOf("&", start + 1);
				if (end < 0)
					end = bookself.Url.Length;
				json["Category"] = bookself.Url.Substring(start, end - start);
			}

			start = bookself.Url.IndexOf("&char=");
			if (start > 0)
			{
				start += 6;
				end = bookself.Url.IndexOf("&", start + 1);
				if (end < 0)
					end = bookself.Url.Length;
				json["Char"] = bookself.Url.Substring(start, end - start);
			}

			Utility.WriteTextFile(filePath, json.ToString(Newtonsoft.Json.Formatting.Indented));

			List<string> books = new List<string>();
			if (bookself.Books != null)
				for (int index = 0; index < bookself.Books.Count; index++)
					books.Add(bookself.Books[index].ToString(Newtonsoft.Json.Formatting.None));

			filePath = (string.IsNullOrWhiteSpace(folder) ? "" : folder + "\\") + "isach.info.json";
			Utility.WriteTextFile(filePath, books);
		}

		public static async Task<BookSelf> GetBookSelf(BookSelf bookself, CancellationToken cancellationToken, Action<BookSelf> onCompleted, Action<BookSelf, Exception> onError)
		{
			// get data
			cancellationToken.ThrowIfCancellationRequested();

			bookself.Url = bookself.UrlPattern;
			if (bookself.UrlParameters.Count > 0)
				for (int index = 0; index < bookself.UrlParameters.Count; index++)
					bookself.Url = bookself.Url.Replace("{" + index + "}", bookself.UrlParameters[index]);

			string html = "";
			try
			{
				html = await Utility.GetWebPageAsync(bookself.Url, ISach.ReferUri, Utility.SpiderUserAgent, cancellationToken);
			}
			catch (Exception ex)
			{
				if (onError != null)
				{
					onError(bookself, ex);
					return bookself;
				}
				else
					throw ex;
			}

			cancellationToken.ThrowIfCancellationRequested();
			int start = -1, end = -1;

			// pages
			if (bookself.TotalPages < 1)
			{
				start = html.IndexOf("paging_box_top", StringComparison.OrdinalIgnoreCase);
				start = start < 0 ? -1 : html.IndexOf("<ul", start + 1, StringComparison.OrdinalIgnoreCase);
				end = start < 0 ? -1 : html.IndexOf("</div>", start + 1, StringComparison.OrdinalIgnoreCase);
				if (start > 0 && end > 0)
				{
					string info = html.Substring(start, end - start), data = "";
					start = info.IndexOf("<a", StringComparison.OrdinalIgnoreCase);
					while (start > -1)
					{
						end = info.IndexOf(">", start + 1, StringComparison.OrdinalIgnoreCase);
						string anchor = info.Substring(start, end - start);
						if (anchor.IndexOf("class='navigator'") < 0)
						{
							start = info.IndexOf("href=\"", start + 1, StringComparison.OrdinalIgnoreCase) + 6;
							end = info.IndexOf("\"", start + 1, StringComparison.OrdinalIgnoreCase);
							data = info.Substring(start, end - start);
						}
						start = info.IndexOf("<a", start + 1, StringComparison.OrdinalIgnoreCase);
					}
					start = data.IndexOf("page=");
					bookself.TotalPages = Convert.ToInt32(data.Substring(start + 5));
				}
				else if (html.IndexOf("paging_box_empty", StringComparison.OrdinalIgnoreCase) > 0)
					bookself.TotalPages = 1;
			}

			// books
			bookself.Books = new List<Book>();

			start = html.IndexOf("story_content_list", StringComparison.OrdinalIgnoreCase);
			start = start < 0 ? -1 : html.IndexOf("<div class='ms_list_item'>", start + 1, StringComparison.OrdinalIgnoreCase);
			end = start < 0 ? -1 : html.IndexOf("<div class='paging_box_bottom'>", start + 1, StringComparison.OrdinalIgnoreCase);
			html = start > 0 && end > 0 ? html.Substring(start, end - start) : "";

			start = html.IndexOf("<div class='ms_list_item'>", StringComparison.OrdinalIgnoreCase);
			start = html.IndexOf("<a", start + 1, StringComparison.OrdinalIgnoreCase) > -1 ? start : -1;
			while (start > -1)
			{
				Book book = new Book();
				book.Source = "isach.info";

				start = html.IndexOf("<a", start + 1, StringComparison.OrdinalIgnoreCase);
				end = html.IndexOf(">", start + 1, StringComparison.OrdinalIgnoreCase);
				if (html.Substring(start, end - start).IndexOf("story.php") < 0)
					start = html.IndexOf("<a", start + 1, StringComparison.OrdinalIgnoreCase);
				start = html.IndexOf("href=\"", start + 1, StringComparison.OrdinalIgnoreCase) + 6;
				end = html.IndexOf("\"", start + 1, StringComparison.OrdinalIgnoreCase);
				book.SourceUri = "http://isach.info/mobile/" + html.Substring(start, end - start).Trim();

				start = html.IndexOf(">", start + 1, StringComparison.OrdinalIgnoreCase) + 1;
				end = html.IndexOf("</a>", start + 1, StringComparison.OrdinalIgnoreCase);
				book.Title = html.Substring(start, end - start).GetNormalized();

				start = html.IndexOf("<a", start + 1, StringComparison.OrdinalIgnoreCase) + 1;
				end = html.IndexOf(">", start + 1, StringComparison.OrdinalIgnoreCase);
				start = html.IndexOf("<span", start + 1, StringComparison.OrdinalIgnoreCase);
				start = html.IndexOf(">", start + 1, StringComparison.OrdinalIgnoreCase) + 1;
				end = html.IndexOf("<", start + 1, StringComparison.OrdinalIgnoreCase);
				book.Category = Book.GetCategory(html.Substring(start, end - start).Trim()).GetNormalized();

				start = html.IndexOf("<a", start + 1, StringComparison.OrdinalIgnoreCase) + 1;
				start = html.IndexOf("<span", start + 1, StringComparison.OrdinalIgnoreCase);
				start = html.IndexOf(">", start + 1, StringComparison.OrdinalIgnoreCase) + 1;
				end = html.IndexOf("<", start + 1, StringComparison.OrdinalIgnoreCase);
				book.Author = Book.GetAuthor(html.Substring(start, end - start).Trim());

				bookself.Books.Add(book);
				start = html.IndexOf("<div class='ms_list_item'>", start + 1, StringComparison.OrdinalIgnoreCase);
			}

			if (onCompleted != null)
				onCompleted(bookself);

			return bookself;
		}

		public static async Task<BookSelf> GetBookSelf(string urlPattern, List<string> parameters, int currentPage, int totalPages, CancellationToken cancellationToken, Action<BookSelf> onCompleted, Action<BookSelf, Exception> onError)
		{
			BookSelf bookself = new BookSelf();
			bookself.UrlPattern = string.IsNullOrWhiteSpace(urlPattern) ? "http://isach.info/mobile/story.php?list=story&order=created_date&page={0}" : urlPattern.Trim();
			bookself.UrlParameters = parameters != null && parameters.Count > 0 ? parameters : new List<string>();
			bookself.UrlParameters.Add((currentPage > 1 ? currentPage : 1).ToString());
			bookself.CurrentPage = currentPage > 1 ? currentPage : 1;
			bookself.TotalPages = totalPages;

			return await ISach.GetBookSelf(bookself, cancellationToken, onCompleted, onError);
		}

		public static List<string> Categories
		{
			get
			{
				return new List<string>() { "kiem_hiep", "tien_hiep", "tuoi_hoc_tro", "co_tich", "truyen_ngan", "truyen_cuoi", "kinh_di", "khoa_hoc", "tuy_but", "tieu_thuyet", "ngon_tinh", "trinh_tham", "trung_hoa", "ky_nang_song", "nghe_thuat_song" };
			}
		}

		public static HashSet<string> LargeCategories
		{
			get
			{
				return new HashSet<string>() { "truyen_ngan", "truyen_cuoi", "tieu_thuyet", "nghe_thuat_song" };
			}
		}

		public static string ReferUri = "http://isach.info/mobile/index.php";
		#endregion

		#region Get details of a book
		public static async Task<Book> GetBook(string id, string uri, string folder, CancellationToken cancellationToken, 
																										Action<string> onProcess, Action<Book> onParsed, Action<Book> onCompleted, Action<Book, Exception> onError,
																										Action<string, List<string>> onChapterCompleted, Action<string, Exception> onChapterError,
																										Action<string, string> onDownloadFileCompleted, Action<string, Exception> onDownloadFileError,
																										int crawlMethod)
		{
			cancellationToken.ThrowIfCancellationRequested();
			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();

			// parse book
			Book book = null;
			try
			{
				book = await ISach.ParseBook(uri, cancellationToken);
				book.PermanentID = string.IsNullOrWhiteSpace(id) ? Utility.GetUUID() : id;
				if (string.IsNullOrWhiteSpace(book.Title) && onError != null)
				{
					onError(book, new InformationInvalidException("The book is invalid"));
					return null;
				}
				else if (onParsed != null)
					onParsed(book);
			}
			catch (Exception ex)
			{
				if (onError != null)
				{
					onError(book, ex);
					return null;
				}
				else
					throw ex;
			}

			// fetch chapters
			book = await ISach.FetchChapters(book, folder, cancellationToken, onProcess, onChapterCompleted, onChapterError, onDownloadFileCompleted, onDownloadFileError, crawlMethod);

			stopwatch.Stop();
			if (onProcess != null)
				onProcess("..... Total times for processing: " + stopwatch.GetElapsedTimes());

			// callback when done
			if (onCompleted != null)
				onCompleted(book);

			return book;
		}
		#endregion

		#region Parse a book (to get listing of all chapters)
		public static async Task<Book> ParseBook(string uri, CancellationToken cancellationToken)
		{
			// get identity
			string url = "/mobile/story.php?story=" + Book.GetIdentity(uri);

			Book book = new Book();
			book.Source = "isach.info";
			book.SourceUri = "http://isach.info" + url;

			string html = await Utility.GetWebPageAsync(book.SourceUri, ISach.ReferUri, Utility.SpiderUserAgent, cancellationToken);

			// check permission
			if (html.IndexOf("Để đọc tác phẩm này, được yêu cầu phải đăng nhập", StringComparison.OrdinalIgnoreCase) > 0)
				throw new InformationNotFoundException("Access denied: Để đọc tác phẩm này, được yêu cầu phải đăng nhập");

			// title
			int start = html.IndexOf("ms_title", StringComparison.OrdinalIgnoreCase);
			start = start < 0 ? -1 : html.IndexOf("<a", start + 1, StringComparison.OrdinalIgnoreCase);
			start = start < 0 ? -1 : html.IndexOf(">", start + 1, StringComparison.OrdinalIgnoreCase);
			int end = start < 0 ? -1 : html.IndexOf("<", start + 1, StringComparison.OrdinalIgnoreCase);
			if (start > 0 && end > 0)
				book.Title = html.Substring(start + 1, end - start - 1).GetNormalized();

			// author
			start = html.IndexOf("Tác giả:", StringComparison.OrdinalIgnoreCase);
			start = start < 0 ? -1 : html.IndexOf("<a", start + 1, StringComparison.OrdinalIgnoreCase);
			start = start < 0 ? -1 : html.IndexOf(">", start + 1, StringComparison.OrdinalIgnoreCase);
			end = start < 0 ? -1 : html.IndexOf("<", start + 1, StringComparison.OrdinalIgnoreCase);
			if (start > 0 && end > 0)
				book.Author = Book.GetAuthor(html.Substring(start + 1, end - start - 1).Trim());

			// category
			start = html.IndexOf("Thể loại:", StringComparison.OrdinalIgnoreCase);
			start = start < 0 ? -1 : html.IndexOf("<a", start + 1, StringComparison.OrdinalIgnoreCase);
			start = start < 0 ? -1 : html.IndexOf(">", start + 1, StringComparison.OrdinalIgnoreCase);
			end = start < 0 ? -1 : html.IndexOf("<", start + 1, StringComparison.OrdinalIgnoreCase);
			if (start > 0 && end > 0)
				book.Category = Book.GetCategory(html.Substring(start + 1, end - start - 1)).GetNormalized();

			// original
			start = html.IndexOf("Nguyên tác:", StringComparison.OrdinalIgnoreCase);
			end = start < 0 ? -1 : html.IndexOf("<", start + 1, StringComparison.OrdinalIgnoreCase);
			if (start > 0 && end > 0)
				book.Original = html.Substring(start + 11, end - start - 11).Trim().GetNormalized();

			// translator
			start = html.IndexOf("Dịch giả:", StringComparison.OrdinalIgnoreCase);
			start = start < 0 ? -1 : html.IndexOf("<a", start + 1, StringComparison.OrdinalIgnoreCase);
			start = start < 0 ? -1 : html.IndexOf(">", start + 1, StringComparison.OrdinalIgnoreCase);
			end = start < 0 ? -1 : html.IndexOf("<", start + 1, StringComparison.OrdinalIgnoreCase);
			if (start > 0 && end > 0)
				book.Translator = html.Substring(start + 1, end - start - 1).Trim().GetNormalized();

			// cover image
			start = html.IndexOf("ms_image", StringComparison.OrdinalIgnoreCase);
			start = start < 0 ? -1 : html.IndexOf("src='", start + 1, StringComparison.OrdinalIgnoreCase);
			end = start < 0 ? -1 : html.IndexOf("'", start + 5, StringComparison.OrdinalIgnoreCase);
			if (start > 0 && end > 0)
				book.Cover = "http://isach.info" + html.Substring(start + 5, end - start - 5).Trim();

			// chapters
			if (!book.Cover.Equals(""))
			{
				start = html.IndexOf("<a href='" + url, StringComparison.OrdinalIgnoreCase);
				end = start < 0 ? -1 : html.IndexOf("'", start + 9, StringComparison.OrdinalIgnoreCase);
				if (start > -1 && end > -1)
				{
					string tocUrl = "http://isach.info" + html.Substring(start + 9, end - start - 9).Trim();
					await Task.Delay(Utility.GetRandomNumber(123, 432));
					html = await Utility.GetWebPageAsync(tocUrl, url, Utility.SpiderUserAgent, cancellationToken);
				}
			}

			start = html.IndexOf("ms_chapter", StringComparison.OrdinalIgnoreCase);
			if (start < 0)
				start = html.IndexOf("<div id='c0000", StringComparison.OrdinalIgnoreCase);
			start = start < 0 ? -1 : html.IndexOf("<div", start + 1, StringComparison.OrdinalIgnoreCase);
			end = start < 0 ? -1 : html.IndexOf("</form>", start + 1, StringComparison.OrdinalIgnoreCase);

			if (start < 0 || end < 0)
			{
				List<string> contents = ISach.ParseChapter(html);
				book.Chapters.Add((!string.IsNullOrWhiteSpace(contents[0]) ? "<h1>" + contents[0] + "</h1>" + "\n" : "") + contents[1]);
			}
			else
			{
				html = html.Substring(start, end - start).Trim();
				start = html.IndexOf("<a href='", StringComparison.OrdinalIgnoreCase);
				while (start > -1)
				{
					end = html.IndexOf("'", start + 9, StringComparison.OrdinalIgnoreCase);
					string chapterUrl = html.Substring(start + 9, end - start - 9).Trim();
					while (chapterUrl.StartsWith("/"))
						chapterUrl = chapterUrl.Right(chapterUrl.Length - 1);
					chapterUrl = (!chapterUrl.StartsWith("http://isach.info") ? "http://isach.info/mobile/" : "") + chapterUrl;
					if (chapterUrl.IndexOf("&chapter=") < 0)
						chapterUrl += "&chapter=0001";

					book.Chapters.Add(chapterUrl);
					book.ChapterUrls.Add(chapterUrl);

					start = html.IndexOf(">", start + 1, StringComparison.OrdinalIgnoreCase) + 1;
					end = html.IndexOf("<", start + 1, StringComparison.OrdinalIgnoreCase);
					book.TOCs.Add(html.Substring(start, end - start).GetNormalized());

					start = html.IndexOf("<a href='", start + 1, StringComparison.OrdinalIgnoreCase);
				}
			}

			if (book.ChapterUrls.Count < 1 && (book.Chapters.Count < 1 || book.Chapters[0].Equals("")))
			{
				List<string> contents = ISach.ParseChapter(html);
				book.Chapters.Add((!string.IsNullOrWhiteSpace(contents[0]) ? "<h1>" + contents[0] + "</h1>" + "\n" : "") + contents[1]);
			}

			return book;
		}
		#endregion

		#region Fetch all chapters of a book
		public static async Task<Book> FetchChapters(Book book, string folder, CancellationToken cancellationToken, Action<string> onProcess, 
																										Action<string, List<string>> onChapterCompleted, Action<string, Exception> onChapterError,
																										Action<string, string> onDownloadFileCompleted, Action<string, Exception> onDownloadFileError,
																										int crawlMethod)
		{
			// fetch chapters
			Func<Task> fastCrawl = async () =>
			{
				int chaptersOfBigBook = 39;
				int normalDelayMin = 456, normalDelayMax = 1234;
				int mediumDelayMin = 2345, mediumDelayMax = 4321, longDelayMin = 3456, longDelayMax = 5678;

				int step = 7, start = 0;
				int end = start + step;

				bool isCompleted = false;
				while (!isCompleted)
				{
					List<Task> fetchingTasks = new List<Task>();
					for (int index = start; index < end; index++)
					{
						if (index >= book.Chapters.Count)
						{
							isCompleted = true;
							break;
						}

						string chapterUrl = book.ChapterUrls[index];
						if (chapterUrl.Equals("") || !chapterUrl.StartsWith("http://isach.info"))
							continue;

						string referUri = index > 0 && index < book.ChapterUrls.Count ? book.ChapterUrls[index - 1] : book.SourceUri;
						if (referUri.Equals(""))
							referUri = book.SourceUri;

						fetchingTasks.Add(Task.Run(async () =>
						{
							int delay = book.ChapterUrls.Count > chaptersOfBigBook
												? Utility.GetRandomNumber(mediumDelayMin, mediumDelayMax)
												: Utility.GetRandomNumber(normalDelayMin, normalDelayMax);
							await Task.Delay(delay, cancellationToken);

							try
							{
								List<string> contents = await ISach.GetChapter(chapterUrl, referUri, cancellationToken);
								int chapterIndex = book.ChapterUrls.IndexOf(chapterUrl);
								if (contents != null && (!contents[0].Equals("") || !contents[1].Equals("")))
								{
									string title = contents[0];
									if (string.IsNullOrWhiteSpace(title) && book.TOCs != null && book.TOCs.Count > chapterIndex)
									{
										title = book.GetTOCItem(chapterIndex);
										contents[0] = title;
									}
									book.Chapters[chapterIndex] = (!string.IsNullOrWhiteSpace(contents[0]) ? "<h1>" + contents[0] + "</h1>" : "")
																										 + (contents[1].Equals("") ? "--(empty)--" : contents[1]);
								}

								if (onChapterCompleted != null)
								{
									contents.Add(chapterIndex.ToString());
									contents.Add(book.Chapters.Count.ToString());
									onChapterCompleted(chapterUrl, contents);
								}
							}
							catch (Exception ex)
							{
								if (onChapterError != null)
									onChapterError(chapterUrl, ex);
							}
						}, cancellationToken));
					}
					await Task.WhenAll(fetchingTasks);

					// go next
					if (!isCompleted)
					{
						start += step;
						end += step;
						if (end <= book.Chapters.Count)
							await Task.Delay(Utility.GetRandomNumber(longDelayMin, longDelayMax), cancellationToken);
					}
				}
			};

			Func<Task> slowCrawl = async () =>
			{
				int chaptersOfLargeBook = 69, mediumPausePointOfLargeBook = 6, longPausePointOfLargeBook = 29;
				int chaptersOfBigBook = 29, mediumPausePointOfBigBook = 3, longPausePointOfBigBook = 14;
				int normalDelayMin = 456, normalDelayMax = 890, mediumDelay = 4321, longDelayOfBigBook = 7890, longDelayOfLargeBook = 15431;

				int chapterCounter = 0, totalChapters = 0;
				for (int index = 0; index < book.ChapterUrls.Count; index++)
					if (!book.ChapterUrls[index].Equals("") && book.ChapterUrls[index].StartsWith("http://isach.info"))
						totalChapters++;

				int chapterIndex = -1;
				while (chapterIndex < book.ChapterUrls.Count)
				{
					chapterIndex++;
					string chapterUrl = chapterIndex < book.ChapterUrls.Count ? book.ChapterUrls[chapterIndex] : "";
					if (chapterUrl.Equals("") || !chapterUrl.StartsWith("http://isach.info"))
						continue;

					int number = totalChapters > chaptersOfBigBook ? mediumPausePointOfLargeBook : mediumPausePointOfBigBook;
					int delay = chapterCounter > (number - 1) && chapterCounter % number == 0 ? mediumDelay : Utility.GetRandomNumber(normalDelayMin, normalDelayMax);
					if (totalChapters > chaptersOfLargeBook)
					{
						if (chapterCounter > longPausePointOfLargeBook && chapterCounter % (longPausePointOfLargeBook + 1) == 0)
						{
							if (onProcess != null)
								onProcess("\r\n" + "..... Wait for few seconds before continue with more chapters......." + "\r\n");
							delay = longDelayOfLargeBook;
						}
					}
					else if (totalChapters > chaptersOfBigBook)
					{
						if (chapterCounter > longPausePointOfBigBook && chapterCounter % (longPausePointOfBigBook + 1) == 0)
						{
							if (onProcess != null)
								onProcess("\r\n" + "..... Wait for few seconds before continue with more chapters......." + "\r\n");
							delay = longDelayOfBigBook;
						}
					}
					await Task.Delay(delay, cancellationToken);

					try
					{
						string referUri = chapterIndex > 0 && chapterIndex < book.ChapterUrls.Count ? book.ChapterUrls[chapterIndex - 1] : book.SourceUri;
						if (referUri.Equals(""))
							referUri = book.SourceUri;

						List<string> contents = await ISach.GetChapter(chapterUrl, referUri, cancellationToken);
						cancellationToken.ThrowIfCancellationRequested();

						if (contents != null && (!contents[0].Equals("") || !contents[1].Equals("")))
						{
							string title = contents[0];
							if (string.IsNullOrWhiteSpace(title) && book.TOCs != null && book.TOCs.Count > chapterIndex)
							{
								title = book.GetTOCItem(chapterIndex);
								contents[0] = title;
							}
							else if (book.TOCs != null && book.TOCs.Count > chapterIndex && book.TOCs[chapterIndex].IndexOf(title, StringComparison.OrdinalIgnoreCase) < 0)
								book.TOCs[chapterIndex] = title;
							book.Chapters[chapterIndex] = (!string.IsNullOrWhiteSpace(contents[0]) ? "<h1>" + contents[0] + "</h1>" : "")
																								 + (contents[1].Equals("") ? "--(empty)--" : contents[1]);
						}

						if (onChapterCompleted != null)
						{
							contents.Add((chapterIndex + 1).ToString());
							contents.Add(book.Chapters.Count.ToString());
							onChapterCompleted(chapterUrl, contents);
						}
					}
					catch (Exception ex)
					{
						if (onChapterError != null)
							onChapterError(chapterUrl, ex);
					}
					chapterCounter++;
				}
			};

			bool useFastMethod = crawlMethod.Equals((int)CrawMethods.Fast);
			if (!useFastMethod && !crawlMethod.Equals((int)CrawMethods.Slow))
				useFastMethod = Utility.GetRandomNumber() % 7 == 0;

			if (useFastMethod)
				await fastCrawl();
			else
				await slowCrawl();

			// download media files
			List<Task> downloadingTasks = new List<Task>();
			string folderPath = (string.IsNullOrWhiteSpace(folder) ? "" : folder + "\\") + Utils.MediaFolder;
			if (!string.IsNullOrWhiteSpace(book.Cover) && !book.Cover.StartsWith(Utils.MediaUri))
			{
				string filename = Utils.GetFilename(book.Cover);
				book.MediaFiles.Add(filename);

				string referUri = book.ChapterUrls.Count > 0 ? book.ChapterUrls[0] : ISach.ReferUri;
				if (referUri.IndexOf("&chapter=") > 0)
					referUri = referUri.Substring(0, referUri.IndexOf("&chapter="));
				downloadingTasks.Add(Utils.DownloadFileAsync(book.Cover, referUri, folderPath, book.PermanentID, cancellationToken, onDownloadFileCompleted, onDownloadFileError));

				book.Cover = Utils.MediaUri + filename;
			}

			for (int index = 0; index < book.Chapters.Count; index++)
			{
				object[] data = Utils.NormalizeMediaFiles(book.Chapters[index]);
				if (data == null || data.Length < 1)
					continue;

				book.Chapters[index] = data[0] as string;
				foreach (string fileUri in data[1] as List<string>)
				{
					if (fileUri.StartsWith(Utils.MediaUri))
						continue;

					string uri = (!fileUri.StartsWith("http://") ? "http://isach.info" : "") + (!fileUri.StartsWith("/") ? "/" : "") + fileUri;
					string filename = Utils.GetFilename(uri);
					if (book.MediaFiles.Contains(filename))
						continue;

					book.MediaFiles.Add(filename);
					downloadingTasks.Add(Utils.DownloadFileAsync(uri, ISach.ReferUri, folderPath, book.PermanentID, cancellationToken, onDownloadFileCompleted, onDownloadFileError));
				}
			}
			await Task.WhenAll(downloadingTasks);

			// normalize TOC
			book.NormalizeTOCs();

			// return information
			return book;
		}
		#endregion

		#region Get & Parse details of a chapter
		public static async Task<List<string>> GetChapter(string uri, string referUri, CancellationToken cancellationToken)
		{
			return ISach.ParseChapter(await Utility.GetWebPageAsync(uri.Replace("/mobile//", "/mobile/"), referUri, Utility.SpiderUserAgent, cancellationToken));
		}

		public static List<string> ParseChapter(string html)
		{
			int start = html.IndexOf("<div class='chapter_navigator'>", StringComparison.OrdinalIgnoreCase);
			if (start < 0)
				start = html.IndexOf("<div class='mobile_chapter_navigator'>", StringComparison.OrdinalIgnoreCase) > 0
							? html.IndexOf("<div class='mobile_chapter_navigator'>", StringComparison.OrdinalIgnoreCase)
							: html.IndexOf("<div id='story_detail'", StringComparison.OrdinalIgnoreCase);
			start = html.IndexOf("ms_chapter", start + 1, StringComparison.OrdinalIgnoreCase);
			start = start < 0 ? -1 : html.IndexOf(">", start + 1, StringComparison.OrdinalIgnoreCase);
			int end = start < 0 ? -1 : html.IndexOf("</div>", start + 1, StringComparison.OrdinalIgnoreCase);

			string title = (start > -1 && end > -1 ? html.Substring(start + 1, end - start - 1).Trim() : "").GetNormalized();
			while (title.IndexOf("  ") > -1)
				title = title.Replace("  ", " ");

			if (!title.Equals(""))
			{
				start = html.IndexOf("<div", start + 1, StringComparison.OrdinalIgnoreCase);
				if (title.IndexOf("<div id='dropcap", StringComparison.OrdinalIgnoreCase) > -1 || title.IndexOf("<div id ='dropcap", StringComparison.OrdinalIgnoreCase) > -1)
					title = "";
				else if (title.ToLower().Equals("null"))
					title = "";
			}
			else
			{
				start = html.IndexOf("<span class='dropcap", start + 1, StringComparison.OrdinalIgnoreCase);
				if (start < 0)
				{
					if (html.StartsWith("<div class='ms_text"))
						start = 0;
					else
					{
						start = html.IndexOf("ms_chapter", start + 1, StringComparison.OrdinalIgnoreCase) > 0
									? html.IndexOf("ms_chapter", start + 1, StringComparison.OrdinalIgnoreCase)
									: html.IndexOf("<div style='height: 50px;'></div>", end + 1, StringComparison.OrdinalIgnoreCase) < html.IndexOf("<div class='ms_text'>", end + 1, StringComparison.OrdinalIgnoreCase)
										? html.IndexOf("<div style='height: 50px;'></div>", end + 1, StringComparison.OrdinalIgnoreCase)
										: -1;
						start = start < 0 ? html.IndexOf("<div class='ms_text'>", end + 1, StringComparison.OrdinalIgnoreCase) : html.IndexOf("</div>", start + 1, StringComparison.OrdinalIgnoreCase) + 6;
					}
				}
			}

			end = html.IndexOf("<div style='height: 50px;'></div>", start + 1, StringComparison.OrdinalIgnoreCase);
			if (end < 0)
			{
				end = html.IndexOf("<div class='navigator_bottom'>", start + 1, StringComparison.OrdinalIgnoreCase);
				if (end < 0)
					end = html.IndexOf("<div class='mobile_chapter_navigator'>", start + 1, StringComparison.OrdinalIgnoreCase);
				if (end < 0)
					end = html.IndexOf("</form>", start + 1, StringComparison.OrdinalIgnoreCase);
			}

			string body = start > -1 && end > -1 ? html.Substring(start, end - start).Trim() : "";
			body = body.Replace(StringComparison.OrdinalIgnoreCase, "<div class='ms_text'>", "<p>").Replace(StringComparison.OrdinalIgnoreCase, "<div", "<p").Replace(StringComparison.OrdinalIgnoreCase, "</div>", "</p>");

			if (body.StartsWith("<span class='dropcap", StringComparison.OrdinalIgnoreCase))
				body = "<p>" + body;

			start = body.IndexOf("<p", StringComparison.OrdinalIgnoreCase);
			end = body.IndexOf("</p>", start + 1, StringComparison.OrdinalIgnoreCase);
			while (start > -1 && end > -1)
			{
				int dropcap = body.IndexOf("'dropcap", start + 1, StringComparison.OrdinalIgnoreCase);
				if (dropcap > -1 && dropcap < end)
				{
					string paragraph = body.Substring(start, end - start + 4);
					body = body.Remove(start, end - start + 4);

					string dropcapChar = "";
					dropcap = paragraph.IndexOf("class=");
					if (dropcap > 0)
					{
						dropcap += 7;
						dropcapChar = paragraph.Substring(dropcap - 1, 1);
						end = paragraph.IndexOf(dropcapChar, dropcap + 1);
						dropcapChar = paragraph.Substring(dropcap, end - dropcap);
						dropcapChar = dropcapChar[dropcapChar.Length - 1].ToString();
					}
					paragraph = Utility.RemoveTag(Utility.RemoveTag(paragraph, "p"), "span").Trim();
					if (paragraph.Equals(""))
						paragraph = dropcapChar;
					body = body.Insert(start, (body.StartsWith("<p>") ? "" : "<p>") + paragraph);
				}

				start = body.IndexOf("<p", start + 1, StringComparison.OrdinalIgnoreCase);
				end = body.IndexOf("</p>", start + 1, StringComparison.OrdinalIgnoreCase);
			}

			body = ISach.NormalizeBody(body.Replace(" \n", "").Replace("\r", "").Replace("\n", ""));

			if (title.Equals("")
				&& (body.StartsWith("<p>Quyển ", StringComparison.OrdinalIgnoreCase)
							|| body.StartsWith("<p>Phần ", StringComparison.OrdinalIgnoreCase) || body.StartsWith("<p>Chương ", StringComparison.OrdinalIgnoreCase)))
			{
				start = 0;
				end = body.IndexOf("</p>") + 4;
				title = Utility.RemoveTag(body.Substring(0, end - start), "p").Trim();
				body = body.Remove(0, end - start);
			}

			return new List<string>() { title, body };
		}
		#endregion

		#region Normalize body of a chapter
		public static string NormalizeBody(string input, int chapters)
		{
			string output = Utility.RemoveTag(input.Trim().Replace("�", "").Replace("''", "\"").Replace("\r", "").Replace("\n", "").Replace("\t", ""), "a");

			List<string[]> replacements = new List<string[]>()
			{
				new string[] { "<p> ", "<p>" },
				new string[] { "<p>-  ", "<p>- " },
				new string[] { " </p>", "</p>" },
				new string[] { "<p> ", "<p>" },
				new string[] { "<p>-  ", "<p>- " },
				new string[] { " </p>", "</p>" },
			};
			foreach (string[] replacement in replacements)
			{
				int counter = 0;
				while (counter < 1000 && output.IndexOf(replacement[0]) > 0)
				{
					output = output.Replace(replacement[0], replacement[1]);
					counter++;
				}
			}

			replacements = new List<string[]>()
			{
				new string[] { "<p class='ms_focus'>", "<p>" },
				new string[] { "<p class='ms_note'>", "<p>" },
				new string[] { "<p class='ms_end_note'>", "<p style='font-style:italic'>" },
				new string[] { "<p class='story_author'>", "<p style='font-style:italic'>" },
				new string[] { "<p class='story_poem'>", "<p style='font-style:italic;margin-left:10px'>" },
				new string[] { "<p class='ms_break'>o O o</p>", "<hr/>" },
				new string[] { "<p class='poem_paragraph_break'></p>", "" },
				new string[] { "<p>o0o</p>", "" },
				new string[] { "<p>o0o", "<p>" },
				new string[] { "<p class='ms_text_b'>", "<p style='font-weight:bold'>" },
				new string[] { "<p class='ms_quote'>", "<p style='margin-left:20px'>" },
				new string[] { "<p class='ms_image'>", "<p style='text-align:center'>" },
				new string[] { "<p><p>", "<p>" },
				new string[] { "</p></p>", "</p>" },
				new string[] { "<p></p>", "" },
			};
			foreach (string[] replacement in replacements)
			{
				output = output.Replace(StringComparison.OrdinalIgnoreCase, replacement[0], replacement[1]);
				if (replacement[0].IndexOf("'") > 0)
					output = output.Replace(StringComparison.OrdinalIgnoreCase, replacement[0].Replace("'", "\""), replacement[1]);
			}

			int start = output.IndexOf("<h1>", StringComparison.OrdinalIgnoreCase);
			int end = output.IndexOf("</h1>", start + 1, StringComparison.OrdinalIgnoreCase);
			if (start > -1 && end > start)
			{
				int dropcap = output.IndexOf("='dropcap", start + 1, StringComparison.OrdinalIgnoreCase);
				if (dropcap > start && end > dropcap)
					output = output.Remove(start, end - start + 5).Trim();
				else if (chapters.Equals(1))
					output = output.Replace(StringComparison.OrdinalIgnoreCase, "<h1>", "<p>").Replace(StringComparison.OrdinalIgnoreCase, "</h1>", "</p>");
			}

			return output.Trim();
		}

		public static string NormalizeBody(string input)
		{
			return ISach.NormalizeBody(input, -1);
		}
		#endregion

	}
}