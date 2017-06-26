#region Related components
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;

using Newtonsoft.Json.Linq;

using net.vieapps.Components.Utility;
#endregion

namespace net.vieapps.books.Components
{
	public static class VNThuQuan
	{

		#region Parse & Get information of a listing (bookself)
		public static BookSelf InitializeBookSelf(string folder)
		{
			string filePath = (string.IsNullOrWhiteSpace(folder) ? "" : folder + "\\") + "vnthuquan.net.status.json";
			JObject json = File.Exists(filePath)
										? JObject.Parse(Utility.ReadTextFile(filePath))
										: new JObject()
										{
											{ "TotalPages", 0 },
											{ "CurrentPage", 0 },
											{ "LastActivity", DateTime.Now },
										};

			BookSelf bookself = new BookSelf();
			bookself.TotalPages = Convert.ToInt32((json["TotalPages"] as JValue).Value);
			bookself.CurrentPage = Convert.ToInt32((json["CurrentPage"] as JValue).Value);

			if (bookself.TotalPages < 1)
			{
				bookself.CurrentPage = 0;
				bookself.TotalPages = 0;
				bookself.UrlPattern = "http://vnthuquan.net/mobil/?tranghientai={0}";
			}
			else if (bookself.CurrentPage >= bookself.TotalPages)
				bookself.UrlPattern = null;

			bookself.CurrentPage++;

			return bookself;
		}

		public static void FinaIizeBookSelf(BookSelf bookself, string folder)
		{
			string filePath = (string.IsNullOrWhiteSpace(folder) ? "" : folder + "\\") + "vnthuquan.net.status.json";
			JObject json = File.Exists(filePath)
										? JObject.Parse(Utility.ReadTextFile(filePath))
										: new JObject()
										{
											{ "TotalPages", 0 },
											{ "CurrentPage", 0 },
											{ "LastActivity", DateTime.Now },
										};

			if (bookself.TotalPages > 0)
				json["TotalPages"] = bookself.TotalPages;
			if (bookself.CurrentPage > 0)
				json["CurrentPage"] = bookself.CurrentPage;
			json["LastActivity"] = DateTime.Now;

			Utility.WriteTextFile(filePath, json.ToString(Newtonsoft.Json.Formatting.Indented));

			List<string> books = new List<string>();
			if (bookself.Books != null)
				for (int index = 0; index < bookself.Books.Count; index++)
					books.Add(bookself.Books[index].ToString(Newtonsoft.Json.Formatting.None));

			filePath = (string.IsNullOrWhiteSpace(folder) ? "" : folder + "\\") + "vnthuquan.net.json";
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
				html = await Utility.GetWebPageAsync(bookself.Url, VNThuQuan.ReferUri, Utility.SpiderUserAgent, cancellationToken);
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
				start = html.IndexOf("id='paging'", StringComparison.OrdinalIgnoreCase);
				start = start < 0 ? -1 : html.IndexOf(">", start + 1, StringComparison.OrdinalIgnoreCase) + 1;
				end = start < 0 ? -1 : html.IndexOf("</div>", start + 1, StringComparison.OrdinalIgnoreCase);
				if (start > 0 && end > 0)
				{
					string info = html.Substring(start, end - start), data = "";
					start = info.IndexOf("<a", StringComparison.OrdinalIgnoreCase);
					while (start > -1)
					{
						start = info.IndexOf("href='", start + 1, StringComparison.OrdinalIgnoreCase) + 6;
						end = info.IndexOf("'", start + 1, StringComparison.OrdinalIgnoreCase);
						data = info.Substring(start, end - start);
						start = info.IndexOf("<a", start + 1, StringComparison.OrdinalIgnoreCase);
					}
					data = data.Substring(data.IndexOf("=") + 1);
					bookself.TotalPages = data.Equals("#") ? bookself.CurrentPage : Convert.ToInt32(data);
				}
			}

			// books
			bookself.Books = new List<Book>();

			start = html.IndexOf("data-role=\"content\"", StringComparison.OrdinalIgnoreCase);
			start = start < 0 ? -1 : html.IndexOf("<ul", start + 1, StringComparison.OrdinalIgnoreCase);
			start = start < 0 ? -1 : html.IndexOf(">", start + 1, StringComparison.OrdinalIgnoreCase) + 1;
			end = start < 0 ? -1 : html.IndexOf("</ul>", start + 1, StringComparison.OrdinalIgnoreCase);
			html = start > 0 && end > 0 ? html.Substring(start, end - start) : "";

			start = html.IndexOf("<li", StringComparison.OrdinalIgnoreCase);
			while (start > -1)
			{
				Book book = new Book();
				book.Source = "vnthuquan.net";

				start = html.IndexOf("<a", start + 1, StringComparison.OrdinalIgnoreCase) + 1;
				start = html.IndexOf("href='", start + 1, StringComparison.OrdinalIgnoreCase) + 6;
				end = html.IndexOf("'", start + 1, StringComparison.OrdinalIgnoreCase);
				book.SourceUri = "http://vnthuquan.net/mobil/" + html.Substring(start, end - start).Trim();

				start = html.IndexOf("<p", start + 1, StringComparison.OrdinalIgnoreCase);
				start = html.IndexOf(">", start + 1, StringComparison.OrdinalIgnoreCase) + 1;
				end = html.IndexOf("<", start + 1, StringComparison.OrdinalIgnoreCase);
				book.Category = Book.GetCategory(html.Substring(start, end - start).Trim()).GetNormalized();

				start = html.IndexOf("<h2>", start + 1, StringComparison.OrdinalIgnoreCase) + 4;
				end = html.IndexOf("</h2>", start + 1, StringComparison.OrdinalIgnoreCase);
				book.Title = html.Substring(start, end - start).GetNormalized();
				if (book.Title.Equals(book.Title.ToUpper()))
					book.Title = book.Title.ToLower().GetNormalized();

				start = html.IndexOf("Tác giả:", start + 1, StringComparison.OrdinalIgnoreCase) + 8;
				end = html.IndexOf("<", start + 1, StringComparison.OrdinalIgnoreCase);
				string author = html.Substring(start, end - start).Trim();
				string[] excludeds = "Đồng tác giả|Dịch giả|Người dịch|Dịch viện|Chuyển ngữ|Dịch ra|Anh dịch|Dịch thuật|Bản dịch|Hiệu đính|Biên Tập|Biên soạn|đánh máy bổ sung|Nguyên tác|Nguyên bản|Dịch theo|Dịch từ|Theo bản|Biên dịch|Tổng Hợp|Tủ Sách|Sách Xuất Bản Tại|Chủ biên|Chủ nhiệm".Split('|');
				foreach (string excluded in excludeds)
				{
					int pos = author.IndexOf(excluded, StringComparison.OrdinalIgnoreCase);
					if (pos > -1)
						author = author.Remove(pos).GetNormalized();
				}
				book.Author = Book.GetAuthor(author);

				bookself.Books.Add(book);

				start = html.IndexOf("<li", start + 1, StringComparison.OrdinalIgnoreCase);
			}

