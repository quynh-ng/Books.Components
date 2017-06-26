#region Related components
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

using Newtonsoft.Json.Linq;

using net.vieapps.Components.Utility;
#endregion

namespace net.vieapps.books.Components
{
	public static class Tiki
	{

		public static string ReferUri = "https://tiki.vn/";
		public static string Filename = "tiki.vn";

		#region Initialize bookself
		public static BookSelf InitializeBookSelf(string folder)
		{
			string filePath = (string.IsNullOrWhiteSpace(folder) ? "crawls" : folder + "\\") + Tiki.Filename + ".status.json";
			JObject json = File.Exists(filePath)
										? JObject.Parse(Utility.ReadTextFile(filePath))
										: new JObject()
										{
											{ "TotalPages", 0 },
											{ "CurrentPage", 0 },
											{ "Category",  0 },
											{ "LastActivity", DateTime.Now },
										};

			BookSelf bookself = new BookSelf();
			bookself.TotalPages = Convert.ToInt32((json["TotalPages"] as JValue).Value);
			bookself.CurrentPage = Convert.ToInt32((json["CurrentPage"] as JValue).Value);
			bookself.CategoryIndex = Convert.ToInt32(json["Category"]);

			if (bookself.TotalPages < 1)
				bookself.CurrentPage = 0;

			else if (bookself.CurrentPage >= bookself.TotalPages)
			{
				bookself.CategoryIndex++;
				bookself.CurrentPage = 0;
				bookself.TotalPages = 0;
			}

			bookself.CurrentPage++;
			bookself.UrlPattern = bookself.CategoryIndex < Crawler.Categories.Count ? (Crawler.Categories[bookself.CategoryIndex]["URL"] as JValue).Value.ToString() + "{0}" : null;
			bookself.UrlParameters = new List<string>();
			bookself.UrlParameters.Add("?page=" + bookself.CurrentPage);

			return bookself;
		}
		#endregion

		#region Finalize bookself
		public static void FinaIizeBookSelf(BookSelf bookself, string folder)
		{
			Crawler.SaveCrawledBooks(folder);
			Crawler.SaveMissingBooks(folder);

			string filePath = (string.IsNullOrWhiteSpace(folder) ? "" : folder + "\\") + Tiki.Filename + ".status.json";
			JObject json = File.Exists(filePath)
										? JObject.Parse(Utility.ReadTextFile(filePath))
										: new JObject()
										{
											{ "TotalPages", 0 },
											{ "CurrentPage", 0 },
											{ "Category", 0 },
											{ "LastActivity", DateTime.Now },
										};

			if (bookself.TotalPages > 0)
				json["TotalPages"] = bookself.TotalPages;
			if (bookself.CurrentPage > 0)
				json["CurrentPage"] = bookself.CurrentPage;

			json["Category"] = bookself.CategoryIndex;
			json["LastActivity"] = DateTime.Now;

			Utility.WriteTextFile(filePath, json.ToString(Newtonsoft.Json.Formatting.Indented));

			filePath = (string.IsNullOrWhiteSpace(folder) ? "" : folder + "\\") + Tiki.Filename + ".json";
			//Utility.WriteTextFile(filePath, bookself.Books.ToJArray().ToString(Newtonsoft.Json.Formatting.Indented));

			List<string> books = new List<string>();
			if (bookself.Books != null)
				for (int index = 0; index < bookself.Books.Count; index++)
					books.Add(bookself.Books[index].ToString(Newtonsoft.Json.Formatting.None));

			filePath = (string.IsNullOrWhiteSpace(folder) ? "" : folder + "\\") + Tiki.Filename + ".json";
			Utility.WriteTextFile(filePath, books);
		}
		#endregion

