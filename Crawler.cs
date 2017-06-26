#region Related components
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using net.vieapps.Components.Utility;
#endregion

namespace net.vieapps.books.Components
{

	[Serializable]
	public enum CrawMethods
	{
		Fast = 0,
		Slow = 1
	}

	public class Crawler
	{

		#region Constructors
		public Crawler()
		{
			this.Source = "";
			this.MaxAttempts = 0;
			this.Status = "Initialized";
		}
		#endregion

		#region Properties
		public string Source { get; internal set; }
		public int MaxAttempts { get; internal set; }
		public string Status { get; internal set; }

		internal int _crawledCounter = 0, _bypassCounter = 0, _queuedIndex = 0, _crawlMethod = -1;
		internal string _crawlFolder = "", _tempFolder = "", _booksFolder = "";
		internal CancellationToken _cancellationToken;
		internal Action<string> _onProcess = null;
		internal Action<Crawler> _onCompleted = null;
		internal Action<string, Exception> _onError = null;
		internal List<Book> _queuedBooks = new List<Book>();
		internal HashSet<string> _crawledBooks = new HashSet<string>(), _bypassBooks = new HashSet<string>();

		internal Stopwatch _stopwatch = null;
		#endregion

		#region Get crawlers
		public static List<Crawler> GetCrawlers(List<string> arguments, string crawlFolder, string tempFolder, string booksFolder, Action<string> onProcess, Action<Crawler> onCompleted, Action<string, Exception> onError, CancellationToken cancellationToken)
		{
			List<Crawler> crawlers = new List<Crawler>();

			if (arguments != null && arguments.Count > 0)
				for (int index = 0; index < arguments.Count; index++)
				{
					string argument = arguments[index].ToLower();
					if (!argument.StartsWith("/crawl-"))
						continue;

					Crawler crawler = new Crawler();
					crawler._crawlFolder = crawlFolder;
					crawler._tempFolder = tempFolder;
					crawler._booksFolder = booksFolder;
					crawler._onProcess = onProcess;
					crawler._onCompleted = onCompleted;
					crawler._onError = onError;
					crawler._cancellationToken = cancellationToken;

					if (argument.StartsWith("/crawl-site:"))
					{
						argument = argument.Substring(argument.IndexOf(":") + 1);
						if (argument.StartsWith("isach.info"))
							crawler.Source = "http://isach.info";
						else if (argument.StartsWith("vnthuquan.net"))
							crawler.Source = "http://vnthuquan.net";
						else if (argument.StartsWith("vinabook.com"))
							crawler.Source = "https://www.vinabook.com";
						else if (argument.StartsWith("tiki.vn"))
							crawler.Source = "https://tiki.vn";
					}

					else if (argument.StartsWith("/crawl-books:"))
					{
						argument = argument.Substring(argument.IndexOf(":") + 1);
						crawler.Source = "file://" + crawler._crawlFolder + "\\" + (argument.IndexOf("-") > 0 ? argument.Substring(0, argument.IndexOf("-")) : argument);
					}

					if (argument.IndexOf("-") > 0 && !argument.StartsWith("/crawl-method:"))
						try
						{
							crawler.MaxAttempts = Convert.ToInt32(argument.Substring(argument.IndexOf("-") + 1));
						}
						catch { }

					if (!crawler.Source.Equals(""))
					{
						foreach (string arg in arguments)
							if (arg.StartsWith("/crawl-method:"))
							{
								try
								{
									crawler._crawlMethod = Convert.ToInt32(arg.Substring(arg.IndexOf(":") + 1));
								}
								catch { }
								break;
							}

						if (crawler._crawlMethod < 0)
							crawler._crawlMethod = 1;

						crawlers.Add(crawler);
					}
				}

			return crawlers;
		}
		#endregion

		public void Run()
		{
			this.Status = "Running";
			if (this.Source.StartsWith("file://" + this._crawlFolder + "\\"))
			{
				if (this._onProcess != null)
					this._onProcess("Start to crawl details of all queued books");

				this.StartBookCrawlers();
			}
			else if (this.Source.StartsWith("http://isach.info"))
			{
				if (this._onProcess != null)
					this._onProcess("Start to crawl the listing of books on iSach.info" + (this.MaxAttempts > 0 ? " - Max attempts: " + this.MaxAttempts : "") + "\r\n");

				this.RunISachCrawler();
			}
			else if (this.Source.StartsWith("http://vnthuquan.net"))
			{
				if (this._onProcess != null)
					this._onProcess("Start to crawl the listing of books on VNThuQuan.net" + (this.MaxAttempts > 0 ? " - Max attempts: " + this.MaxAttempts : "") + "\r\n");

				this.RunVNThuQuanCrawler();
			}
			else if (this.Source.StartsWith("https://www.vinabook.com"))
			{
				if (this._onProcess != null)
					this._onProcess("Start to crawl books on VinaBook.com" + (this.MaxAttempts > 0 ? " - Max attempts: " + this.MaxAttempts : "") + "\r\n");

				this._stopwatch = new Stopwatch();
				this._stopwatch.Start();

				Crawler.GetCategories(this._crawlFolder, "https://www.vinabook.com");
				Crawler.GetCrawledBooks(this._crawlFolder);
				Crawler.GetMissingBooks(this._crawlFolder);

				this.RunVinaBookCrawler();
			}
			else if (this.Source.StartsWith("https://tiki.vn"))
			{
				if (this._onProcess != null)
					this._onProcess("Start to crawl books on Tiki.vn" + (this.MaxAttempts > 0 ? " - Max attempts: " + this.MaxAttempts : "") + "\r\n");

				this._stopwatch = new Stopwatch();
				this._stopwatch.Start();

				Crawler.GetCategories(this._crawlFolder, "https://tiki.vn");
				Crawler.GetCrawledBooks(this._crawlFolder);
				Crawler.GetMissingBooks(this._crawlFolder);

				this.RunTikiCrawler();
			}
		}