			if (onCompleted != null)
				onCompleted(bookself);

			return bookself;
		}

		public static async Task<BookSelf> GetBookSelf(string url, List<string> parameters, int currentPage, int totalPages, CancellationToken cancellationToken, Action<BookSelf> onCompleted, Action<BookSelf, Exception> onError)
		{
			BookSelf bookself = new BookSelf();
			bookself.UrlPattern = string.IsNullOrWhiteSpace(url) ? "http://vnthuquan.net/mobil/?tranghientai={0}" : url.Trim();
			bookself.UrlParameters = parameters != null && parameters.Count > 0 ? parameters : new List<string>();
			bookself.UrlParameters.Add((currentPage > 1 ? currentPage : 1).ToString());
			bookself.TotalPages = totalPages;
			bookself.CurrentPage = currentPage > 1 ? currentPage : 1;

			return await VNThuQuan.GetBookSelf(bookself, cancellationToken, onCompleted, onError);
		}

		public static string ReferUri = "http://vnthuquan.net/mobil/";
		#endregion

		#region Get details of a book
		public static async Task<Book> GetBook(string id, string uri, string folder, CancellationToken cancellationToken, 
																										Action<string> onProcess, Action<Book> onParsed, Action<Book> onCompleted, Action<Book, Exception> onError,
																										Action<string, List<string>> onChapterCompleted, Action<string, Exception> onChapterError, 
																										Action<string, string> onDownloadFileCompleted, Action<string, Exception> onDownloadFileError,
																										int crawlMethod)
		{
			// parse book
			cancellationToken.ThrowIfCancellationRequested();
			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();

			Book book = null;
			try
			{
				book = await VNThuQuan.ParseBook(uri, cancellationToken);
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
			book = await VNThuQuan.FetchChapters(book, folder, cancellationToken, onProcess, onChapterCompleted, onChapterError, onDownloadFileCompleted, onDownloadFileError, crawlMethod);

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
			// get data
			Book book = new Book();
			book.Source = "vnthuquan.net";
			book.SourceUri = "http://vnthuquan.net/mobil/truyen.aspx?tid=" + Book.GetIdentity(uri);

			string html = await Utility.GetWebPageAsync(book.SourceUri, VNThuQuan.ReferUri, Utility.SpiderUserAgent, cancellationToken);

			// title & meta (author & category)
			int start = html.IndexOf("<div data-role=\"content\">", StringComparison.OrdinalIgnoreCase);
			start = html.IndexOf("<h3>", start, StringComparison.OrdinalIgnoreCase);
			int end = html.IndexOf("</h3>", start + 1, StringComparison.OrdinalIgnoreCase);
			if (end < 0)
				end = html.IndexOf("<h3>", start + 1, StringComparison.OrdinalIgnoreCase);

			if (start > 0 && end > 0)
			{
				string info = Utility.RemoveTag(html.Substring(start + 4, end - start - 4).Trim(), "span");
				start = info.IndexOf("<br>", StringComparison.OrdinalIgnoreCase);
				book.Category = Book.GetCategory(info.Substring(0, start)).GetNormalized();

				end = info.IndexOf("<br>", start + 1, StringComparison.OrdinalIgnoreCase);
				book.Title = info.Substring(start + 4, end - start - 4);
				book.Author = Book.GetAuthor(info.Substring(end + 4).Trim());

				string[] excludeds = "Đồng tác giả|Dịch giả|Người dịch|Dịch viện|Chuyển ngữ|Dịch ra|Anh dịch|Dịch thuật|Bản dịch|Hiệu đính|Biên Tập|Biên soạn|đánh máy bổ sung|Nguyên tác|Nguyên bản|Dịch theo|Dịch từ|Theo bản|Biên dịch|Tổng Hợp|Tủ Sách|Sách Xuất Bản Tại|Chủ biên|Chủ nhiệm".Split('|');
				foreach (string excluded in excludeds)
				{
					start = book.Title.IndexOf(excluded, StringComparison.OrdinalIgnoreCase);
					if (start > -1)
					{
						end = book.Title.IndexOf("<br>", start, StringComparison.OrdinalIgnoreCase);
						if (end < 0)
							end = book.Title.Length - 4;
						book.Title = book.Title.Remove(start, end - start + 4).Trim();
					}

					start = book.Author.IndexOf(excluded, StringComparison.OrdinalIgnoreCase);
					if (start > -1)
					{
						end = book.Author.IndexOf("<br>", start, StringComparison.OrdinalIgnoreCase);
						if (end < 0)
							end = book.Author.Length - 4;
						book.Author = book.Author.Remove(start, end - start + 4).Trim();
					}
				}

				book.Title = Utility.RemoveTag(book.Title, "br").GetNormalized();
				if (book.Title.Equals(book.Title.ToUpper()))
					book.Title = book.Title.ToLower().GetNormalized();
			}

			// chapters
			start = html.IndexOf("id=\"mucluc", StringComparison.OrdinalIgnoreCase);
			start = start < 0 ? -1 : html.IndexOf("<ul", start + 1, StringComparison.OrdinalIgnoreCase);
			start = start < 0 ? -1 : html.IndexOf(">", start + 1, StringComparison.OrdinalIgnoreCase);
			end = start < 0 ? -1 : html.IndexOf("</ul>", start + 1, StringComparison.OrdinalIgnoreCase);

			if (start > 0 && end > 0)
			{
				string info = html.Substring(start + 1, end - start - 1);
				start = info.IndexOf("<li", StringComparison.OrdinalIgnoreCase);
				while (start > -1)
				{
					start = info.IndexOf("<a", start + 1, StringComparison.OrdinalIgnoreCase);
					end = info.IndexOf("</a>", start + 1, StringComparison.OrdinalIgnoreCase);
					string data = info.Substring(start, end - start + 4);

					start = data.IndexOf("'");
					end = start < 0 ? -1 : data.IndexOf("'", start + 1);
					string chapterId = Book.GetIdentity(end < 0 ? "" : data.Substring(start + 1, end - start - 1));

					if (!chapterId.Equals(Book.GetIdentity(book.SourceUri)) || (chapterId.Equals(Book.GetIdentity(book.SourceUri)) && book.ChapterUrls.Count < 1))
					{
						string chapterUrl = "http://vnthuquan.net/mobil/noidung.aspx?tid=" + chapterId;
						book.Chapters.Add(chapterUrl);
						book.ChapterUrls.Add(chapterUrl);

						start = data.IndexOf(">") + 1;
						end = data.IndexOf("<", start + 1);
						if (start > -1 && end > start)
							book.TOCs.Add(data.Substring(start, end - start).GetNormalized());
					}

					start = info.IndexOf("</li>", StringComparison.OrdinalIgnoreCase);
					info = info.Remove(0, start + 5).Trim();
					start = info.IndexOf("<li", StringComparison.OrdinalIgnoreCase);
				}
			}
			else
			{
				string chapterUrl = "http://vnthuquan.net/mobil/noidung.aspx?tid=" + Book.GetIdentity(uri);
				book.Chapters.Add(chapterUrl);
				book.ChapterUrls.Add(chapterUrl);
			}

			return book;
		}
		#endregion

		#region Fetch all chapters of the book
		public static async Task<Book> FetchChapters(Book book, string folder, CancellationToken cancellationToken, Action<string> onProcess, 
																										Action<string, List<string>> onChapterCompleted, Action<string, Exception> onChapterError,
																										Action<string, string> onDownloadFileCompleted, Action<string, Exception> onDownloadFileError,
																										int crawlMethod)
		{
			// fetch chapters
			Func<Task> fastMethod = async () =>
			{
				List<Task> fetchingTasks = new List<Task>();
				for (int index = 0; index < book.Chapters.Count; index++)
				{
					string chapterUrl = book.ChapterUrls[index];
					if (!chapterUrl.StartsWith("http://vnthuquan.net"))
						continue;

					fetchingTasks.Add(Task.Run(async () =>
					{
						try
						{
							List<string> contents = await VNThuQuan.GetChapter(chapterUrl, book.SourceUri, cancellationToken);

							List<string> data = VNThuQuan.ParseChapter(contents);
							int chapterIndex = book.ChapterUrls.IndexOf(chapterUrl);

							if (data[0].Equals("") && data[1].Equals(""))
								book.Chapters[chapterIndex] = book.GetTOCItem(chapterIndex) + "--(empty)--";

							else if (!data[0].Equals("") || !data[1].Equals(""))
							{
								string title = data[0];
								if (string.IsNullOrWhiteSpace(title) && book.TOCs != null && book.TOCs.Count > chapterIndex)
								{
									title = book.GetTOCItem(chapterIndex);
									data[0] = title;
								}
								book.Chapters[chapterIndex] = (!string.IsNullOrWhiteSpace(data[0]) ? "<h1>" + data[0] + "</h1>" : "")
																									 + (data[1].Equals("") ? "--(empty)--" : data[1]);
							}

							if (string.IsNullOrWhiteSpace(book.Original))
								book.Original = VNThuQuan.GetOriginal(contents);

							if (string.IsNullOrWhiteSpace(book.Translator))
								book.Translator = VNThuQuan.GetTranslator(contents);

							if (string.IsNullOrWhiteSpace(book.Cover) || (contents != null && contents.Count > 1 && contents[1].IndexOf("=\"anhbia\"") > 0))
								book.Cover = VNThuQuan.GetCoverImage(contents);

							if (string.IsNullOrWhiteSpace(book.Credits))
								book.Credits = VNThuQuan.GetCredits(contents);

							if (onChapterCompleted != null)
							{
								data.Add((chapterIndex + 1).ToString());
								data.Add(book.Chapters.Count.ToString());
								onChapterCompleted(chapterUrl, data);
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
			};

			Func<Task> slowMethod = async () =>
			{
				int chapterIndex = -1;
				while (chapterIndex < book.ChapterUrls.Count)
				{
					chapterIndex++;
					string chapterUrl = chapterIndex < book.ChapterUrls.Count ? book.ChapterUrls[chapterIndex] : "";
					if (chapterUrl.Equals("") || !chapterUrl.StartsWith("http://vnthuquan.net"))
						continue;

					try
					{
						List<string> contents = await VNThuQuan.GetChapter(chapterUrl, book.SourceUri, cancellationToken);

						List<string> data = VNThuQuan.ParseChapter(contents);
						if (data[0].Equals("") && data[1].Equals(""))
							book.Chapters[chapterIndex] = "--(empty)--";
						else if (!data[0].Equals("") || !data[1].Equals(""))
						{
							string title = data[0];
							if (string.IsNullOrWhiteSpace(title) && book.TOCs != null && book.TOCs.Count > chapterIndex)
							{
								title = book.GetTOCItem(chapterIndex);
								data[0] = title;
							}
							book.Chapters[chapterIndex] = (!string.IsNullOrWhiteSpace(data[0]) ? "<h1>" + data[0] + "</h1>" : "")
																								 + (data[1].Equals("") ? "--(empty)--" : data[1]);
						}

						if (string.IsNullOrWhiteSpace(book.Original))
							book.Original = VNThuQuan.GetOriginal(contents);

						if (string.IsNullOrWhiteSpace(book.Translator))
							book.Translator = VNThuQuan.GetTranslator(contents);

						if (string.IsNullOrWhiteSpace(book.Cover) || (contents != null && contents.Count > 1 && contents[1].IndexOf("=\"anhbia\"") > 0))
							book.Cover = VNThuQuan.GetCoverImage(contents);

						if (string.IsNullOrWhiteSpace(book.Credits))
							book.Credits = VNThuQuan.GetCredits(contents);

						if (onChapterCompleted != null)
						{
							data.Add((chapterIndex + 1).ToString());
							data.Add(book.Chapters.Count.ToString());
							onChapterCompleted(chapterUrl, data);
						}
					}
					catch (Exception ex)
					{
						if (onChapterError != null)
							onChapterError(chapterUrl, ex);
					}
				}
			};

			bool useFastMethod = crawlMethod.Equals((int)CrawMethods.Fast);
			if (!useFastMethod && !crawlMethod.Equals((int)CrawMethods.Slow))
				useFastMethod = Utility.GetRandomNumber() % 7 == 0;
			
			if (useFastMethod)
				await fastMethod();
			else
				await slowMethod();

			// normalize paragraph that contain cover image
			if (!string.IsNullOrWhiteSpace(book.Cover) && book.Chapters[0].IndexOf(book.Cover) > 0)
			{
				int start = book.Chapters[0].IndexOf("<img", StringComparison.OrdinalIgnoreCase);
				int end = book.Chapters[0].IndexOf(">", start);
				book.Chapters[0] = book.Chapters[0].Remove(start, end - start + 1);
				book.Chapters[0] = book.Chapters[0].Replace("<p></p>", "").Replace(StringComparison.OrdinalIgnoreCase, "<p align=\"center\"></p>", "");
			}

			// download media files
			List<Task> downloadingTasks = new List<Task>();
			string folderPath = (string.IsNullOrWhiteSpace(folder) ? "" : folder + "\\") + Utils.MediaFolder;
			if (!string.IsNullOrWhiteSpace(book.Cover) && !book.Cover.StartsWith(Utils.MediaUri))
			{
				downloadingTasks.Add(Utils.DownloadFileAsync(book.Cover, book.SourceUri, folderPath, book.PermanentID, cancellationToken, onDownloadFileCompleted, onDownloadFileError));
				book.MediaFiles.Add(Utils.GetFilename(book.Cover));
				book.Cover = Utils.MediaUri + Utils.GetFilename(book.Cover);
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

					string uri = (!fileUri.StartsWith("http://") ? "http://vnthuquan.net" : "") + (!fileUri.StartsWith("/") ? "/" : "") + fileUri;
					string filename = Utils.GetFilename(uri);
					if (book.MediaFiles.Contains(filename))
						continue;

					book.MediaFiles.Add(filename);
					downloadingTasks.Add(Utils.DownloadFileAsync(fileUri, book.SourceUri, folderPath, book.PermanentID, cancellationToken, onDownloadFileCompleted, onDownloadFileError));
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
			string html = await Utility.GetWebPageAsync(uri, referUri, Utility.SpiderUserAgent, cancellationToken);
			string splitter = "--!!tach_noi_dung!!--";
			List<string> contents = new List<string>();
			int start = html.IndexOf(splitter, StringComparison.OrdinalIgnoreCase);
			while (start > 0)
			{
				contents.Add(html.Substring(0, start));
				html = html.Remove(0, start + splitter.Length);
				start = html.IndexOf(splitter, StringComparison.OrdinalIgnoreCase);
			}
			contents.Add(html);
			return contents;
		}

		public static List<string> ParseChapter(List<string> contents)
		{
			if (contents == null || contents.Count < 3)
				return null;

			string title = Utility.RemoveWhitespaces(contents[1].Trim()).Replace("\r", "").Replace("\n", "").Replace("\t", "");
			int start = title.IndexOf("<h4", StringComparison.OrdinalIgnoreCase);
			start = title.IndexOf(">", start + 1, StringComparison.OrdinalIgnoreCase);
			int end = title.IndexOf("</h4>", start + 1, StringComparison.OrdinalIgnoreCase);
			if (start > 0 && end > 0)
			{
				title = title.Substring(start + 1, end - start - 1).Trim().Replace("♦", " ");

				string[] excludeds = "Đồng tác giả|Dịch giả|Người dịch|Dịch viện|Chuyển ngữ|Dịch ra|Anh dịch|Dịch thuật|Bản dịch|Hiệu đính|Biên Tập|Biên soạn|đánh máy bổ sung|Nguyên tác|Nguyên bản|Dịch theo|Dịch từ|Theo bản|Biên dịch|Tổng Hợp|Tủ Sách|Tuyển tập|Sách Xuất Bản Tại|Chủ biên|Chủ nhiệm".Split('|');
				foreach (string excluded in excludeds)
				{
					start = title.IndexOf(excluded, StringComparison.OrdinalIgnoreCase);
					if (start > -1)
					{
						end = title.IndexOf("<br>", start, StringComparison.OrdinalIgnoreCase);
						if (end < 0)
							end = title.Length - 4;
						title = title.Remove(start, end - start + 4).Trim();
					}
				}

				while (title.StartsWith("<br>", StringComparison.OrdinalIgnoreCase))
					title = title.Substring(4).Trim();
				while (title.EndsWith("<br>", StringComparison.OrdinalIgnoreCase))
					title = title.Substring(0, title.Length - 4).Trim();

				title = title.Replace("<br>", ": ").Replace("<BR>", " - ").Trim();
			}

			title = Utility.ClearTag(title, "img").Trim();
			title = Utility.RemoveTag(title, "br").Trim();
			title = Utility.RemoveTag(title, "p").Trim();
			title = Utility.RemoveTag(title, "i").Trim();
			title = Utility.RemoveTag(title, "b").Trim();
			title = Utility.RemoveTag(title, "em").Trim();
			title = Utility.RemoveTag(title, "strong").Trim();

			while (title.IndexOf("  ") > 0)
				title = title.Replace("  ", " ");
			while (title.IndexOf("- -") > 0)
				title = title.Replace("- -", "-");
			while (title.IndexOf(": -") > 0)
				title = title.Replace(": -", ":");

			title = title.Trim().Replace("( ", "(").Replace(" )", ")").Replace("- (", "(").Replace(": :", ":").GetNormalized();

			while (title.StartsWith(")") || title.StartsWith("]"))
				title = title.Right(title.Length - 1).Trim();
			while (title.EndsWith("(") || title.EndsWith("["))
				title = title.Left(title.Length - 1).Trim();

			while (title.StartsWith(":"))
				title = title.Right(title.Length - 1).Trim();
			while (title.EndsWith(":"))
				title = title.Left(title.Length - 1).Trim();

			if (title.Equals(title.ToUpper()))
				title = title.ToLower().GetNormalized();

			string body = Utility.RemoveWhitespaces(contents[2].Trim()).Replace(StringComparison.OrdinalIgnoreCase, "\r", "").Replace(StringComparison.OrdinalIgnoreCase, "\n", "").Replace(StringComparison.OrdinalIgnoreCase, "\t", "");

			while (body.StartsWith("<div>", StringComparison.OrdinalIgnoreCase))
				body = body.Substring(5).Trim();
			while (body.EndsWith("</div>", StringComparison.OrdinalIgnoreCase))
				body = body.Substring(0, body.Length - 6).Trim();

			body = Utility.ClearTag(body, "script");
			body = Utility.ClearComments(body);
			body = Utility.RemoveMsOfficeTags(body);
			start = body.IndexOf("<?xml", StringComparison.OrdinalIgnoreCase);
			while (start > -1)
			{
				end = body.IndexOf(">", start);
				body = body.Remove(start, end - start + 1);
				start = body.IndexOf("<?xml", StringComparison.OrdinalIgnoreCase);
			}

			string[] headingTags = "h1|h2|h3|h4|h5|h6".Split('|');

			body = body.Replace(StringComparison.OrdinalIgnoreCase, "<DIV class=\"truyen_text\"></DIV></STRONG>", "</STRONG>\n<p>");
			body = body.Replace(StringComparison.OrdinalIgnoreCase, "<DIV class=\"truyen_text\"></DIV></EM>", "</EM>\n<p>");

			string[] otherTags = "strong|em|p|img".Split('|');
			foreach (string tag in otherTags)
				body = body.Replace(StringComparison.OrdinalIgnoreCase, "<" + tag, "<" + tag).Replace(StringComparison.OrdinalIgnoreCase, "</" + tag + ">", "</" + tag + ">");

			foreach (string tag in headingTags)
			{
				body = body.Replace(StringComparison.OrdinalIgnoreCase, "<" + tag + "><div class=\"truyen_text\"></div>", "<" + tag + "> ").Replace(StringComparison.OrdinalIgnoreCase, "<" + tag + "><div class=\"truyen_text\"> </div>", "<" + tag + ">");
				body = Utility.RemoveTagAttributes(body, tag);
			}

			body = body.Replace(StringComparison.OrdinalIgnoreCase, "<div class=\"truyen_text\"></div>", "</p><p>").Replace(StringComparison.OrdinalIgnoreCase, "<div class=\"truyen_text\"> </div>", "</p><p>");
			body = body.Replace(StringComparison.OrdinalIgnoreCase, "<div class=\"truyen_text\">", "<p>").Replace(StringComparison.OrdinalIgnoreCase, "<div", "<p").Replace(StringComparison.OrdinalIgnoreCase, "</div>", "</p>");
			body = body.Replace(StringComparison.OrdinalIgnoreCase, "</li></p>", "</li>").Replace(StringComparison.OrdinalIgnoreCase, "<p><li>", "<li>");
			body = body.Replace(StringComparison.OrdinalIgnoreCase, "<p></ul></p>", "</ul>").Replace(StringComparison.OrdinalIgnoreCase, "<p></ol></p>", "</ol>");

			body = body.Replace(StringComparison.OrdinalIgnoreCase, "<i class=\"calibre7\"", "<i").Replace(StringComparison.OrdinalIgnoreCase, "<img class=\"calibre1\"", "<img").Replace(StringComparison.OrdinalIgnoreCase, "<b class=\"calibre4\"", "<b");
			body = body.Replace(StringComparison.OrdinalIgnoreCase, "<p> <b>", "<p><b>").Replace(StringComparison.OrdinalIgnoreCase, ". </b>", ".</b> ").Replace(StringComparison.OrdinalIgnoreCase, ". </i>", ".</i> ");
			body = body.Replace(StringComparison.OrdinalIgnoreCase, "<p align=\"center\"> <", "<p align=\"center\"><").Replace(StringComparison.OrdinalIgnoreCase, "<p> <", "<p><").Replace(StringComparison.OrdinalIgnoreCase, "<p> ", "<p>");
			body = body.Replace(StringComparison.OrdinalIgnoreCase, "<p><p>", "<p>").Replace(StringComparison.OrdinalIgnoreCase, "</p></p>", "</p>").Replace(StringComparison.OrdinalIgnoreCase, ". </p> ", ".</p>");

			foreach (string tag in headingTags)
			{
				body = body.Replace(StringComparison.OrdinalIgnoreCase, "<" + tag + "> <", "<" + tag + "><").Replace(StringComparison.OrdinalIgnoreCase, "> </" + tag + ">", "></" + tag + ">");
				body = body.Replace(StringComparison.OrdinalIgnoreCase, "<" + tag + "></" + tag + ">", "").Replace(StringComparison.OrdinalIgnoreCase, "<" + tag + "> </" + tag + ">", "");
				body = body.Replace(StringComparison.OrdinalIgnoreCase, "<" + tag + "></p>", "<" + tag + ">").Replace(StringComparison.OrdinalIgnoreCase, "<p></" + tag + ">", "</" + tag + ">");
				body = body.Replace(StringComparison.OrdinalIgnoreCase, "<" + tag + "><strong>", "<" + tag + ">").Replace(StringComparison.OrdinalIgnoreCase, "</strong></" + tag + ">", "</" + tag + ">");
				body = body.Replace(StringComparison.OrdinalIgnoreCase, "<" + tag + "><em>", "<" + tag + ">").Replace(StringComparison.OrdinalIgnoreCase, "</em></" + tag + ">", "</" + tag + ">");
				body = body.Replace(StringComparison.OrdinalIgnoreCase, "<p><" + tag + ">", "<" + tag + ">").Replace(StringComparison.OrdinalIgnoreCase, "</" + tag + "></p>", "</" + tag + ">").Replace(StringComparison.OrdinalIgnoreCase, "<" + tag + "></p>", "");
			}

			foreach (string tag in headingTags)
			{
				body = body.Replace(StringComparison.OrdinalIgnoreCase, "<p><" + tag + ">", "<" + tag + ">").Replace(StringComparison.OrdinalIgnoreCase, "</" + tag + "></p>", "</" + tag + ">");
				start = body.IndexOf("<" + tag + ">", StringComparison.OrdinalIgnoreCase);
				while (start > -1)
				{
					end = body.IndexOf("</" + tag + ">", start + 1, StringComparison.OrdinalIgnoreCase);
					string heading = body.Substring(start + 4, end - start - 4);
					body = body.Remove(start, end - start + 5);

					int pos = heading.IndexOf("<");
					while (pos > -1)
					{
						end = heading.IndexOf(">", pos);
						if (end > 0)
							heading = heading.Remove(pos, end - pos + 1);
						pos = heading.IndexOf("<");
					}
					body = body.Insert(start, "<" + tag + ">" + heading + "</" + tag + ">");
					start = body.IndexOf("<" + tag + ">", start + 1, StringComparison.OrdinalIgnoreCase);
				}
			}

			start = body.IndexOf("<p id=\"chuhoain\"", StringComparison.OrdinalIgnoreCase);
			while (start > -1)
			{
				end = body.IndexOf("</span><p>", start, StringComparison.OrdinalIgnoreCase);
				int img = body.IndexOf("<img", start, StringComparison.OrdinalIgnoreCase);
				if (start > -1 && end > start && img > start)
				{
					int imgStart = body.IndexOf("src=\"", img, StringComparison.OrdinalIgnoreCase) + 5, imgEnd = -1;
					if (imgStart < 0)
					{
						imgStart = body.IndexOf("src='", img, StringComparison.OrdinalIgnoreCase) + 5;
						imgEnd = body.IndexOf("'", imgStart);
					}
					else
						imgEnd = body.IndexOf("\"", imgStart);
					string imgChar = body.Substring(imgStart, imgEnd - imgStart);
					body = body.Remove(start, end - start + 10);
					body = body.Insert(start, "<p>" + VNThuQuan.GetImageCharacter(imgChar));
				}
				start = body.IndexOf("<p id=\"chuhoain\"", start + 1, StringComparison.OrdinalIgnoreCase);
			}

			start = body.IndexOf("<img", StringComparison.OrdinalIgnoreCase);
			while (start > -1)
			{
				end = body.IndexOf(">", start + 1, StringComparison.OrdinalIgnoreCase);
				int img = body.IndexOf("src=\"http://vnthuquan.net/userfiles/images/chu%20cai/cotich", start);
				if (img < 0)
					img = body.IndexOf("src='http://vnthuquan.net/userfiles/images/chu%20cai/cotich", start);
				if (img > -1 && end > img)
				{
					end = body.IndexOf("\"", img + 5, StringComparison.OrdinalIgnoreCase);
					if (end < 0)
						end = body.IndexOf("'", img + 5, StringComparison.OrdinalIgnoreCase);
					string imgChar = body.Substring(img + 5, end - img + 5);
					end = body.IndexOf("<p>", start, StringComparison.OrdinalIgnoreCase);
					if (end < 0)
						end = body.IndexOf(">", start, StringComparison.OrdinalIgnoreCase) + 1;
					else
						end += 3;
					string str = body.Substring(start, end - start);
					body = body.Remove(start, end - start);
					body = body.Insert(start, VNThuQuan.GetImageCharacter(imgChar));
				}
				start = body.IndexOf("<img", start + 1, StringComparison.OrdinalIgnoreCase);
			}

			if (body.Equals("</p><p>"))
				body = "";
			else
			{
				body = VNThuQuan.NormalizeBody(body);
				body = body.Replace(StringComparison.OrdinalIgnoreCase, "<h1>", "<h2>").Replace(StringComparison.OrdinalIgnoreCase, "</h1>", "</h2>");
			}

			return new List<string>() { title, body };
		}

		internal static string GetImageCharacter(string imgChar)
		{
			int start = imgChar.IndexOf("_");
			if (start < 0)
				return "";
			int end = imgChar.IndexOf(".", start);
			if (end < 0)
				return "";

			string @char = imgChar.Substring(start + 1, end - start - 1).ToUpper();
			if (@char.Equals("DD"))
				@char = "Đ";
			else if (@char.Equals("AA"))
				@char = "Â";
			else if (@char.Equals("AW"))
				@char = "Ă";
			else if (@char.Equals("EE"))
				@char = "Ê";
			else if (@char.Equals("OW"))
				@char = "Ơ";
			else if (@char.Equals("OO"))
				@char = "Ô";
			return @char;
		}

		internal static string GetValueOfTitle(List<string> contents, string[] indicators)
		{
			if (contents == null || contents.Count < 3)
				return "";

			string title = Utility.RemoveWhitespaces(contents[1].Trim()).Replace(StringComparison.OrdinalIgnoreCase, "\r", "").Replace(StringComparison.OrdinalIgnoreCase, "\n", "").Replace(StringComparison.OrdinalIgnoreCase, "\t", "");
			int start = title.IndexOf("<h4");
			start = title.IndexOf(">", start + 1);
			int end = title.IndexOf("</h4>", start + 1);
			if (start < 0 || end < 0)
				return "";

			title = title.Substring(start + 1, end - start - 1).Trim();

			foreach (string indicator in indicators)
			{
				start = title.IndexOf(indicator, StringComparison.OrdinalIgnoreCase);
				if (start > -1)
				{
					end = title.IndexOf("<br", start, StringComparison.OrdinalIgnoreCase);
					if (end < 0)
						end = title.Length;
					break;
				}
			}

			if (start < 0)
				return "";

			title = title.Substring(start, end - start).Trim();
			start = title.IndexOf(":");
			if (start > 0)
				title = title.Substring(start + 1).Trim();
			while (title.StartsWith(":"))
				title = title.Substring(1).Trim();

			return title.GetNormalized();
		}

		internal static string GetTranslator(List<string> contents)
		{
			return VNThuQuan.GetValueOfTitle(contents, "Dịch giả|Người dịch|Dịch viện|Chuyển ngữ|Dịch ra|Anh dịch|Dịch thuật|Biên dịch".Split('|')).ToLower().GetNormalized();
		}

		internal static string GetOriginal(List<string> contents)
		{
			return VNThuQuan.GetValueOfTitle(contents, "Nguyên tác|Dịch theo|Dịch từ|Theo bản".Split('|')).ToLower().GetNormalized();
		}

		internal static string GetCoverImage(List<string> contents)
		{
			if (contents == null || contents.Count < 2)
				return "";

			string data = contents[1];
			int start = data.IndexOf("<img", StringComparison.OrdinalIgnoreCase);
			if (start < 0 && contents.Count > 2)
			{
				data = contents[2];
				start = data.IndexOf("<img", StringComparison.OrdinalIgnoreCase);
			}
			if (start < 0)
				return "";

			start = data.IndexOf("src=\"", start + 1, StringComparison.OrdinalIgnoreCase);
			start = data.IndexOf("\"", start + 1);
			int end = data.IndexOf("\"", start + 1);
			return start > 0 && end > 0 ? data.Substring(start + 1, end - start - 1) : "";
		}

		internal static string GetCredits(List<string> contents)
		{
			if (contents == null || contents.Count < 4 || contents[3].IndexOf("<p") < 0)
				return "";

			int start = contents[3].IndexOf("<p");
			int end = contents[3].IndexOf("</p>");
			if (start < 0 || end < 0)
				return "";

			string space = "&nbsp;".HtmlDecode();
			string credits = Utility.RemoveTagAttributes(contents[3].Substring(start, end - start + 4).Trim(), "p");
			credits = Utility.RemoveWhitespaces(credits.Replace(StringComparison.OrdinalIgnoreCase, "<br>", " ").Replace("\r", "").Replace("\n", "").Replace("\t", ""));
			while (credits.IndexOf(space + space) > 0)
				credits = credits.Replace(space + space, " ");
			while (credits.IndexOf("  ") > 0)
				credits = credits.Replace("  ", " ");
			credits = credits.Replace(StringComparison.OrdinalIgnoreCase, "</p><p>","</p>\n<p>").Trim();
			credits = credits.Replace("<p> ", "<p>").Replace(" :", ":");
			credits = credits.Replace("&", "&amp;").Replace("&amp;amp;", "&amp;");
			return credits;
		}
		#endregion

		#region Normalize body of a chapter
		public static string NormalizeBody(string input)
		{
			string output = Utility.RemoveTag(input.Trim().Replace("\r", "").Replace("\n", "").Replace("\t", ""), "a");
			output = output.Replace(StringComparison.OrdinalIgnoreCase, "<em", "<i").Replace(StringComparison.OrdinalIgnoreCase, "</em>", "</i>");
			output = output.Replace(StringComparison.OrdinalIgnoreCase, "<I", "<i").Replace(StringComparison.OrdinalIgnoreCase, "</I>", "</i>");
			output = output.Replace(StringComparison.OrdinalIgnoreCase, "<strong", "<b").Replace(StringComparison.OrdinalIgnoreCase, "</strong>", "</b>");
			output = output.Replace(StringComparison.OrdinalIgnoreCase, "<B", "<b").Replace(StringComparison.OrdinalIgnoreCase, "</b>", "</b>");
			output = output.Replace(StringComparison.OrdinalIgnoreCase, "<U", "<u").Replace(StringComparison.OrdinalIgnoreCase, "</u>", "</u>");

			string[] formattingTags = "b|i|u".Split('|');
			output = Utility.RemoveMsOfficeTags(output);
			output = Utility.RemoveTagAttributes(output, "p");
			foreach (string tag in formattingTags)
				output = Utility.RemoveTagAttributes(output, tag);

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

			string[] symbols = ".|,|!|?|;|:".Split('|');
			foreach (string tag in formattingTags)
			{
				output = output.Replace("<" + tag + "><" + tag + ">", "<" + tag + ">").Replace("</" + tag + "></" + tag + ">", "</" + tag + ">");
				output = output.Replace("<" + tag + "></" + tag + ">", "").Replace("<" + tag + "> </" + tag + ">", "");
				foreach (string symbol in symbols)
				{
					output = output.Replace("</" + tag + ">" + symbol + "</p>", symbol + "</" + tag + "></p>");
					output = output.Replace(symbol + "</" + tag + ">", symbol + "</" + tag + "> ");
					output = output.Replace("</" + tag + ">" + symbol, "</" + tag + ">" + symbol + " ");
				}
			}

			foreach (string[] replacement in replacements)
			{
				int counter = 0;
				while (counter < 100 && output.IndexOf(replacement[0]) > 0)
				{
					output = output.Replace(replacement[0], replacement[1]);
					counter++;
				}
			}

			int start = -1, end = -1;
			if (!output.StartsWith("<p>"))
			{
				start = output.IndexOf("<p>", StringComparison.OrdinalIgnoreCase);
				end = output.IndexOf("</p>", StringComparison.OrdinalIgnoreCase);
				if (start > end)
					output = "<p>" + output;
			}

			output += !output.EndsWith("</p>") ? "</p>" : "";

			replacements = new List<string[]>();
			string[] beRemoved = "<p></p>|<p class='msg signature'></p>|<p><p align='center'></p>|<p align='left'></p>|<p style='text-align: left;'></p>|<p align='center'></p>|<p style='text-align: center;'></p>|<p align='right'></p>|<p style='text-align: right;'></p>|<p>.</p>|<h2>HẾT</h2>|<strong>HẾT</strong>".Split('|');
			foreach (string removed in beRemoved)
				replacements.Add(new string[] { removed, "" });
			foreach (string[] replacement in replacements)
			{
				output = output.Replace(StringComparison.OrdinalIgnoreCase, replacement[0], replacement[1]);
				if (replacement[0].IndexOf("'") > 0)
					output = output.Replace(StringComparison.OrdinalIgnoreCase, replacement[0].Replace("'", "\""), replacement[1]);
			}

			string[] headingTags = "h1|h2|h3|h4|h5|h6".Split('|');
			foreach (string tag in headingTags)
				output = output.Replace(StringComparison.OrdinalIgnoreCase, "<p><" + tag + ">", "<" + tag + ">").Replace(StringComparison.OrdinalIgnoreCase, "</" + tag + "></p>", "</" + tag + ">");

			output = VNThuQuan.ReformatParagraphs(output);
			output += !output.EndsWith("</p>") ? "</p>" : "";
			output = output.Replace("<p><p>", "<p>").Replace("</p></p>", "</p>").Replace("<p></p>", "");
			output = output.Replace(StringComparison.OrdinalIgnoreCase, "<p class=\"hr\"></p>", "<hr/>").Replace("<p><b><i></b></i></p>", "");

			return output.Trim();
		}

		public static string ReformatParagraphs(string input)
		{
			string output = input.Trim();
			string[] formattingTags = "b|i|u".Split('|');
			int start = -1, end = -1;

			foreach (string tag in formattingTags)
			{
				start = output.IndexOf("<" + tag + ">");
				while (start > -1)
				{
					end = output.IndexOf("</" + tag + ">", start + 1);
					if (end > 0)
					{
						string paragraph = output.Substring(start, end - start + 3 + tag.Length);
						if (paragraph.IndexOf("<p>") > 0)
						{
							paragraph = paragraph.Replace("</p>", "</" + tag + "></p>").Replace("<p>", "<p><" + tag + ">");
							output = output.Remove(start, end - start + 3 + tag.Length);
							output = output.Insert(start, paragraph);
						}
					}
					else
					{
						end = output.IndexOf("</p>", start + 1);
						if (end > 0)
							output = output.Insert(end, "</" + tag + ">");
					}

					start = output.IndexOf("<" + tag + ">", start + 1);
				}
			}

			start = output.IndexOf("<p");
			while (start > -1)
			{
				end = output.IndexOf("</p>", start + 1);
				if (end > start)
				{
					string paragraph = output.Substring(start, end - start + 4);
					try
					{
						paragraph = VNThuQuan.ReformatParagraph(paragraph);
					}
					catch { }
					output = output.Remove(start, end - start + 4);
					output = output.Insert(start, paragraph);
				}

				start = start + 1 < output.Length ? output.IndexOf("<p", start + 1) : -1;
			}

			return output;
		}

		static string ReformatParagraph(string input)
		{
			string output = Utility.RemoveTag(input, "span").Trim();
			if (output.Equals("") || output.Equals("<p></p>"))
				return "";

			int start = output.IndexOf(">") + 1;
			int end = output.IndexOf("</p>", start + 1);
			if (end < start)
				start = 3;
			output = output.Substring(start, end - start).Trim();

			string[] formattingTags = "b|i|u".Split('|');
			foreach (string tag in formattingTags)
			{
				if (output.IndexOf("<" + tag + ">") == 1)
				{
					string @char = output.Left(1);
					output = output.Right(output.Length - 1).Insert(tag.Length + 2, @char);
				}

				start = output.IndexOf("<" + tag + ">");
				if (start < 0)
				{
					if (output.IndexOf("</" + tag + ">") > 0)
						output = "<" + tag + ">" + output;
				}
				else
					while (start > -1)
					{
						int next = output.IndexOf("<" + tag + ">", start + 1);
						end = output.IndexOf("</" + tag + ">", start + 1);
						if (end < 0)
						{
							if (next < 0)
								output += "</" + tag + ">";
							else
								output = output.Insert(next, "</" + tag + ">");
						}
						else if (next > 0 && next < end)
							output = output.Insert(next, "</" + tag + ">");

						start = output.IndexOf("<" + tag + ">", start + 1);
					}
			}

			return "<p>" + output.Trim() + "</p>";
		}
		#endregion

	}
}