		#region Get bookself and get details of all books in the bookself
		public static async Task<BookSelf> GetBookSelf(BookSelf bookself, string mediafilesFolder, int crawlMethod, CancellationToken cancellationToken, Action<string> onProcess, Action<BookSelf> onCompleted, Action<BookSelf, Exception> onError)
		{
			// get data
			cancellationToken.ThrowIfCancellationRequested();

			bookself.Url = bookself.UrlPattern;
			if (bookself.UrlParameters.Count > 0)
				for (int index = 0; index < bookself.UrlParameters.Count; index++)
					bookself.Url = bookself.Url.Replace("{" + index + "}", bookself.UrlParameters[index]);

			if (onProcess != null)
				onProcess("Start to crawl data [" + bookself.Url + "]" + "\r\n");

			string html = "";
			try
			{
				html = await Utility.GetWebPageAsync(bookself.Url, Tiki.ReferUri, Utility.DesktopUserAgent, cancellationToken);
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

			// total pages
			start = html.PositionOf("<div class=\"list-pager");
			start = start < 0 ? -1 : html.PositionOf("<ul>", start + 1);
			end = start < 0 ? -1 : html.PositionOf("</ul>", start + 1);
			if (start > 0 && end > 0)
			{
				string info = html.Substring(start + 4, end - start - 4), page = "";
				start = info.IndexOf("<li");
				while (start > -1)
				{
					end = info.IndexOf("<li", start + 1);
					if (end < 0)
						end = info.Length - 1;
					string pager = info.Substring(start, end - start);
					info = info.Remove(0, end);
					if (pager.PositionOf("class=\"normal") > 0)
						page = Utility.RemoveTag(pager.Substring(pager.IndexOf("<a")), "a");
					start = info.IndexOf("<li");
				}
				try
				{
					bookself.TotalPages = Convert.ToInt32(page);
				}
				catch
				{
					bookself.TotalPages = 1;
				}
			}
			else
				bookself.TotalPages = 1;

			// books
			bookself.Books = new List<Book>();

			start = html.PositionOf("product-box-list");
			start = start < 0 ? -1 : html.PositionOf(">", start + 1) + 1;
			end = start < 0 ? -1 : html.PositionOf("<div class=\"list-pager", start + 1);
			html = start > 0 && end > 0 ? html.Substring(start, end - start) : "";

			start = html.PositionOf("<a");
			while (start > -1)
			{
				Book book = new Book();

				start = html.PositionOf("href=\"", start + 1) + 6;
				end = html.PositionOf("\"", start + 1);
				book.SourceUri = html.Substring(start, end - start).Trim();
				if (book.SourceUri.PositionOf("?") > 0)
					book.SourceUri = book.SourceUri.Substring(0, book.SourceUri.IndexOf("?"));

				start = html.PositionOf("title=\"", start + 1) + 7;
				end = html.PositionOf("\"", start + 1);
				book.Title = html.Substring(start, end - start).Trim();

				bool bypass = book.Title.StartsWith("combo", StringComparison.OrdinalIgnoreCase) || book.Title.StartsWith("boxset:", StringComparison.OrdinalIgnoreCase)
											|| book.Title.StartsWith("hộp sách", StringComparison.OrdinalIgnoreCase) || book.Title.StartsWith("hộp", StringComparison.OrdinalIgnoreCase)
											|| (book.Title.PositionOf("(tập") > 0 && book.Title.PositionOf("đến tập") > 0)
											|| (book.Title.PositionOf("(tập") > 0 && book.Title.PositionOf("- tập") > 0);

				if (!bypass)
				{
					int eposides = 1;
					try
					{
						eposides = Crawler.GetPaperBookEposides(book.Title);
					}
					catch { }
					book.ExtraData.Add(new JProperty("Eposides", eposides));
					book.Title = Crawler.GetPaperBookTitle(book.Title);
					bookself.Books.Add(book);
				}

				start = html.PositionOf("<a", start + 1);
			}

			if (onProcess != null)
				onProcess("The listing of books is parsed, now start to crawl details of " + bookself.Books.Count + " books" + "\r\n");

			// get details of all books and update final collection of the book
			List<Book> finalBooks = new List<Book>();
			string category = (Crawler.Categories[bookself.CategoryIndex]["Name"] as JValue).Value.ToString();
			string tags = (Crawler.Categories[bookself.CategoryIndex]["Tags"] as JValue).Value.ToString();

			Action<string, Exception> onDownloadError = (url, ex) =>
			{
				if (onProcess != null && !(ex is OperationCanceledException))
					onProcess("\r\n" + "------ Error occurred while downlading file [" + url + "]" + "\r\n" + ex.Message + "\r\n" + "Stack: " + ex.StackTrace + "\r\n\r\n");
			};

			Func<int, Task> getDetails = async (index) =>
			{
				cancellationToken.ThrowIfCancellationRequested();
				Book book = bookself.Books[index];
				book.Category = category;
				book.Tags = tags;

				try
				{
					await Task.Delay(Utility.GetRandomNumber(123, 345), cancellationToken);
					html = await Utility.GetWebPageAsync(book.SourceUri, bookself.Url, Utility.DesktopUserAgent, cancellationToken);
					cancellationToken.ThrowIfCancellationRequested();

					// cover image
					string coverImageUri = null;

					start = html.PositionOf("<div class=\"thumb");
					int pos = html.PositionOf("data-zoom-image=", start + 1);

					start = html.PositionOf("<a", start + 1);
					end = html.PositionOf("</a>", start + 1);
					if (start > 0 && end > start && pos > start && pos < end)
						start = pos;
					else
					{
						start = html.PositionOf("id=\"product-magiczoom");
						end = start < 0 ? -1 : html.PositionOf("</div>", start + 1);
						if (start > 0 && end > start)
						{
							if (html.PositionOf("<img", start + 1) > 0)
							{
								start = html.PositionOf("<img", start + 1);
								start = html.PositionOf("src=", start + 1);
							}
							else
								start = html.PositionOf("src=", start + 1);
						}
					}

					start = start < 0 ? -1 : html.PositionOf("\"", start + 1) + 1;
					end = start < 1 ? -1 : html.PositionOf("\"", start + 1);
					if (start > 0 && end > start)
					{
						coverImageUri = html.Substring(start, end - start).Replace("/w300/", "/w800/").Replace("/w1200/", "/w800/").Replace(" ", "%20").Replace("+", "%20");
						if (coverImageUri.StartsWith("https://vcdn.tikicdn.com/cache/w800/media/catalog/product/t/i/tiki_", StringComparison.OrdinalIgnoreCase))
							coverImageUri = null;
					}

					// price
					start = html.PositionOf("<span id=\"span-list-price");
					start = start < 0 ? -1 : html.PositionOf(">", start + 1) + 1;
					end = start < 0 ? -1 : html.PositionOf("</span>", start + 1);
					string price = start > 0 && start > 0 ? html.Substring(start, end - start).Trim().Replace("₫", "").Replace(".", "").Trim() : "";
					try
					{
						if (price.Length > 4)
							price = price.Left(price.Length - 3) + "000";
						book.ExtraData.Add(new JProperty("Price", Convert.ToDouble(string.IsNullOrWhiteSpace(price) ? "0" : price)));
					}
					catch
					{
						book.ExtraData.Add(new JProperty("Price", 0.0d));
					}

					// cover type
					string coverType = book.Title.IsContains("bìa mềm")
															? "Bìa Mềm"
															: book.Title.IsContains("bìa cứng")
																? "Bìa Cứng"
																: html.IsContains("Bìa Mềm") ? "Bìa Mềm" : "";
					book.ExtraData.Add(new JProperty("CoverType", coverType));

					// summary
					start = html.PositionOf("<div id=\"gioi-thieu");
					start = start < 0 ? -1 : html.PositionOf("<p", start + 1);
					end = start < 0 ? -1 : html.PositionOf("</div>", start + 1);
					string summary = Crawler.NormalizeSummary(start > 0 && end > start ? html.Substring(start, end - start).Trim() : "");

					string startingSummary = "<p><strong>" + (book.Title.PositionOf(" (Bộ") > 0 ? book.Title.Substring(0, book.Title.PositionOf(" (Bộ")).Trim() : book.Title) + "</strong></p>";
					if (summary.StartsWith(startingSummary, StringComparison.OrdinalIgnoreCase))
						summary = summary.Substring(startingSummary.Length).Trim();

					// features
					start = html.PositionOf("id=\"chi-tiet");
					start = start < 0 ? -1 : html.PositionOf("<tbody", start + 1);
					end = start < 0 ? -1 : html.PositionOf("</tbody>", start + 1);
					start = start < 0 ? -1 : html.PositionOf(">", start + 1) + 1;
					html = start > -1 && end > 0 ? html.Substring(start, end - start).Trim() : "";

					start = html.PositionOf("<tr");
					while (start > -1)
					{
						start = html.PositionOf(">", start + 1) + 1;
						end = start < 0 ? -1 : html.PositionOf("</tr>", start + 1);

						string info = start > -1 && end > 0 ? html.Substring(start, end - start).Trim() : "";
						html = end > 0 ? html.Remove(0, end < html.Length - 5 ? end + 5 : html.Length).Trim() : "";

						start = info.PositionOf("<td");
						start = info.PositionOf(">", start + 1);
						end = info.PositionOf("</td>", start + 1);
						string title = end > start ? info.Substring(start + 1, end - start - 1).Trim() : "";

						start = info.PositionOf("<td", start + 1);
						start = info.PositionOf(">", start + 1);
						end = info.PositionOf("</td>", start + 1);
						info = end > start ? info.Substring(start + 1, end - start - 1).Trim() : "";

						info = Utility.RemoveTag(info, "br");
						info = info.Replace(StringComparison.OrdinalIgnoreCase, ",", "<br/>");

						info = Utility.RemoveTag(info, "span");
						info = Utility.RemoveTag(info, "a");
						info = Utility.RemoveTag(info, "meta");
						info = Utility.ClearTag(info, "div");
						info = info.Trim();

						start = info.PositionOf("<");
						end = info.PositionOf(">", start + 1);
						while (start > 0 && end > start)
						{
							info = info.Substring(0, start).Trim() + " - " + info.Substring(end + 1).Trim();
							start = info.PositionOf("<");
							end = info.PositionOf(">", start + 1);
						}
						info = info.Trim();

						if (title.StartsWith("Tác giả", StringComparison.OrdinalIgnoreCase) || title.StartsWith("Author", StringComparison.OrdinalIgnoreCase))
							book.Author = info.GetNormalized();
						else if (title.StartsWith("Người dịch", StringComparison.OrdinalIgnoreCase) || title.StartsWith("Dịch giả", StringComparison.OrdinalIgnoreCase))
							book.Translator = info.GetNormalized();
						else if (title.StartsWith("Nhà xuất bản", StringComparison.OrdinalIgnoreCase))
						{
							book.Publisher = info.Replace(StringComparison.OrdinalIgnoreCase, "NXB", "").Replace(StringComparison.OrdinalIgnoreCase, "Nhà Xuất Bản", "").Trim().GetNormalized();
							if (book.Publisher.IsEquals("Đang Cập Nhật"))
								book.Publisher = "";
						}
						else if (title.StartsWith("Công ty phát hành", StringComparison.OrdinalIgnoreCase))
							book.Producer = info.GetNormalized();
						else if (title.StartsWith("Trọng lượng", StringComparison.OrdinalIgnoreCase))
							try
							{
								book.ExtraData.Add(new JProperty("Weight", Convert.ToInt32(info)));
							}
							catch
							{
								if (book.ExtraData["Weight"] == null)
									book.ExtraData.Add(new JProperty("Weight", 0));
							}
						else if (title.StartsWith("Kích thước", StringComparison.OrdinalIgnoreCase))
							book.ExtraData.Add(new JProperty("Dimenssions", info));
						else if (title.StartsWith("Ngày xuất bản", StringComparison.OrdinalIgnoreCase))
							book.ExtraData.Add(new JProperty("PublishedDate", info.Replace("-", "/")));
						else if (title.StartsWith("Số trang", StringComparison.OrdinalIgnoreCase))
							try
							{
								book.ExtraData.Add(new JProperty("Pages", Convert.ToInt32(info)));
							}
							catch
							{
								if (book.ExtraData["Pages"] == null)
									book.ExtraData.Add(new JProperty("Pages", 0));
							}

						start = html.PositionOf("<tr");
					}

					// check empty attributes
					if (book.ExtraData["Weight"] == null)
						book.ExtraData.Add(new JProperty("Weight", 0));
					if (book.ExtraData["Pages"] == null)
						book.ExtraData.Add(new JProperty("Pages", 0));

					// add summary at the last element
					book.ExtraData.Add(new JProperty("Summary", summary));

					// re-normalize title
					if (book.Title.EndsWith("(" + book.Author + ")"))
						book.Title = book.Title.Left(book.Title.Length - 2 - book.Author.Length).Trim();

					if (book.Title.EndsWith("()"))
						book.Title = book.Title.Left(book.Title.Length - 2).Trim();

					// check
					cancellationToken.ThrowIfCancellationRequested();
					book.PermanentID = book.ID = Book.GenerateID(book.Title, book.Author, book.Translator);

					// add into final collection
					int eposides = Convert.ToInt32(book.ExtraData["Eposides"]);
					if (eposides > 1)
					{
						// download cover image
						string coverFilePath = "";
						if (!string.IsNullOrWhiteSpace(coverImageUri))
						{
							await Utils.DownloadFileAsync(coverImageUri, book.SourceUri, mediafilesFolder, book.ID, cancellationToken, null, onDownloadError);
							book.Cover = Utils.MediaUri + Utils.GetFilename(coverImageUri);
							coverFilePath = book.Cover.Replace(Utils.MediaUri, mediafilesFolder + "\\" + book.ID + "-");
						}

						if (!string.IsNullOrWhiteSpace(coverFilePath) && !File.Exists(coverFilePath))
						{
							book.Cover = "";
							coverFilePath = "";
						}

						book.Title = Crawler.GetPaperBookTitle(book.Title, eposides);

						price = book.ExtraData["Price"] == null ? "0" : (Convert.ToDouble((book.ExtraData["Price"] as JValue).Value) / eposides).ToString("######000");
						if (price.Length > 4)
							price = price.Left(price.Length - 3) + "000";
						double finalPrice = Convert.ToDouble(price);

						int finalWeight = book.ExtraData["Weight"] == null ? 0 : Convert.ToInt32((book.ExtraData["Weight"] as JValue).Value) / eposides;
						int finalPages = book.ExtraData["Pages"] == null ? 0 : Convert.ToInt32((book.ExtraData["Pages"] as JValue).Value) / eposides;

						for (int idx = 1; idx <= eposides; idx++)
						{
							// prepare object
							Book theBook = book.Clone();
							theBook.Title = (theBook.Title + Crawler.GetPaperBookEposides(idx, eposides)).Replace(") (", " - ");
							theBook.PermanentID = theBook.ID = Book.GenerateID(theBook.Title, theBook.Author, theBook.Translator);

							theBook.ExtraData = book.ExtraData;
							theBook.ExtraData.Remove("Eposides");

							theBook.ExtraData["Price"] = finalPrice;
							theBook.ExtraData["Weight"] = finalWeight;
							theBook.ExtraData["Pages"] = finalPages;

							// check and update into collection
							if (!Crawler.CrawledBooks.Contains(theBook.ID))
							{
								if (!string.IsNullOrWhiteSpace(coverFilePath))
								{
									theBook.Cover = Utils.MediaUri + Utils.GetFilename(book.Cover.Replace(Utils.MediaUri, Utility.GetUUID() + "-"));
									File.Copy(coverFilePath, theBook.Cover.Replace(Utils.MediaUri, mediafilesFolder + "\\" + theBook.ID + "-"));
								}

								finalBooks.Add(theBook);
								Crawler.CrawledBooks.Add(theBook.ID);

								if (onProcess != null)
									onProcess("The book is completed [" + theBook.Name + " : " + theBook.SourceUri + "]");
							}
							else if (onProcess != null)
								onProcess("The book is BY-PASS [" + theBook.Name + "]");
						}

						// delete old cover image
						if (!string.IsNullOrWhiteSpace(coverFilePath))
							File.Delete(coverFilePath);
					}
					else if (!Crawler.CrawledBooks.Contains(book.ID))
					{
						// download cover image
						if (!string.IsNullOrWhiteSpace(coverImageUri))
						{
							await Utils.DownloadFileAsync(coverImageUri, book.SourceUri, mediafilesFolder, book.ID, cancellationToken, null, onDownloadError);
							book.Cover = Utils.MediaUri + Utils.GetFilename(coverImageUri);
						}

						// update into collections
						book.ExtraData.Remove("Eposides");
						finalBooks.Add(book);
						Crawler.CrawledBooks.Add(book.ID);

						if (onProcess != null)
							onProcess("The book is completed [" + book.Name + " : " + book.SourceUri + "]");
					}
					else if (onProcess != null)
						onProcess("The book is BY-PASS [" + book.Name + "]");
				}
				catch (Exception ex)
				{
					if (!Crawler.MissingBooks.ContainsKey(book.ID))
						Crawler.MissingBooks.Add(book.ID, book.SourceUri + "|" + book.Title);

					if (onProcess != null && !(ex is OperationCanceledException))
					{
						string msg = "\r\n" + "--- Error occurred while crawling details of book [" + book.Name + " : " + book.SourceUri + "]"
												+ "\r\n" + "[" + ex.GetType().ToString() + "]: " + ex.Message + "\r\n"
												+ "\r\n" + "Stack: " + ex.StackTrace + "\r\n\r\n";
						onProcess(msg);
					}
				}
			};

			Func<Task> fastCrawl = async () =>
			{
				List<Task> tasks = new List<Task>();
				for (int index = 0; index < bookself.Books.Count; index++)
					tasks.Add(getDetails(index));
				await Task.WhenAll(tasks);
			};

			Func<Task> slowCrawl = async () =>
			{
				int index = 0;
				while (index < bookself.Books.Count)
				{
					cancellationToken.ThrowIfCancellationRequested();
					await getDetails(index);
					index++;
				}
			};

			bool useFastMethod = crawlMethod.Equals((int)CrawMethods.Fast);
			if (!useFastMethod && !crawlMethod.Equals((int)CrawMethods.Slow))
				useFastMethod = Utility.GetRandomNumber() % 7 == 0;

			if (useFastMethod)
				await fastCrawl();
			else
				await slowCrawl();

			// update collection of books
			cancellationToken.ThrowIfCancellationRequested();
			bookself.Books = finalBooks;

			// callback on complete
			if (onCompleted != null)
				onCompleted(bookself);

			return bookself;
		}
		#endregion

	}
}