		#region Run crawler to get the listing of books on isach.info
		void RunISachCrawler()
		{
			System.Timers.Timer timer = new System.Timers.Timer();
			timer.Interval = 10;
			timer.Elapsed += new System.Timers.ElapsedEventHandler(this.OnRunISachCrawler);
			timer.AutoReset = false;
			timer.Start();
		}

		void OnRunISachCrawler(object sender, System.Timers.ElapsedEventArgs e)
		{
			BookSelf bookself = ISach.InitializeBookSelf(this._crawlFolder);
			if (bookself.UrlPattern == null)
			{
				if (this._onProcess != null)
					this._onProcess("\r\n" + "Complete the crawling process on iSach.info" + "\r\n");

				this.Status = "Completed";
				if (this._onCompleted != null)
					this._onCompleted(this);
			}
			else if (bookself.TotalPages < 1 || bookself.CurrentPage <= bookself.TotalPages)
				Task.Run(async () =>
				{
					if (bookself.CurrentPage < 2)
						try
						{
							await Utility.GetWebPageAsync("http://isach.info/robots.txt");
						}
						catch { }

					try
					{
						int delay = Utility.GetRandomNumber(1234, 2345);
						if (bookself.TotalPages > 10 && bookself.CurrentPage > 9 && bookself.CurrentPage % 10 == 0)
						{
							if (this._onProcess != null)
								this._onProcess("... Wait for few seconds before continue with lot of pages ...");
							delay = Utility.GetRandomNumber(4321, 5432);
						}
						await Task.Delay(delay, this._cancellationToken);

						await ISach.GetBookSelf(bookself.UrlPattern, bookself.UrlParameters, bookself.CurrentPage, bookself.TotalPages, this._cancellationToken, this.OnISachCrawlerCompleted, this.OnISachCrawlerError);
					}
					catch (OperationCanceledException)
					{
						if (this._onProcess != null)
							this._onProcess("\r\n" + "......... Canceled (crawlers) ........." + "\r\n");
					}
					catch (Exception ex)
					{
						if (this._onError != null)
							this._onError("Error: " + ex.Message, ex);
					}
				}).ConfigureAwait(false);
		}

		void OnISachCrawlerCompleted(BookSelf bookself)
		{
			ISach.FinaIizeBookSelf(bookself, this._crawlFolder);
			this._crawledCounter++;
			if (this._onProcess != null)
				this._onProcess("Success crawl data of iSach.info [" + bookself.CurrentPage + "/" + bookself.TotalPages + "]: " + bookself.Url);

			if (bookself.CurrentPage <= bookself.TotalPages)
			{
				if ((this.MaxAttempts < 1 || (this.MaxAttempts > 1 && this._crawledCounter < this.MaxAttempts)))
				{
					if (this._onProcess != null)
						this._onProcess((bookself.CurrentPage < bookself.TotalPages ? "Process next page [" + (bookself.CurrentPage + 1) + "/" + bookself.TotalPages + "]" : "Process next category/char") + " of iSach.info");
					this.RunISachCrawler();
				}
				else
				{
					if (this._onProcess != null)
						this._onProcess("\r\n" + "Stop crawling when reach max-attempts < " + this.MaxAttempts + " > on iSach.info" + "\r\n");

					this.Status = "Completed";
					if (this._onCompleted != null)
						this._onCompleted(this);
				}
			}
		}

		void OnISachCrawlerError(BookSelf bookself, Exception ex)
		{
			this.Status = "Error";
			if (this._onError != null)
				this._onError("Error occurred while crawling data of iSach.info [" + bookself.Url + "] <" + bookself.CurrentPage + "/" + bookself.TotalPages + ">", ex);
		}
		#endregion

		#region Run crawler to get the listing of books on vnthuquan.net
		void RunVNThuQuanCrawler()
		{
			System.Timers.Timer timer = new System.Timers.Timer();
			timer.Interval = 10;
			timer.Elapsed += new System.Timers.ElapsedEventHandler(this.OnRunVNThuQuanCrawler);
			timer.AutoReset = false;
			timer.Start();
		}

		void OnRunVNThuQuanCrawler(object sender, System.Timers.ElapsedEventArgs e)
		{
			BookSelf bookself = VNThuQuan.InitializeBookSelf(this._crawlFolder);
			if (bookself.UrlPattern == null)
			{
				if (this._onProcess != null)
					this._onProcess("\r\n" + "Complete the crawling process on VNThuQuan.net" + "\r\n");

				this.Status = "Completed";
				if (this._onCompleted != null)
					this._onCompleted(this);
			}
			else if (bookself.TotalPages < 1 || bookself.CurrentPage <= bookself.TotalPages)
				Task.Run(async () =>
				{
					try
					{
						await VNThuQuan.GetBookSelf(bookself.UrlPattern, bookself.UrlParameters, bookself.CurrentPage, bookself.TotalPages, this._cancellationToken, this.OnVNThuQuanCrawlerCompleted, this.OnVNThuQuanCrawlerError);
					}
					catch (OperationCanceledException)
					{
						if (this._onProcess != null)
							this._onProcess("\r\n" + "......... Canceled (crawlers) ........." + "\r\n");
					}
					catch (Exception ex)
					{
						if (this._onError != null)
							this._onError("Error: " + ex.Message, ex);
					}
				}).ConfigureAwait(false);
		}

		void OnVNThuQuanCrawlerCompleted(BookSelf bookself)
		{
			VNThuQuan.FinaIizeBookSelf(bookself, this._crawlFolder);
			this._crawledCounter++;
			if (this._onProcess != null)
				this._onProcess("Success crawl data of VNThuQuan.net [" + bookself.CurrentPage + "/" + bookself.TotalPages + "]: " + bookself.Url);

			if (bookself.CurrentPage <= bookself.TotalPages)
			{
				if ((this.MaxAttempts < 1 || (this.MaxAttempts > 1 && this._crawledCounter < this.MaxAttempts)))
				{
					if (this._onProcess != null)
						this._onProcess("Process next page [" + (bookself.CurrentPage + 1) + "/" + bookself.TotalPages + "] of VNThuQuan.net");
					this.RunVNThuQuanCrawler();
				}
				else
				{
					if (this._onProcess != null)
						this._onProcess("\r\n" + "Stop when got max attempts < " + this.MaxAttempts + " > on VNThuQuan.net" + "\r\n");

					this.Status = "Completed";
					if (this._onCompleted != null)
						this._onCompleted(this);
				}
			}
		}

		void OnVNThuQuanCrawlerError(BookSelf bookself, Exception ex)
		{
			this.Status = "Error";
			if (this._onError != null)
				this._onError("Error occurred while crawling data of VNThuQuan.net [" + bookself.Url + "] <" + bookself.CurrentPage + "/" + bookself.TotalPages + ">", ex);
		}
		#endregion

		#region Run crawler to get details of books
		void StartBookCrawlers()
		{
			string filePath = this.Source.Substring(7) + ".json";
			try
			{
				string[] sourceJsons = Utility.ReadTextFile(filePath).Replace("\r", "").Split("\n".ToCharArray());
				foreach (string sourceJson in sourceJsons)
					if (!string.IsNullOrWhiteSpace(sourceJson))
						try
						{
							Book book = new Book();
							book.ParseJson(sourceJson);
							this._queuedBooks.Add(book);
						}
						catch { }
			}
			catch (Exception ex)
			{
				if (this._onError != null)
					this._onError("Cannot read source for crawling", ex);
			}

			if (this._queuedBooks.Count < 1)
			{
				if (this._onProcess != null)
					this._onProcess("Stop process because got no data [" + filePath + "]");

				this.Status = "Completed";
				if (this._onCompleted != null)
					this._onCompleted(this);

				return;
			}

			this._queuedIndex = 0;
			this._crawledCounter = 0;

			filePath = this._crawlFolder + "\\crawled-books.data";
			if (File.Exists(filePath))
			{
				string[] ids = Utility.ReadTextFile(filePath).Replace("\r", "").Split("\n".ToCharArray());
				foreach (string id in ids)
					if (!string.IsNullOrWhiteSpace(id) && !this._crawledBooks.Contains(id.Trim().ToLower()))
						this._crawledBooks.Add(id.Trim().ToLower());
			}

			filePath = this._crawlFolder + "\\bypass-books.data";
			if (File.Exists(filePath))
			{
				string[] ids = Utility.ReadTextFile(filePath).Replace("\r", "").Split("\n".ToCharArray());
				foreach (string id in ids)
					if (!string.IsNullOrWhiteSpace(id) && !this._bypassBooks.Contains(id.Trim().ToLower()))
						this._bypassBooks.Add(id.Trim().ToLower());
			}

			if (this._onProcess != null)
			{
				string message = " - Source: " + this.Source.Replace("\\", "/") + "\r\n"
													+ " - Crawling method: [" + (this._crawlMethod.Equals(0) || this._crawlMethod.Equals(1) ? ((CrawMethods)this._crawlMethod).ToString() : "Random") + "]" + "\r\n"
													+ " - Total of queued books: " + this._queuedBooks.Count.ToString("###,##0") + "\r\n"
													+ " - Total of crawled books: " + (this._crawledBooks.Count / 2).ToString("###,##0") + "\r\n"
													+ " - Total of bypass books: " + (this._bypassBooks.Count / 2).ToString("###,##0") + "\r\n"
													+ (this.MaxAttempts > 0 ? " - Max attempts: " + this.MaxAttempts : "") + "\r\n";
				this._onProcess(message);
			}

			this.RunBookCrawler();
		}

		void SaveCrawledBooks()
		{
			List<string> crawledIDs = new List<string>();
			foreach (string id in this._crawledBooks)
				crawledIDs.Add(id);

			Utility.WriteTextFile(this._crawlFolder + "\\crawled-books.data", crawledIDs, false);
		}

		void RunBookCrawler()
		{
			System.Timers.Timer timer = new System.Timers.Timer();
			timer.Interval = 10;
			timer.Elapsed += new System.Timers.ElapsedEventHandler(this.OnRunBookCrawler);
			timer.AutoReset = false;
			timer.Start();
		}

		void OnRunBookCrawler(object sender, System.Timers.ElapsedEventArgs e)
		{
			// check
			if (this.MaxAttempts > 0 && this._crawledCounter >= this.MaxAttempts)
			{
				if (this._onProcess != null)
					this._onProcess("Stop when got max attempts < " + this.MaxAttempts + " > on details of books");
				this.OnRunBookCrawlerCompleted();
				return;
			}

			// get book for crawling
			Book book = null;
			while (this._queuedIndex < this._queuedBooks.Count && book == null)
			{
				book = this._queuedBooks[this._queuedIndex];
				if (this._crawledBooks.Contains(book.ID) || this._crawledBooks.Contains(book.PermanentID) || this._crawledBooks.Contains(book.Name.ToLower().GetMD5()) || this._crawledBooks.Contains(book.Filename.ToLower().GetMD5()))
				{
					book = null;
					this._queuedIndex++;
				}
				else if (this._bypassBooks.Contains(book.ID) || this._bypassBooks.Contains(book.PermanentID))
				{
					if (this._onProcess != null)
						this._onProcess("\r\n" + " ++++++ Bypass the book:  < " + book.Name + " > ***** " + "\r\n");
					book = null;
					this._bypassCounter++;
					this._queuedIndex++;
				}
			}

			// stop if has no book
			if (book == null)
			{
				this.OnRunBookCrawlerCompleted();
				return;
			}

			// run crawling process
			if (this._onProcess != null)
				this._onProcess(DateTime.Now.ToString("dd/MM HH:mm") + ": ----- (" + (this._crawledCounter + 1) + ")" + "\r\n" + "Start to crawl details of the book [" + book.Name + " : " + book.SourceUri + "]");

			Task.Run(async () =>
			{
				try
				{
					if (book.SourceUri.Contains("isach.info"))
					{
						try
						{
							await Utility.GetWebPageAsync("http://isach.info/robots.txt");
						}
						catch { }

						int delay = this._crawledCounter > 4 && this._crawledCounter % 5 == 0 ? 3210 : 1500;
						if (this._crawledCounter > 12 && this._crawledCounter % 13 == 0)
							delay = Utility.GetRandomNumber(3456, 7000);
						await Task.Delay(delay, this._cancellationToken);

						await ISach.GetBook(book.PermanentID, book.SourceUri, this._tempFolder, this._cancellationToken, this.OnCrawlingProcess, this.OnCrawlingBookParsed, this.OnCrawlingBookCompleted, this.OnCrawlingBookError, this.OnCrawlingChapterCompleted, this.OnCrawlingChapterError, this.OnCrawlingFileCompleted, this.OnCrawlingFileError, this._crawlMethod);
					}
					else
						await VNThuQuan.GetBook(book.PermanentID, book.SourceUri, this._tempFolder, this._cancellationToken, this.OnCrawlingProcess, this.OnCrawlingBookParsed, this.OnCrawlingBookCompleted, this.OnCrawlingBookError, this.OnCrawlingChapterCompleted, this.OnCrawlingChapterError, this.OnCrawlingFileCompleted, this.OnCrawlingFileError, this._crawlMethod);
				}
				catch (OperationCanceledException)
				{
					this.SaveCrawledBooks();
					if (this._onProcess != null)
						this._onProcess("\r\n" + "......... Canceled (crawlers) ........." + "\r\n");
				}
				catch (Exception ex)
				{
					if (this._onError != null)
						this._onError("Error: " + ex.Message, ex);
				}
			}).ConfigureAwait(false);
		}

		void OnRunBookCrawlerCompleted()
		{
			this.SaveCrawledBooks();
			if (this._onProcess != null)
			{
				this._onProcess("The crawling process is completed");
				this._onProcess("- Total of crawled books: " + this._crawledCounter);
				this._onProcess("- Total of by-pass books: " + this._crawledCounter + "\r\n");
			}

			this.Status = "Completed";
			if (this._onCompleted != null)
				this._onCompleted(this);
		}

		void OnCrawlingProcess(string message)
		{
			if (this._onProcess != null)
				this._onProcess(message);
		}

		void OnCrawlingBookParsed(Book book)
		{
			if (this._onProcess != null)
				this._onProcess("The book is parsed. Start to fetch [" + book.Chapters.Count + "] chapter(s).");
		}

		void OnCrawlingChapterCompleted(string url, List<string> data)
		{
			if (this._onProcess != null)
			{
				string message = "";
				if (data == null || data[0].Equals("") && data[1].Equals(""))
					message = "- :--- The chapter is NOT downloaded [" + url + "]";
				else
				{
					string counter = data.Count > 3 ? data[2] + "/" + data[3] + " " : "";
					message = "- The chapter " + counter + "is fetched" + (!string.IsNullOrWhiteSpace(data[0]) ? " [" + data[0] + "]" : "");
				}
				this._onProcess(message);
			}
		}

		void OnCrawlingChapterError(string url, Exception ex)
		{
			if (!(ex is OperationCanceledException) && this._onError != null)
				this._onError("Error occurred while crawling chapter [" + url + "]: " + ex.Message, ex);
		}

		void OnCrawlingFileCompleted(string url, string filePath)
		{
			if (this._onProcess != null)
				this._onProcess("- The file is downloaded [" + url + "]");
		}

		void OnCrawlingFileError(string url, Exception ex)
		{
			if (!(ex is OperationCanceledException) && this._onError != null)
				this._onError("Error occurred while downloading file [" + url + "]: " + ex.Message, ex);
		}

		void OnCrawlingBookCompleted(Book book)
		{
			if (book == null || book.Title.Equals(""))
			{
				if (this._onProcess != null)
					this._onProcess("The book is NOT downloaded. Please check the internet connection.");
				this.GoNext();
			}
			else
			{
				if (this._onProcess != null)
					this._onProcess("The book is downloaded [" + book.Name + "]. Start to generate JSON file.");

				Book.GenerateJsonFile(book, this._tempFolder, book.Filename);
				if (this._onProcess != null)
					this._onProcess("JSON file of the book is generated [" + this._booksFolder + "\\" + book.Filename + ".json]");

				if (File.Exists(this._booksFolder + "\\" + Utils.NormalizeFilename(book.Title + " - Sưu Tầm") + ".json"))
					try
					{
						File.Delete(this._booksFolder + "\\" + Utils.NormalizeFilename(book.Title + " - Sưu Tầm") + ".json");
					}
					catch { }

				Book.MoveFiles(book, this._tempFolder, this._booksFolder, true, true, this.OnMoveFilesCompleted);
			}
		}

		void OnCrawlingBookError(Book book, Exception ex)
		{
			if (!(ex is OperationCanceledException) && this._onError != null)
				this._onError("Error occurred while crawling the book" + (book != null ? " [" + book.SourceUri + "]" : ""), ex);
			this.GoNext();
		}

		void OnMoveFilesCompleted(Book book)
		{
			if (this._onProcess != null)
				this._onProcess("-----" + "\r\n" + DateTime.Now.ToString("dd/MM HH:mm") + ": The book [" + book.Name + "] is completed...." + "\r\n" + (this._queuedIndex < this._queuedBooks.Count ? "\r\n" + "Jump to next step...." + "\r\n" : "") + "\r\n");

			this._crawledBooks.Add(book.ID);
			this._crawledBooks.Add(book.PermanentID);

			this.GoNext();
		}

		void GoNext()
		{
			this._queuedIndex++;

			this._crawledCounter++;
			if (this._crawledCounter > 9 && this._crawledCounter % 10 == 0)
				this.SaveCrawledBooks();

			this.RunBookCrawler();
		}
		#endregion

		// ----------------------------------------------------------------

		#region Helper properties for working with crawler of paper books
		internal static JArray _Categories = null;

		public static JArray Categories
		{
			get
			{
				return Crawler._Categories;
			}
		}

		public static void GetCategories(string folder, string startWith)
		{
			string filePath = folder + "\\categories.json";
			if (!File.Exists(filePath))
				throw new FileNotFoundException("JSON file of categories is not found [" + filePath + "]");

			Crawler._Categories = new JArray();
			JArray normalizedCategories = new JArray();

			JArray categories = JArray.Parse(Utility.ReadTextFile(filePath));
			foreach (JObject category in categories)
			{
				Crawler.AddCategory(category, null, startWith);
				JToken children = category["Children"];
				if (children != null && (children is JArray))
				{
					string parent = category["Name"] != null ? (category["Name"] as JValue).Value.ToString() : null;
					foreach (JObject child in children as JArray)
						Crawler.AddCategory(child, parent, startWith);
				}

				category.Remove("Sources");
				category.Remove("Children");
				category.Add(new JProperty("Counters", 0));

				if (children != null && (children is JArray))
				{
					JArray childCategories = new JArray();
					foreach (JObject child in children as JArray)
					{
						child.Remove("Sources");
						child.Remove("Children");
						child.Add(new JProperty("Counters", 0));
						childCategories.Add(child);
					}
					category.Add(new JProperty("Children", childCategories));
				}

				normalizedCategories.Add(category);
			}

			filePath = folder + "\\normalized-categories.json";
			Utility.WriteTextFile(filePath, normalizedCategories.ToString(Formatting.Indented));
		}

		static void AddCategory(JObject json, string parent, string startWith)
		{
			string name = json["Name"] == null ? null : (!string.IsNullOrWhiteSpace(parent) ? parent + " > " : "") + (json["Name"] as JValue).Value.ToString();
			JArray sources = json["Sources"] != null ? json["Sources"] as JArray : null;
			if (sources != null && sources.Count > 0)
				foreach (JObject source in sources)
				{
					string url = source["URL"] != null ? (source["URL"] as JValue).Value.ToString() : null;
					if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(url) && url.StartsWith(startWith))
						Crawler._Categories.Add(new JObject() {
						{ "Name",  name },
						{ "URL",  url },
						{ "Tags", source["Tags"] != null ? source["Tags"] : "" }
					});
				}
		}

		static HashSet<string> _CrawledBooks = null;

		public static HashSet<string> CrawledBooks
		{
			get
			{
				return Crawler._CrawledBooks;
			}
		}

		public static void GetCrawledBooks(string folder)
		{
			Crawler._CrawledBooks = new HashSet<string>();
			string filePath = folder + "\\paper-books-crawled.txt";
			if (File.Exists(filePath))
			{
				string[] ids = Utility.ReadTextFile(filePath).Replace("\r", "").Split("\n".ToCharArray());
				foreach (string id in ids)
					if (!string.IsNullOrWhiteSpace(id) && !Crawler._CrawledBooks.Contains(id.Trim().ToLower()))
						Crawler._CrawledBooks.Add(id.Trim().ToLower());
			}
		}

		public static void SaveCrawledBooks(string folder)
		{
			List<string> ids = new List<string>();
			foreach (string id in Crawler._CrawledBooks)
				ids.Add(id);

			string filePath = folder + "\\paper-books-crawled.txt";
			Utility.WriteTextFile(filePath, ids, false);
		}

		static Hashtable _MissingBooks = null;

		public static Hashtable MissingBooks
		{
			get
			{
				return Crawler._MissingBooks;
			}
		}

		public static void GetMissingBooks(string folder)
		{
			Crawler._MissingBooks = new Hashtable();
			string filePath = folder + "\\paper-books-missing.txt";
			if (File.Exists(filePath))
			{
				string[] lines = Utility.ReadTextFile(filePath).Replace("\r", "").Split("\n".ToCharArray());
				foreach (string line in lines)
				{
					if (string.IsNullOrWhiteSpace(line))
						continue;

					string[] info = line.Split('|');
					if (!Crawler._MissingBooks.Contains(info[0].Trim().ToLower()))
						Crawler._MissingBooks.Add(info[0].Trim().ToLower(), info[1] + (info.Length > 2 ? "|" + info[2] : ""));
				}
			}
		}

		public static void SaveMissingBooks(string folder)
		{
			List<string> lines = new List<string>();
			foreach (string key in Crawler._MissingBooks.Keys)
				lines.Add(key + "|" + Crawler._MissingBooks[key] as string);

			string filePath = folder + "\\paper-books-missing.txt";
			Utility.WriteTextFile(filePath, lines, false);
		}

		public static int GetPaperBookEposides(string title)
		{
			string info = title.Trim();

			int pos = info.PositionOf("trọn bộ");
			if (pos > 0)
			{
				if (info.EndsWith("tập", StringComparison.OrdinalIgnoreCase))
					info = info.Replace(StringComparison.OrdinalIgnoreCase, "trọn bộ", "(trọn bộ").Replace(StringComparison.OrdinalIgnoreCase, "tập", "tập)").Trim();
			}
			else
			{
				pos = info.PositionOf("bộ");
				if (pos > 0 && info.EndsWith("cuốn", StringComparison.OrdinalIgnoreCase))
					info = info.Replace(StringComparison.OrdinalIgnoreCase, "bộ", "(bộ").Replace(StringComparison.OrdinalIgnoreCase, "cuốn", "cuốn)").Trim();
			}

			pos = info.PositionOf("hộp");
			if (pos > 0 && info.EndsWith("cuốn", StringComparison.OrdinalIgnoreCase))
				info = info.Replace(StringComparison.OrdinalIgnoreCase, "hộp", "(hộp").Replace(StringComparison.OrdinalIgnoreCase, "cuốn", "cuốn)").Trim();

			bool gotEposides = info.PositionOf("(") > 0 && (info.PositionOf("tập)") > 0 || info.PositionOf("cuốn)") > 0 || info.PositionOf("quyển)") > 0);
			if (!gotEposides)
				return 1;

			pos = -1;
			if (pos < 0)
			{
				pos = info.PositionOf("tập)");
				if (pos > 0)
					pos += 4;
			}

			if (pos < 0)
			{
				pos = info.PositionOf("cuốn)");
				if (pos > 0)
					pos += 6;
			}

			if (pos < 0)
			{
				pos = info.PositionOf("quyển)");
				if (pos > 0)
					pos += 7;
			}

			if (pos > 0)
				info = info.Left(pos);

			info = info.Substring(info.IndexOf("("));
			while (info.StartsWith("("))
				info = info.Right(info.Length - 1).Trim();

			if (info.IndexOf("(") > 0)
			{
				info = info.Substring(info.IndexOf("("));
				while (info.StartsWith("("))
					info = info.Right(info.Length - 1).Trim();
			}

			info = info.Left(info.IndexOf(")"));
			while (info.EndsWith("("))
				info = info.Left(info.Length - 1).Trim();

			info = info.Replace(StringComparison.OrdinalIgnoreCase, "trọn bộ", "").Replace(StringComparison.OrdinalIgnoreCase, "tập", "").Trim();
			info = info.Replace(StringComparison.OrdinalIgnoreCase, "quyển", "").Replace(StringComparison.OrdinalIgnoreCase, "cuốn", "").Trim();
			info = info.Replace(StringComparison.OrdinalIgnoreCase, "bộ", "").Replace(StringComparison.OrdinalIgnoreCase, "hộp", "").Trim();

			try
			{
				return Convert.ToInt32(info.Trim());
			}
			catch
			{
				return 1;
			}
		}

		public static string GetPaperBookEposides(int eposide, int eposides)
		{
			string format = eposides > 999
											? "0000"
											: eposides > 99
												? "000"
												: eposides > 9
												? "00"
												: "0";
			return " (Bộ " + eposides  + " tập) - Tập " + eposide.ToString(format);
		}

		public static string GetPaperBookTitle(string title, int eposides)
		{
			List<string> start = new List<string>() { "Bộ", "Hộp" };
			List<string> end = new List<string>() { "tập", "cuốn", "quyển" };

			string output = title.Trim();

			for (int index = 0; index < start.Count; index++)
				for (int idx = 0; idx < end.Count; idx++)
					output = output.Replace(StringComparison.OrdinalIgnoreCase, start[index] + " " + eposides + " " + end[idx], "").Replace("()", "").Trim();

			while (output.Contains("  "))
				output = output.Replace("  ", " ");

			while (output.Contains("()"))
				output = output.Replace("()", "");

			output = output.GetNormalized();
			while (output.Contains(") ("))
				output = output.Replace(") (", " - ");

			return output;
		}

		public static string GetPaperBookTitle(string title)
		{
			string bookTitle = title.Trim();

			string coverType = "";
			if (bookTitle.PositionOf("bìa cứng") > 0)
				coverType = "Bìa Cứng";
			else if (bookTitle.PositionOf("bìa mềm") > 0)
				coverType = "Bìa Mềm";

			int pos = bookTitle.PositionOf("(tái bản");
			if (pos > 0)
				bookTitle = bookTitle.Substring(0, pos).Trim();

			pos = bookTitle.PositionOf("- tái bản");
			if (pos > 0)
				bookTitle = bookTitle.Substring(0, pos).Trim();

			pos = bookTitle.PositionOf("(in lần");
			if (pos > 0)
				bookTitle = bookTitle.Substring(0, pos).Trim();

			pos = bookTitle.PositionOf("- in lần");
			if (pos > 0)
				bookTitle = bookTitle.Substring(0, pos).Trim();

			pos = bookTitle.PositionOf("(trọn bộ");
			if (pos > 0)
				bookTitle = bookTitle.Substring(0, pos).Trim();

			pos = bookTitle.PositionOf("- trọn bộ");
			if (pos > 0)
				bookTitle = bookTitle.Substring(0, pos).Trim();

			pos = bookTitle.PositionOf("(kèm");
			if (pos > 0)
				bookTitle = bookTitle.Substring(0, pos).Trim();

			pos = bookTitle.PositionOf("- kèm");
			if (pos > 0)
				bookTitle = bookTitle.Substring(0, pos).Trim();

			pos = bookTitle.PositionOf("(tặng");
			if (pos > 0)
				bookTitle = bookTitle.Substring(0, pos).Trim();

			pos = bookTitle.PositionOf("- tặng");
			if (pos > 0)
				bookTitle = bookTitle.Substring(0, pos).Trim();

			pos = bookTitle.PositionOf("(hộp");
			if (pos > 0)
				bookTitle = bookTitle.Substring(0, pos).Trim();

			pos = bookTitle.PositionOf("trọn bộ");
			if (pos > 0 && bookTitle.EndsWith("tập", StringComparison.OrdinalIgnoreCase))
				bookTitle = bookTitle.Substring(0, pos).Trim();

			pos = bookTitle.PositionOf("(");
			if (pos > 0 && bookTitle.EndsWith("tập)", StringComparison.OrdinalIgnoreCase))
				bookTitle = bookTitle.Substring(0, pos).Trim();

			pos = bookTitle.PositionOf("(Tác Phẩm");
			if (pos > 0 && bookTitle.EndsWith("Kinh Điển)", StringComparison.OrdinalIgnoreCase))
				bookTitle = bookTitle.Substring(0, pos).Trim();

			if (bookTitle.StartsWith("combo", StringComparison.OrdinalIgnoreCase))
				bookTitle = bookTitle.Right(bookTitle.Length - 5).Trim();
			else if (bookTitle.StartsWith("boxset:", StringComparison.OrdinalIgnoreCase))
				bookTitle = bookTitle.Right(bookTitle.Length - 7).Trim();
			else if (bookTitle.StartsWith("hộp sách", StringComparison.OrdinalIgnoreCase))
				bookTitle = bookTitle.Right(bookTitle.Length - 9).Trim();
			else if (bookTitle.StartsWith("hộp", StringComparison.OrdinalIgnoreCase))
				bookTitle = bookTitle.Right(bookTitle.Length - 3).Trim();
			else if (bookTitle.StartsWith("bộ", StringComparison.OrdinalIgnoreCase))
				bookTitle = bookTitle.Right(bookTitle.Length - 2).Trim();

			pos = bookTitle.PositionOf("(tập");
			if (pos > 0)
			{
				if (bookTitle.PositionOf("đến tập") > 0)
					bookTitle = bookTitle.Substring(0, pos).Trim();
				else if (bookTitle.EndsWith(")"))
				{
					bookTitle = bookTitle.Substring(0, pos).Trim() + " - " + bookTitle.Substring(pos + 1).Trim();
					if (bookTitle.EndsWith(")"))
						bookTitle = bookTitle.Left(bookTitle.Length - 1);
				}
			}

			bookTitle = bookTitle.GetNormalized();
			if (!string.IsNullOrWhiteSpace(coverType) && bookTitle.PositionOf(coverType) < 0)
				bookTitle += " (" + coverType + ")";

			return bookTitle;
		}

		internal static string NormalizeSummary(string input)
		{
			if (string.IsNullOrWhiteSpace(input))
				return string.Empty;

			string output = input.Trim();
			
			output = Utility.RemoveTag(output, "a");
			output = Utility.RemoveTag(output, "span");
			output = Utility.RemoveTag(output, "font");
			output = Utility.RemoveTagAttributes(output, "img");
			output = Utility.RemoveTagAttributes(output, "p");
			output = Utility.ClearTag(output, "div");

			output = output.Replace("\r", "").Replace("\n", "").Trim();
			try
			{
				output = Utility.RemoveWhitespaces(output).Trim();
			}
			catch { }

			List<string> beRemoved = new List<string>() {
				"Mời bạn đón đọc",
				"<strong><br/></strong>",
				"<strong>.</strong>",
				"<strong></strong>",
				"<b><br/></b>",
				"<b>.</b>",
				"<b></b>",
				"<p></p>",
				"<img>",
			};
			foreach (string str in beRemoved)
				while (output.IsContains(str))
					output = output.Replace(StringComparison.OrdinalIgnoreCase, str, "").Trim();

			List<string[]> beReplaced = new List<string[]>()
			{
				new string[] { "<p><p>", "<p>" },
				new string[] { "</p></p>", "</p>" },
				new string[] { "<br></p>", "</p>" },
				new string[] { "<br/></p>", "</p>" }
			};
			foreach (string[] str in beReplaced)
				while (output.IsContains(str[0]))
					output = output.Replace(StringComparison.OrdinalIgnoreCase, str[0], str[1]).Trim();

			return output;
		}
		#endregion

		#region Run crawler to get the listing of books on Tiki.vn
		void RunTikiCrawler()
		{
			System.Timers.Timer timer = new System.Timers.Timer();
			timer.Interval = 10;
			timer.Elapsed += new System.Timers.ElapsedEventHandler(this.OnRunTikiCrawler);
			timer.AutoReset = false;
			timer.Start();
		}

		void OnRunTikiCrawler(object sender, System.Timers.ElapsedEventArgs e)
		{
			BookSelf bookself = Tiki.InitializeBookSelf(this._crawlFolder);
			if (bookself.UrlPattern == null)
			{
				this._stopwatch.Stop();
				if (this._onProcess != null)
				{
					this._onProcess("\r\n" + "Complete the crawling process on Tiki.vn" + "\r\n");
					this._onProcess("\r\n" + "Total of times for crawling: " + this._stopwatch.GetElapsedTimes() + "\r\n");
				}

				this.Status = "Completed";
				if (this._onCompleted != null)
					this._onCompleted(this);
			}
			else if (bookself.TotalPages < 1 || bookself.CurrentPage <= bookself.TotalPages)
				Task.Run(async () =>
				{
					try
					{
						await Tiki.GetBookSelf(bookself, this._crawlFolder + "\\" + Utils.MediaFolder, this._crawlMethod, this._cancellationToken, this._onProcess, this.OnTikiCrawlerCompleted, this.OnTikiCrawlerError);
					}
					catch (OperationCanceledException)
					{
						if (this._onProcess != null)
							this._onProcess("\r\n" + "......... Canceled (crawlers) ........." + "\r\n");
					}
					catch (Exception ex)
					{
						if (this._onError != null)
							this._onError("Error: " + ex.Message, ex);
					}
				}).ConfigureAwait(false);
		}

		void OnTikiCrawlerCompleted(BookSelf bookself)
		{
			if (this._onProcess != null)
				this._onProcess("\r\n" + "Success crawl data of Tiki.vn [" + bookself.CurrentPage + "/" + (bookself.TotalPages >= bookself.CurrentPage ? bookself.TotalPages : bookself.CurrentPage) + "]: " + bookself.Url);

			Tiki.FinaIizeBookSelf(bookself, this._crawlFolder);
			this._crawledCounter++;

			if ((this.MaxAttempts < 1 || (this.MaxAttempts > 1 && this._crawledCounter < this.MaxAttempts)))
			{
				if (this._onProcess != null)
					this._onProcess((bookself.CurrentPage < bookself.TotalPages ? "Process next page [" + (bookself.CurrentPage + 1) + "/" + bookself.TotalPages + "]" : "Process next category") + " of Tiki.vn" + "\r\n");

				this.RunTikiCrawler();
			}
			else
			{
				this._stopwatch.Stop();
				if (this._onProcess != null)
				{
					this._onProcess("\r\n" + "Stop crawling when reach max-attempts < " + this.MaxAttempts + " > on Tiki.vn" + "\r\n");
					this._onProcess("\r\n" + "Total of times for crawling: " + this._stopwatch.GetElapsedTimes() + "\r\n");
				}

				this.Status = "Completed";
				if (this._onCompleted != null)
					this._onCompleted(this);
			}
		}

		void OnTikiCrawlerError(BookSelf bookself, Exception ex)
		{
			this.Status = "Error";
			if (this._onError != null)
				this._onError("Error occurred while crawling data of Tiki.vn [" + bookself.Url + "] <" + bookself.CurrentPage + "/" + bookself.TotalPages + ">", ex);
		}
		#endregion

		#region Run crawler to get the listing of books on VinaBook.com
		void RunVinaBookCrawler()
		{
			System.Timers.Timer timer = new System.Timers.Timer();
			timer.Interval = 10;
			timer.Elapsed += new System.Timers.ElapsedEventHandler(this.OnRunVinaBookCrawler);
			timer.AutoReset = false;
			timer.Start();
		}

		void OnRunVinaBookCrawler(object sender, System.Timers.ElapsedEventArgs e)
		{
			BookSelf bookself = VinaBook.InitializeBookSelf(this._crawlFolder);
			if (bookself.UrlPattern == null)
			{
				this._stopwatch.Stop();
				if (this._onProcess != null)
				{
					this._onProcess("\r\n" + "Complete the crawling process on VinaBook.com" + "\r\n");
					this._onProcess("\r\n" + "Total of times for crawling: " + this._stopwatch.GetElapsedTimes() + "\r\n");
				}

				this.Status = "Completed";
				if (this._onCompleted != null)
					this._onCompleted(this);
			}
			else if (bookself.TotalPages < 1 || bookself.CurrentPage <= bookself.TotalPages)
				Task.Run(async () =>
				{
					try
					{
						await VinaBook.GetBookSelf(bookself, this._crawlFolder + "\\" + Utils.MediaFolder, this._crawlMethod, this._cancellationToken, this._onProcess, this.OnVinaBookCrawlerCompleted, this.OnVinaBookCrawlerError);
					}
					catch (OperationCanceledException)
					{
						if (this._onProcess != null)
							this._onProcess("\r\n" + "......... Canceled (crawlers) ........." + "\r\n");
					}
					catch (Exception ex)
					{
						if (this._onError != null)
							this._onError("Error: " + ex.Message, ex);
					}
				}).ConfigureAwait(false);
		}

		void OnVinaBookCrawlerCompleted(BookSelf bookself)
		{
			VinaBook.FinaIizeBookSelf(bookself, this._crawlFolder);
			this._crawledCounter++;
			if (this._onProcess != null)
				this._onProcess("\r\n" + "Success crawl data of VinaBook.com [" + bookself.CurrentPage + "/" + bookself.TotalPages + "]: " + bookself.Url);

			if ((this.MaxAttempts < 1 || (this.MaxAttempts > 1 && this._crawledCounter < this.MaxAttempts)))
			{
				if (this._onProcess != null)
					this._onProcess((bookself.CurrentPage < bookself.TotalPages ? "Process next page [" + (bookself.CurrentPage + 1) + "/" + bookself.TotalPages + "]" : "Process next category") + " of VinaBook.com" + "\r\n");
				this.RunVinaBookCrawler();
			}
			else
			{
				this._stopwatch.Stop();
				if (this._onProcess != null)
				{
					this._onProcess("\r\n" + "Stop crawling when reach max-attempts < " + this.MaxAttempts + " > on VinaBook.com" + "\r\n");
					this._onProcess("\r\n" + "Total of times for crawling: " + this._stopwatch.GetElapsedTimes() + "\r\n");
				}

				this.Status = "Completed";
				if (this._onCompleted != null)
					this._onCompleted(this);
			}
		}

		void OnVinaBookCrawlerError(BookSelf bookself, Exception ex)
		{
			this.Status = "Error";
			if (this._onError != null)
				this._onError("Error occurred while crawling data of VinaBook.com [" + bookself.Url + "] <" + bookself.CurrentPage + "/" + bookself.TotalPages + ">", ex);
		}
		#endregion

	}
}