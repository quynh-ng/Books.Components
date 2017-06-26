#region Related components
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.IO;
using System.Diagnostics;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using net.vieapps.Components.Utility;
#endregion

namespace net.vieapps.books.Components
{
	[Serializable]
	[DebuggerDisplay("Title = {Title}, Author = {Author}")]
	public class Book
	{

		#region Constructors
		public Book()
		{
			this.Title = "";
			this.Author = "";
			this.Translator = "";
			this.Cover = "";
			this.Category = "";
			this.Original = "";
			this.Publisher = "";
			this.Producer = "";
			this.Credits = "";
			this.Source = "";
			this.SourceUri = "";
			this.Language = "vi";
			this.Tags = "";
			this.TOCs = new List<string>();
			this.Chapters = new List<string>();
			this.ChapterUrls = new List<string>();
			this.MediaFiles = new HashSet<string>();
		}
		#endregion

		#region Properties
		string _ID = "";
		public string ID
		{
			set { this._ID = value; }
			get
			{
				if (string.IsNullOrWhiteSpace(this._ID))
					this._ID = !string.IsNullOrWhiteSpace(this.Title)
										? Book.GenerateID(this.Title, this.Author)
										: "";
				return this._ID;
			}
		}

		string _PermanentID = "";
		public string PermanentID
		{
			set { this._PermanentID = value; }
			get
			{
				if (string.IsNullOrWhiteSpace(this._PermanentID))
					this._PermanentID = Utility.GetUUID();
				return this._PermanentID;
			}
		}

		public string Title { get; set; }
		public string Author { get; set; }
		public string Translator { get; set; }
		public string Cover { get; set; }
		public string Category { get; set; }
		public string Original { get; set; }
		public string Publisher{ get; set; }
		public string Producer { get; set; }
		public string Credits { get; set; }
		public string Source { get; set; }
		public string SourceUri { get; set; }
		public string Language { get; set; }
		public string Tags { get; set; }
		public string Stylesheet { get; set; }
		public List<string> TOCs { get; set; }
		public List<string> Chapters { get; set; }
		[JsonIgnore]
		public List<string> ChapterUrls { get; set; }
		[NonSerialized]
		JObject _ExtraData = new JObject();
		public JObject ExtraData
		{
			get { return this._ExtraData; }
			set { this._ExtraData = value; }
		}
		#endregion

		#region Helper properties
		[JsonIgnore]
		public List<int>  MissingChapters
		{
			get
			{
				return this._missingChapters;
			}
		}

		[JsonIgnore]
		public bool IsCompleted
		{
			get
			{
				return !(this.MissingChapters.Count > 0 || this.Chapters == null || this.Chapters.Count < 1);
			}
		}

		[JsonIgnore]
		public HashSet<string> MediaFiles { get; set; }

		[JsonIgnore]
		public string Name
		{
			get
			{
				return string.IsNullOrWhiteSpace(this.Title) ? "" : this.Title.Trim() + (string.IsNullOrWhiteSpace(this.Author) ? "" : " - " + this.Author.Trim());
			}
		}

		[JsonIgnore]
		public string Filename
		{
			get
			{
				return Utils.NormalizeFilename(this.Name);
			}
		}

		internal static string CreditsInApp
		{
			get
			{
				return "<p>Chuyển đổi và đóng gói bằng <b>vieBooks.net Converter</b></p>";
			}
		}

		internal static string DefaultMobiStylesheet
		{
			get
			{
				return @"
					h1, h2, h3, h4, h5, h6, p, div, blockquote { 
						display: block; 
						clear: both; 
						overflow: hidden; 
						text-indent: 0; 
						text-align: left; 
						margin: 0.75em 0;
					}
					h1, h2, h3, h4, h5, h6 { 
						font-family: sans-serif;
					}
					h1 { 
						font-size: 1.5em;
						font-weight: bold;
					}
					h2 { 
						font-size: 1.4em;
						font-weight: bold;
					}
					h3 { 
						font-size: 1.3em;
						font-weight: bold;
					}
					h1.title { 
						font-size: 2em;
						font-weight: bold;
						margin: 1em 0;
					}
					p.author, p.translator { 
						margin: 0.5em 0;
					}
					p, div, blockquote { 
						font-family: serif;
						line-height: 1.42857143;
					}
					p.app-credits, blockquote { 
						font-family: sans-serif;
						font-size: 0.8em;
					}
					";
			}
		}
		#endregion

		#region Static default data (categories)
		static List<string> _DefaultCategories = null;

		public static List<string> DefaultCategories
		{
			get
			{
				if (Book._DefaultCategories == null)
				{
					Book._DefaultCategories = new List<string>();
					string preDefined = @"
					Lịch Sử
					Hồi Ký - Nhân Vật
					Chính Trị - Xã Hội
					Kinh Doanh - Quản Trị
					Kinh Tế - Tài Chính
					Khoa Học - Công Nghệ
					Tâm Linh - Huyền Bí - Giả Tưởng
					Văn Học Cổ Điển
					Cổ Văn Việt Nam
					Phát Triển Cá Nhân
					Tiếu Lâm
					Cổ Tích
					Tiểu Thuyết
					Trinh Thám
					Kiếm Hiệp
					Tiên Hiệp
					Tuổi Hoa
					Truyện Ngắn
					Tuỳ Bút - Tạp Văn
					Kinh Dị
					Trung Hoa
					Ngôn Tình
					Khác";
					string[] categories = preDefined.Replace("\t", "").Replace("\r", "").Trim().Split("\n".ToCharArray());
					foreach (string category in categories)
						if (!string.IsNullOrWhiteSpace(category))
							Book._DefaultCategories.Add(category);
				}
				return Book._DefaultCategories;
			}
		}

		static Hashtable _NormalizedCategories = null;

		public static string GetCategory(string category)
		{
			if (Book._NormalizedCategories == null)
			{
				string preDefined = @"
					truyện dài|Tiểu Thuyết
					Bài viết|Khác
					Khoa học|Khoa Học - Công Nghệ
					Kinh Dị, Ma quái|Kinh Dị
					Trinh Thám, Hình Sự|Trinh Thám
					Tập Truyện ngắn|Truyện Ngắn
					Suy ngẫm, Làm Người|Phát Triển Cá Nhân
					Kỹ Năng Sống|Phát Triển Cá Nhân
					Nghệ Thuật Sống|Phát Triển Cá Nhân
					Nhân Vật Lịch sử|Lịch Sử
					Triết Học, Kinh Tế|Chính Trị - Xã Hội
					Y Học, Sức Khỏe|Chính Trị - Xã Hội
					Tình Cảm, Xã Hội|Chính Trị - Xã Hội
					Phiêu Lưu, Mạo Hiểm|Tiểu Thuyết
					Hồi Ký, Tuỳ Bút|Tuỳ Bút - Tạp Văn
					VH cổ điển nước ngoài|Văn Học Cổ Điển
					Tôn giáo, Chính Trị|Chính Trị - Xã Hội
					Truyện Tranh|Khác
					Cuộc Chiến VN|Chính Trị - Xã Hội
					Kịch, Kịch bản|Khác
					Khoa học Huyền bí|Tâm Linh - Huyền Bí - Giả Tưởng
					Khoa học, giả tưởng|Tâm Linh - Huyền Bí - Giả Tưởng
					Truyện Cười|Tiếu Lâm
					Khoa Học - Kỹ Thuật|Khoa Học - Công Nghệ
					Kinh Tế|Kinh Tế - Tài Chính
					Tài Chính|Kinh Tế - Tài Chính
					Làm Giàu|Kinh Doanh - Quản Trị
					Tuổi Học Trò|Tuổi Hoa
					Tùy Bút|Tuỳ Bút - Tạp Văn";

				Book._NormalizedCategories = new Hashtable();
				string[] categories = preDefined.Replace("\t", "").Replace("\r", "").Trim().Split("\n".ToCharArray());
				foreach (string cat in categories)
				{
					string[] catInfo = cat.Split('|');
					if (catInfo.Length > 1 && !Book._NormalizedCategories.ContainsKey(catInfo[0].ToLower()))
						Book._NormalizedCategories.Add(catInfo[0].ToLower(), catInfo[1]);
				}
			}

			string normalizedCategory = Book._NormalizedCategories.ContainsKey(category.Trim().ToLower())
																			? Book._NormalizedCategories[category.Trim().ToLower()] as string
																			: category.ToLower().GetNormalized();

			return Book.DefaultCategories.IndexOf(normalizedCategory) < 0 ? "Khác" : normalizedCategory;
		}
		#endregion

		#region Static helper methods: identities, normalize, ...
		public static string GetIdentity(string input)
		{
			string output = input;
			int pos = output.IndexOf("story=");
			if (pos > 0)
				output = output.Substring(pos + 6);
			pos = output.IndexOf("tid=");
			if (pos > 0)
				output = output.Substring(pos + 4);
			pos = output.IndexOf("&");
			if (pos > 0)
				output = output.Substring(0, pos);
			pos = output.IndexOf("#");
			if (pos > 0)
				output = output.Substring(0, pos);
			return output;
		}

		public static string GenerateID(string title, string author, string translator)
		{
			string identity = title.Trim() + (string.IsNullOrWhiteSpace(author) ? "" : " - " + author.Trim()) + (string.IsNullOrWhiteSpace(translator) ? "" : " - " + translator.Trim());
			return identity.ToLower().GetMD5();
		}

		public static string GenerateID(string title, string author)
		{
			return Book.GenerateID(title, author, null);
		}

		public static string GetMetaDataOfHtml(string html, string name)
		{
			int start = html.IndexOf("<meta name=\"book:" + name + "\"", StringComparison.OrdinalIgnoreCase);
			if (start > 0)
			{
				start = html.IndexOf("content=\"", start + 1, StringComparison.OrdinalIgnoreCase) + 9;
				int end = html.IndexOf("\"", start + 1, StringComparison.OrdinalIgnoreCase);
				return html.Substring(start, end - start).Trim();
			}
			else
				return "";
		}

		public static string GetMetaDataFromHtmlFile(string filePath, string name)
		{
			if (!File.Exists(filePath))
				return "";

			string html = Utility.ReadTextFile(filePath, 10).Aggregate((i, j) => i + "\n" + j).ToString();
			return Book.GetMetaDataOfHtml(html, name);
		}

		public static string GetDataOfJson(string json, string attribute)
		{
			string indicator = "\"" + attribute + "\":";
			int start = json.IndexOf(indicator, StringComparison.OrdinalIgnoreCase);
			start = start < 0 ? -1 : json.IndexOf("\"", start + indicator.Length, StringComparison.OrdinalIgnoreCase);
			int end = start < 0 ? -1 : json.IndexOf("\"", start + 1, StringComparison.OrdinalIgnoreCase);
			return start > -1 && end > 0 ? json.Substring(start + 1, end - start - 1).Trim() : "";
		}

		public static string GetDataFromJsonFile(string filePath, string attribute)
		{
			if (!File.Exists(filePath))
				return "";

			string json = Utility.ReadTextFile(filePath, 15).Aggregate((i, j) => i + "\n" + j).ToString();
			return Book.GetDataOfJson(json, attribute);
		}

		public static string GetIdentityFromHtmlFile(string filePath)
		{
			return Book.GetMetaDataFromHtmlFile(filePath, "PermanentID");
		}

		public static string GetIdentityFromJsonFile(string filePath)
		{
			return Book.GetDataFromJsonFile(filePath, "PermanentID");
		}

		public static string GetAuthor(string author)
		{
			string result
				= string.IsNullOrWhiteSpace(author)
					|| author.StartsWith("không rõ", StringComparison.OrdinalIgnoreCase) || author.StartsWith("không xác định", StringComparison.OrdinalIgnoreCase)
					|| author.StartsWith("sưu tầm", StringComparison.OrdinalIgnoreCase) || author.Equals("vô danh", StringComparison.OrdinalIgnoreCase)
					|| author.StartsWith("truyện ma", StringComparison.OrdinalIgnoreCase) || author.StartsWith("kiếm hiệp", StringComparison.OrdinalIgnoreCase)
					|| author.StartsWith("dân gian", StringComparison.OrdinalIgnoreCase) || author.StartsWith("cổ tích", StringComparison.OrdinalIgnoreCase)
				? "Khuyết Danh"
				: author.GetNormalized();

			result = result.Replace(StringComparison.OrdinalIgnoreCase, "(sưu tầm)", "").Replace(StringComparison.OrdinalIgnoreCase, "(dịch)", "").Trim();
			result = result.Replace(StringComparison.OrdinalIgnoreCase, "(phỏng dịch)", "").Replace(StringComparison.OrdinalIgnoreCase, "phỏng dịch", "").Trim();
			result = result.Replace(StringComparison.OrdinalIgnoreCase, "(phóng tác)", "").Replace(StringComparison.OrdinalIgnoreCase, "phóng tác", "").Trim();

			if (result.Equals("Andecxen", StringComparison.OrdinalIgnoreCase)
				|| (result.StartsWith("Hans", StringComparison.OrdinalIgnoreCase) && result.EndsWith("Andersen", StringComparison.OrdinalIgnoreCase)))
				result = "Hans Christian Andersen";
			else if (result.Equals(result.ToUpper()))
				result = result.ToLower().GetNormalized();

			return result;
		}

		public static List<string> GetAuthors(string author)
		{
			List<string> authors = new List<string>();

			string theAuthors = author.GetNormalized();
			List<string> indicators = new List<string>() { "&", " và ", " - ", "/" };
			foreach (string indicator in indicators)
			{
				int start = theAuthors.IndexOf(indicator, StringComparison.OrdinalIgnoreCase);
				while (start > -1)
				{
					authors.Add(theAuthors.Substring(0, start).GetNormalized());
					theAuthors = theAuthors.Remove(0, start + indicator.Length).Trim();
					start = theAuthors.IndexOf(indicator, StringComparison.OrdinalIgnoreCase);
				}
			}

			if (!string.IsNullOrWhiteSpace(theAuthors))
				authors.Add(theAuthors.GetNormalized());

			return authors;
		}

		public static string GetAuthorName(string author)
		{
			int start = author.IndexOf(",");
			if (start > 0)
				return author.Substring(0, start);

			string name = author.GetNormalized();
			List<string> indicators = new List<string>() { "(", "[", "{", "<" };
			foreach (string indicator in indicators)
			{
				start = name.IndexOf(indicator);
				while (start > -1)
				{
					name = name.Remove(start).Trim();
					start = name.IndexOf(indicator);
				}
			}

			indicators = new List<string>() { ".", " ", "-" };
			foreach (string indicator in indicators)
			{
				start = name.IndexOf(indicator);
				while (start > -1)
				{
					name = name.Remove(0, start + indicator.Length).Trim();
					start = name.IndexOf(indicator);
				}
			}

			return name;
		}

		public static string GetStatus(string filePath)
		{
			FileInfo jsonFile = new FileInfo(filePath + ".json");
			FileInfo epubFile = new FileInfo(filePath + ".epub");
			FileInfo mobiFile = new FileInfo(filePath + ".mobi");

			return jsonFile.Exists && epubFile.Exists && mobiFile.Exists
							? "Completed"
							: jsonFile.Exists && (epubFile.Exists || mobiFile.Exists) ? "Generating" : "Incompleted";
		}
		#endregion

		#region Helper methods: Working with JSON
		public JObject ToJson()
		{
			return this.ToJson<Book>();
		}

		public override string ToString()
		{
			return this.ToString(Newtonsoft.Json.Formatting.Indented);
		}

		public string ToString(Newtonsoft.Json.Formatting format)
		{
			return this.ToJson().ToString(format);
		}

		public void ParseJson(JObject json)
		{
			this.CopyFrom(json.FromJson<Book>());
		}

		public void ParseJson(string json)
		{
			this.CopyFrom(json.FromJson<Book>());
		}

		public static Book FromJson(JObject json)
		{
			Book book = new Components.Book();
			book.ParseJson(json);
			return book;
		}

		public static Book FromJson(string json)
		{
			Book book = new Components.Book();
			book.ParseJson(json);
			return book;
		}

		public static Book FromJsonFile(string filePath)
		{
			return !File.Exists(filePath) ? null : Components.Book.FromJson(Utility.ReadTextFile(filePath));
		}
		#endregion

		#region Helper methods: Working with HTML
		public void ParseHtml(string html)
		{
			// title
			int start = html.IndexOf("<title>", StringComparison.OrdinalIgnoreCase), end = -1;
			if (start > 0)
			{
				end = html.IndexOf("</title>", start + 1, StringComparison.OrdinalIgnoreCase);
				this.Title = html.Substring(start + 7, end - start - 7).Replace("\r", "").Replace("\n", " ").GetNormalized();
			}

			// permanent ID
			this.PermanentID = Book.GetMetaDataOfHtml(html, "PermanentID");

			// original
			this.Original = Book.GetMetaDataOfHtml(html, "Original").GetNormalized();

			// author
			start = html.IndexOf("<meta name=\"author\"", StringComparison.OrdinalIgnoreCase);
			if (start > 0)
			{
				start = html.IndexOf("content=\"", start + 1, StringComparison.OrdinalIgnoreCase) + 9;
				end = html.IndexOf("\"", start + 1, StringComparison.OrdinalIgnoreCase);
				this.Author = html.Substring(start, end - start).GetNormalized();
			}

			// translator
			this.Translator = Book.GetMetaDataOfHtml(html, "Translator").GetNormalized();

			// category
			this.Category = Book.GetMetaDataOfHtml(html, "Category").GetNormalized();

			// cover image
			this.Cover = Book.GetMetaDataOfHtml(html, "Cover").Replace(Utils.MediaFolder + "/" + this.PermanentID + "-", Utils.MediaUri);

			// source
			this.Source = Book.GetMetaDataOfHtml(html, "Source");

			// source uri
			this.SourceUri = Book.GetMetaDataOfHtml(html, "SourceUri");

			// tags
			this.Tags = Book.GetMetaDataOfHtml(html, "Tags");

			// language
			start = html.IndexOf("<meta name=\"content-language\"", StringComparison.OrdinalIgnoreCase);
			if (start > 0)
			{
				start = html.IndexOf("content=\"", start + 1, StringComparison.OrdinalIgnoreCase) + 9;
				end = html.IndexOf("\"", start + 1, StringComparison.OrdinalIgnoreCase);
				this.Language = html.Substring(start, end - start).Trim();
			}

			// stylesheet (CSS)
			start = html.IndexOf("<style", StringComparison.OrdinalIgnoreCase);
			if (start > 0)
			{
				start = html.IndexOf(">", start + 1, StringComparison.OrdinalIgnoreCase) + 1;
				end = html.IndexOf("</style>", start + 1, StringComparison.OrdinalIgnoreCase);
				this.Stylesheet = html.Substring(start, end - start).Trim();
				if (this.Stylesheet.Equals(Book.DefaultMobiStylesheet.Replace("\t", "").Trim()))
					this.Stylesheet = null;
			}

			// credits
			start = html.IndexOf("<h1 class=\"credits\">", StringComparison.OrdinalIgnoreCase);
			if (start > 0)
			{
				start = html.IndexOf("<p", start + 1, StringComparison.OrdinalIgnoreCase);
				end = html.IndexOf(Book.PageBreak, start + 1, StringComparison.OrdinalIgnoreCase);
				this.Credits = html.Substring(start, end - start).Trim();
				if (!this.Credits.Equals(""))
				{
					this.Credits = this.Credits.Replace(StringComparison.OrdinalIgnoreCase, Book.CreditsInApp, "").Trim();
					if (!this.Source.Equals(""))
						this.Credits = this.Credits.Replace(StringComparison.OrdinalIgnoreCase, "<p>Nguồn: " + this.Source + "</p>", "").Trim();
				}
			}

			// TOC
			start = html.IndexOf("<h1 class=\"toc\">", StringComparison.OrdinalIgnoreCase);
			if (start > 0)
			{
				start = html.IndexOf("<p", start + 1, StringComparison.OrdinalIgnoreCase);
				end = html.IndexOf(Book.PageBreak, start + 1, StringComparison.OrdinalIgnoreCase);
				string[] tocs = html.Substring(start, end - start).Trim().Replace("\r", "").Split("\n".ToCharArray());
				this.TOCs = new List<string>();
				foreach (string toc in tocs)
					if (!string.IsNullOrWhiteSpace(toc))
						this.TOCs.Add(toc);
			}
			else
				this.TOCs.Clear();

			// chapters (body)
			this.Chapters = new List<string>();
			start = html.IndexOf(Book.PageBreak, start + 1, StringComparison.OrdinalIgnoreCase);
			while (start > -1)
			{
				end = html.IndexOf(Book.PageBreak, start + 1, StringComparison.OrdinalIgnoreCase);
				if (end < 0)
					end = html.IndexOf("</body>", start + 1, StringComparison.OrdinalIgnoreCase);
				if (start > 0 && end > 0)
				{
					string chapter = html.Substring(start, end - start).Replace(Book.PageBreak, "").Trim();
					if (chapter.StartsWith("<a name=\""))
						chapter = chapter.Substring(chapter.IndexOf("</a>") + 4).Trim();
					this.Chapters.Add(chapter.Replace(Utils.MediaFolder + "/" + this.PermanentID + "-", Utils.MediaUri));
				}
				start = html.IndexOf(Book.PageBreak, start + 1, StringComparison.OrdinalIgnoreCase);
			}
		}
		#endregion

		#region Helper methods: Working with JSON files
		public static void GenerateJsonFile(Book book, string folder, string filename)
		{
			string filePath = (string.IsNullOrWhiteSpace(folder) ? "" : folder + "\\") + (string.IsNullOrWhiteSpace(filename) ? book.ID : filename.Trim()) + ".json";
			Utility.WriteTextFile(filePath, book.ToString(Newtonsoft.Json.Formatting.Indented), false);
		}
		#endregion

		#region Helper methods: Working with EPUB files
		public static void GenerateEpubFile(Book book, string folder, string filename, Action<Book, string> onCompleted, Action<Book, Exception> onError)
		{
			try
			{
				// prepare
				List<string> navs = new List<string>(), pages = new List<string>();
				for (int index = 0; index < book.Chapters.Count; index++)
				{
					navs.Add(book.GetTOCItem(index));
					pages.Add(book.Chapters[index]);
				}

				// meta data
				Epub.Document epub = new Epub.Document();
				epub.AddBookIdentifier(Utility.GetUUID(book.ID));
				epub.AddLanguage(book.Language);
				epub.AddTitle(book.Title);
				epub.AddAuthor(book.Author);
				epub.AddMetaItem("dc:contributor", Book.CreditsInApp.Replace("\n<p>", " - ").Replace("\n", "").Replace("<p>", "").Replace("</p>", "").Replace("<b>", "").Replace("</b>", "").Replace("<i>", "").Replace("</i>", ""));

				if (!string.IsNullOrWhiteSpace(book.Translator))
					epub.AddTranslator(book.Translator);

				if (!string.IsNullOrWhiteSpace(book.Original))
					epub.AddMetaItem("book:Original", book.Original);

				if (!string.IsNullOrWhiteSpace(book.Publisher))
					epub.AddMetaItem("book:Publisher", book.Publisher);

				if (!string.IsNullOrWhiteSpace(book.Producer))
					epub.AddMetaItem("book:Producer", book.Producer);

				if (!string.IsNullOrWhiteSpace(book.Source))
					epub.AddMetaItem("book:Source", book.Source);

				if (!string.IsNullOrWhiteSpace(book.SourceUri))
					epub.AddMetaItem("book:SourceUri", book.SourceUri);

				// CSS stylesheet
				string stylesheet = !string.IsNullOrWhiteSpace(book.Stylesheet)
													? book.Stylesheet
													: @"
													h1, h2, h3, h4, h5, h6, p, div, blockquote { 
														display: block; 
														clear: both; 
														overflow: hidden; 
														text-indent: 0; 
														text-align: left; 
														padding: 0;
														margin: 0.5em 0;
													}
													h1, h2, h3, h4, h5, h6 { 
														font-family: sans-serif;
													}
													h1 { 
														font-size: 1.4em;
														font-weight: bold;
													}
													h2 { 
														font-size: 1.3em;
														font-weight: bold;
													}
													h3 { 
														font-size: 1.2em;
														font-weight: bold;
													}
													h1.title { 
														font-size: 1.8em;
														font-weight: bold;
														margin: 1em 0;
													}
													p, div, blockquote { 
														font-family: serif;
														line-height: 1.42857143;
													}
													p.author { 
														font-family: serif;
														font-weight: bold;
														font-size: 0.9em;
														text-transform: uppercase;
													}
													div.app-credits>p, p.info, blockquote { 
														font-family: sans-serif;
														font-size: 0.8em;
													}".Replace("\t", "");

				epub.AddStylesheetData("style.css", stylesheet);

				// cover image
				if (!string.IsNullOrWhiteSpace(book.Cover))
				{
					string coverFilename = Utils.NormalizeMediaFiles(book.Cover, book.PermanentID);
					byte[] coverData = Utility.ReadFile((string.IsNullOrWhiteSpace(folder) ? "" : folder + "\\") + coverFilename);
					if (coverData != null && coverData.Length > 0)
					{
						string coverId = epub.AddImageData(coverFilename, coverData);
						epub.AddMetaItem("cover", coverId);
					}
				}

				// pages & nav points
				string pageTemplate = @"<!DOCTYPE html>
				<html xmlns=""http://www.w3.org/1999/xhtml"">
					<head>
						<title>{0}</title>
						<meta http-equiv=""Content-Type"" content=""text/html; charset=utf-8""/>
						<link type=""text/css"" rel=""stylesheet"" href=""style.css""/>
						<style type=""text/css"">
							@page {
								padding: 0;
								margin: 0;
							}
						</style>
					</head>
					<body>
						{1}
					</body>
				</html>".Trim().Replace("\t", "");

				// info
				string info = "<p class=\"author\">" + book.Author + "</p>"
										+ "<h1 class=\"title\">" + book.Title + "</h1>";

				if (!string.IsNullOrWhiteSpace(book.Original))
					info += "<p class=\"info\">" + (book.Language.Equals("en") ? "Original: " : "Nguyên tác: ") + "<b><i>" + book.Original + "</i></b></p>";

				info += "<hr/>";

				if (!string.IsNullOrWhiteSpace(book.Translator))
					info += "<p class=\"info\">" + (book.Language.Equals("en") ? "Translator: " : "Dịch giả: ") + "<b><i>" + book.Translator + "</i></b></p>";

				if (!string.IsNullOrWhiteSpace(book.Publisher))
					info += "<p class=\"info\">" + (book.Language.Equals("en") ? "Pubisher: " : "NXB: ") + "<b><i>" + book.Publisher + "</i></b></p>";

				if (!string.IsNullOrWhiteSpace(book.Producer))
					info += "<p class=\"info\">" + (book.Language.Equals("en") ? "Producer: " : "Sản xuất: ") + "<b><i>" + book.Producer + "</i></b></p>";

				info += "<div class=\"credits\">"
						+ (!string.IsNullOrWhiteSpace(book.Source) ? "<p>" + (book.Language.Equals("en") ? "Source: " : "Nguồn: ") + "<b><i>" + book.Source + "</i></b></p>" : "")
						+ (!string.IsNullOrWhiteSpace(book.Credits) ? book.Credits : "")
						+ "\r" + "<hr/>" + Book.CreditsInApp + "</div>";

				epub.AddXhtmlData("page0.xhtml", pageTemplate.Replace("{0}", "Info").Replace("{1}", info.Replace("<p>", "\r" + "<p>")));

				// chapters
				for (int index = 0; index < pages.Count; index++)
				{
					string name = string.Format("page{0}.xhtml", index + 1);
					string content = Utils.NormalizeMediaFiles(pages[index], book.PermanentID);

					int start = content.IndexOf("<img", StringComparison.OrdinalIgnoreCase), end = -1;
					while (start > -1)
					{
						start = content.IndexOf("src=", start + 1, StringComparison.OrdinalIgnoreCase) + 5;
						char @char = content[start - 1];
						end = content.IndexOf(@char.ToString(), start + 1, StringComparison.OrdinalIgnoreCase);

						string image = content.Substring(start, end - start);
						byte[] imageData = Utility.ReadFile((string.IsNullOrWhiteSpace(folder) ? "" : folder + @"\") + image);
						if (imageData != null && imageData.Length > 0)
							epub.AddImageData(image, imageData);

						start = content.IndexOf("<img", start + 1, StringComparison.OrdinalIgnoreCase);
					}

					epub.AddXhtmlData(name, pageTemplate.Replace("{0}", index < navs.Count ? navs[index] : book.Title).Replace("{1}", content.Replace("<p>", "\r" + "<p>")));
					if (book.Chapters.Count > 1)
						epub.AddNavPoint(index < navs.Count ? navs[index] : book.Title + " - " + (index + 1).ToString(), name, index + 1);
				}

				// save into file on disc
				string filePath = (string.IsNullOrWhiteSpace(folder) ? "" : folder + "\\") + (string.IsNullOrWhiteSpace(filename) ? book.ID : filename.Trim()) + ".epub";
				epub.Generate(filePath);

				// callback on completed
				if (onCompleted != null)
					onCompleted(book, filePath);
			}
			catch (Exception ex)
			{
				// callback on got error
				if (onError != null)
					onError(book, ex);
			}
		}

		public static void GenerateEpubFile(Book book, string folder, string filename, Action<Book, string> onCompleted)
		{
			Book.GenerateEpubFile(book, folder, filename, onCompleted, null);
		}
		#endregion

		#region Helper methods: Working with MOBI files
		public static int GenerateMobiFile(string generator, string opfFilePath, EventHandler onCompleted)
		{
			return Utility.RunProcess(string.IsNullOrWhiteSpace(generator) ? "VIEApps.Books.Kindlegen.dll" : generator.Trim(), opfFilePath, onCompleted);
		}

		public static string PageBreak { get { return "<mbp:pagebreak/>"; } }

		public static void GenerateMobiData(Book book, string folder, string filename, Action<Book, string, string> onCompleted, Action<Book, string, string, Exception> onError)
		{
			try
			{
				// prepare HTML
				string content = "<!DOCTYPE html>" + "\n"
						+ "<html xmlns=\"http://www.w3.org/1999/xhtml\">" + "\n"
						+ "<head>" + "\n"
						+ "<title>" + book.Title + "</title>" + "\n"
						+ "<meta http-equiv=\"content-type\" content=\"text/html; charset=utf-8\"/>" + "\n"
						+ "<meta name=\"content-language\" content=\"" + book.Language + "\"/>" + "\n"
						+ (string.IsNullOrWhiteSpace(book.Author) ? "" : "<meta name=\"author\" content=\"" + book.Author + "\"/>" + "\n")
						+ (string.IsNullOrWhiteSpace(book.Original) ? "" : "<meta name=\"book:Original\" content=\"" + book.Original + "\"/>" + "\n")
						+ (string.IsNullOrWhiteSpace(book.Translator) ? "" : "<meta name=\"book:Translator\" content=\"" + book.Translator + "\"/>" + "\n")
						+ (string.IsNullOrWhiteSpace(book.Category) ? "" : "<meta name=\"book:Category\" content=\"" + book.Category + "\"/>" + "\n")
						+ (string.IsNullOrWhiteSpace(book.Cover) ? "" : "<meta name=\"book:Cover\" content=\"" + Utils.NormalizeMediaFiles(book.Cover, book.PermanentID) + "\"/>" + "\n")
						+ (string.IsNullOrWhiteSpace(book.Publisher) ? "" : "<meta name=\"book:Publisher\" content=\"" + book.Publisher + "\"/>" + "\n")
						+ (string.IsNullOrWhiteSpace(book.Producer) ? "" : "<meta name=\"book:Publisher\" content=\"" + book.Producer + "\"/>" + "\n")
						+ (string.IsNullOrWhiteSpace(book.Source) ? "" : "<meta name=\"book:Source\" content=\"" + book.Source + "\"/>" + "\n")
						+ (string.IsNullOrWhiteSpace(book.SourceUri) ? "" : "<meta name=\"book:SourceUri\" content=\"" + book.SourceUri + "\"/>" + "\n")
						+ (string.IsNullOrWhiteSpace(book.Tags) ? "" : "<meta name=\"book:Tags\" content=\"" + book.Tags + "\"/>" + "\n")
						+ "<meta name=\"book:PermanentID\" content=\"" + book.PermanentID + "\"/>" + "\n"
						+ "<style type=\"text/css\">" + (string.IsNullOrWhiteSpace(book.Stylesheet) ? Book.DefaultMobiStylesheet : book.Stylesheet).Replace("\t", "") + "</style>" + "\n"
						+ "</head>" + "\n"
						+ "<body>" + "\n"
						+ "<a name=\"start\"></a>" + "\n"
						+ (string.IsNullOrWhiteSpace(book.Author) ? "" : "<p class=\"author\">" + book.Author + "</p>" + "\n")
						+ "<h1 class=\"title\">" + book.Title + "</h1>" + "\n"
						+ (string.IsNullOrWhiteSpace(book.Translator) ? "" : "<p class=\"translator\">Dịch giả: " + book.Translator + "</p>" + "\n")
						+ "\n";

				content += Book.PageBreak
						+ "\n"
						+ "<h1 class=\"credits\">" + (book.Language.Equals("en") ? "INFO" : "THÔNG TIN") + "</h1>"
						+ (!string.IsNullOrWhiteSpace(book.Publisher) ? "\n<p>" + (book.Language.Equals("en") ? "Publisher: " : "NXB: ") + book.Publisher + "</p>" : "")
						+ (!string.IsNullOrWhiteSpace(book.Producer) ? "\n<p>" + (book.Language.Equals("en") ? "Producer: " : "Sản xuất: ") + book.Producer + "</p>" : "")
						+ (!string.IsNullOrWhiteSpace(book.Source) ? "\n<p>" + (book.Language.Equals("en") ? "Source: " : "Nguồn: ") + book.Source + "</p>" : "")
						+ (!string.IsNullOrWhiteSpace(book.Credits) ? "\n" + book.Credits : "")
						+ "\n" + Book.CreditsInApp + "\n\n";

				string[] headingTags = "h1|h2|h3|h4|h5|h6".Split('|');
				string toc = "", body = "";
				if (book.Chapters != null && book.Chapters.Count > 0)
					for (int index = 0; index < book.Chapters.Count; index++)
					{
						string chapter = Utils.NormalizeMediaFiles(book.Chapters[index], book.PermanentID);
						foreach (string tag in headingTags)
							chapter = chapter.Replace(StringComparison.OrdinalIgnoreCase, "<" + tag + ">", "\n<" + tag + ">").Replace(StringComparison.OrdinalIgnoreCase, "</" + tag + ">", "</" + tag + ">\n");
						chapter = chapter.Trim().Replace("</p><p>", "</p>\n<p>").Replace("\n\n", "\n");

						toc += book.Chapters.Count > 1 ? (!toc.Equals("") ? "\n" : "") + "<p><a href=\"#chapter" + (index + 1) + "\">" + book.GetTOCItem(index) + "</a></p>" : "";
						body += Book.PageBreak + "\n"
									+ "<a name=\"chapter" + (index + 1) + "\"></a>" + "\n"
									+ chapter
									+ "\n\n";
					}

				if (!string.IsNullOrWhiteSpace(toc))
					content += Book.PageBreak
						+ "\n"
						+ "<a name=\"toc\"></a>" + "\n"
						+ "<h1 class=\"toc\">" + (book.Language.Equals("en") ? "TABLE OF CONTENTS: " : "MỤC LỤC") + "</h1>" + "\n"
						+ toc
						+ "\n\n";

				content += body + "</body>\n" + "</html>";

				// geneate HTML file
				string dataFilename = (string.IsNullOrWhiteSpace(filename) ? book.ID : filename.Trim()).ToLower().GetMD5();
				string filePath = (string.IsNullOrWhiteSpace(folder) ? "" : folder + "\\") + dataFilename + ".html";
				Utility.WriteTextFile(filePath, content, false);

				// prepare NCX
				if (book.TOCs != null && book.TOCs.Count > 0)
				{
					content = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" + "\n"
							+ "<!DOCTYPE ncx PUBLIC \" -//NISO//DTD ncx 2005-1//EN\" \"http://www.daisy.org/z3986/2005/ncx-2005-1.dtd\">" + "\n"
							+ "<ncx xmlns=\"http://www.daisy.org/z3986/2005/ncx/\">" + "\n"
							+ "<head>" + "\n"
							+ "<meta name=\"dtb:uid\" content=\"urn:uuid:dbe" + Utility.GetUUID(book.ID) + "\"/>" + "\n"
							+ "</head>" + "\n"
							+ "<docTitle><text>" + book.Title + @"</text></docTitle>" + "\n"
							+ "<docAuthor><text>" + book.Author + @"</text></docAuthor>" + "\n"
							+ "<navMap>";

					for (int index = 0; index < book.TOCs.Count; index++)
						content += "<navPoint id=\"navid" + (index + 1) + "\" playOrder=\"" + (index + 1) + "\">" + "\n"
							+ "<navLabel>" + "\n"
							+ "<text>" + book.GetTOCItem(index) + "</text>" + "\n"
							+ "</navLabel>" + "\n"
							+ "<content src=\"" + dataFilename + ".html#chapter" + (index + 1) + "\"/>" + "\n"
							+ "</navPoint>" + "\n";

					content += "</navMap>" + "\n"
							+ "</ncx>";

					// geneate NCX file
					filePath = (string.IsNullOrWhiteSpace(folder) ? "" : folder + "\\") + dataFilename + ".ncx";
					Utility.WriteTextFile(filePath, content, false);
				}

				// prepare OPF
				content = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" + "\n"
					+ "<package xmlns=\"http://www.idpf.org/2007/opf\" version=\"2.0\" unique-identifier=\"" + Utility.GetUUID(book.ID) + "\">" + "\n"
					+ "<metadata xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:opf=\"http://www.idpf.org/2007/opf\">" + "\n"
					+ "<dc:Title>" + book.Title + @"</dc:Title>" + "\n"
					+ "<dc:Creator opf:role=\"aut\">" + book.Author + @"</dc:Creator>" + "\n"
					+ (string.IsNullOrWhiteSpace(book.Publisher) ? "" : "<dc:Publisher>" + book.Publisher + "</dc:Publisher>" + "\n")
					+ "<dc:Language>" + book.Language + "</dc:Language>" + "\n"
					+ "<dc:Contributor>vieBooks.net Converter</dc:Contributor>" + "\n"
					+ "</metadata> " + "\n"
					+ "<manifest>" + "\n"
					+ "<item id=\"ncx\" media-type=\"application/x-dtbncx+xml\" href=\"" + dataFilename + ".ncx\"/> " + "\n"
					+ (string.IsNullOrWhiteSpace(book.Cover) ? "" : "<item id=\"cover\" media-type=\"image/" + (book.Cover.EndsWith(".png") ? "png" : book.Cover.EndsWith(".gif") ? "gif" : "jpeg") + "\" href=\"" + Utils.NormalizeMediaFiles(book.Cover, book.PermanentID) + "\"/>" + "\n")
					+ "<item id=\"contents\" media-type=\"application/xhtml+xml\" href=\"" + dataFilename + ".html\"/> " + "\n"
					+ "</manifest>" + "\n"
					+ "<spine toc=\"" + (book.TOCs != null && book.TOCs.Count > 0 ? "ncx" : "toc") + "\">" + "\n"
					+ "<itemref idref=\"contents\"/>" + "\n"
					+ "</spine>" + "\n"
					+ "<guide>" + "\n"
					+ (string.IsNullOrWhiteSpace(book.Cover) ? "" : "<reference type=\"cover\" title=\"Cover\" href=\"" + Utils.NormalizeMediaFiles(book.Cover, book.PermanentID) + "\"/>" + "\n")
					+ "<reference type=\"toc\" title=\"Table of Contents\" href=\"" + dataFilename + ".html#toc\"/>" + "\n"
					+ "<reference type=\"text\" title=\"Starting Point\" href=\"" + dataFilename + ".html#start\"/>" + "\n"
					+ "</guide>" + "\n"
					+ "</package>";

				// generate OPF
				filePath = (string.IsNullOrWhiteSpace(folder) ? "" : folder + "\\") + (string.IsNullOrWhiteSpace(filename) ? book.ID : filename.Trim()) + ".opf";
				Utility.WriteTextFile(filePath, content, false);

				// callback on completed
				if (onCompleted != null)
					onCompleted(book, folder, filename);
			}
			catch (Exception ex)
			{
				if (onError != null)
					onError(book, folder, filename, ex);
			}
		}
		#endregion

		#region Helper methods for working with files
		internal static string[] DataFileExtentions = ".json|.epub|.mobi|.html|.opf|.ncx".Split('|');
		internal static string[] TempDataFileExtentions = ".html|.opf|.ncx".Split('|');

		public static void RenameFiles(string path)
		{
			// prepare
			string[] filePaths = Utility.GetFileParts(path);
			string filePath = filePaths[0] + "\\" + filePaths[1];
			string filePathMD5 = filePaths[0] + "\\" + filePaths[1].ToLower().GetMD5();

			// rename files
			foreach (string ext in Book.DataFileExtentions)
			{
				string sourceFilePath = filePathMD5 + ext;
				string destinationFilePath = filePath + ext;
				if (File.Exists(sourceFilePath) && !sourceFilePath.IsEquals(destinationFilePath))
					File.Move(sourceFilePath, destinationFilePath);
			}
		}

		public static void CleanFiles(string path)
		{
			// prepare
			string[] filePaths = Utility.GetFileParts(path);
			string filePath = filePaths[0] + "\\" + filePaths[1];
			string filePathMD5 = filePaths[0] + "\\" + filePaths[1].ToLower().GetMD5();

			// clean files
			foreach (string ext in Book.TempDataFileExtentions)
			{
				if (File.Exists(filePath + ext))
					try
					{
						File.Delete(filePath + ext);
					}
					catch { }

				if (File.Exists(filePathMD5 + ext))
					try
					{
						File.Delete(filePathMD5 + ext);
					}
					catch { }
			}
		}

		public static void MoveFiles(string sourceFolder, string destinationFolder, string filename, bool moveMediaFiles, string identifier, bool deleteOnMoved)
		{
			// prepare
			string sourcePath = string.IsNullOrWhiteSpace(sourceFolder) ? "" : sourceFolder.Trim() + "\\";
			string destinationPath = string.IsNullOrWhiteSpace(destinationFolder) ? "" : destinationFolder.Trim() + "\\";

			// stop move if source path is equals to destination path
			if (sourcePath.IsEquals(destinationPath))
				return;

			// move data files
			string filenameMD5 = filename.ToLower().GetMD5();
			foreach(string ext in Book.DataFileExtentions)
			{
				if (File.Exists(sourcePath + filenameMD5 + ext))
				{
					if (File.Exists(destinationPath + filename + ext))
						try
						{
							File.Delete(destinationPath + filename + ext);
						}
						catch { }

					if (deleteOnMoved)
						File.Move(sourcePath + filenameMD5 + ext, destinationPath + filename + ext);
					else
						File.Copy(sourcePath + filenameMD5 + ext, destinationPath + filename + ext, true);
				}
				else if (File.Exists(sourcePath + filename + ext))
				{
					if (File.Exists(destinationPath + filename + ext))
						try
						{
							File.Delete(destinationPath + filename + ext);
						}
						catch { }

					if (deleteOnMoved)
						File.Move(sourcePath + filename + ext, destinationPath + filename + ext);
					else
						File.Copy(sourcePath + filename + ext, destinationPath + filename + ext, true);
				}
			}

			// move media files
			if (moveMediaFiles && !string.IsNullOrWhiteSpace(identifier))
			{
				List<FileInfo> files = Utility.GetFiles(sourcePath + "\\" + Utils.MediaFolder, identifier + "-*.*");
				if (files != null && files.Count > 0)
					foreach (FileInfo file in files)
					{
						if (File.Exists(destinationPath + "\\" + Utils.MediaFolder + "\\" + file.Name))
							try
							{
								File.Delete(destinationPath + "\\" + Utils.MediaFolder + "\\" + file.Name);
							}
							catch { }

						if (deleteOnMoved)
							File.Move(file.FullName, destinationPath + "\\" + Utils.MediaFolder + "\\" + file.Name);
						else
							File.Copy(file.FullName, destinationPath + "\\" + Utils.MediaFolder + "\\" + file.Name, true);
					}
			}
		}

		public static void MoveFiles(string sourceFolder, string destinationFolder, string filename, bool moveMediaFiles, string identifier, bool deleteOnMoved, Action onCompleted)
		{
			Book.MoveFiles(sourceFolder, destinationFolder, filename, moveMediaFiles, identifier, deleteOnMoved);
			if (onCompleted != null)
				onCompleted();
		}

		public static void MoveFiles(Book book, string sourceFolder, string destinationFolder, bool moveMediaFiles, bool deleteOnMoved, Action<Book> onCompleted)
		{
			Book.MoveFiles(sourceFolder, destinationFolder, book.Filename, moveMediaFiles, book.PermanentID, deleteOnMoved);
			if (onCompleted != null)
				onCompleted(book);
		}
		#endregion

		#region Helper methods: verify, normalize, get media files/TOC title/anchor,  ...
		List<int> _missingChapters = new List<int>();

		public void Verify()
		{
			this.ChapterUrls = new List<string>();
			if (this.Chapters == null || this.Chapters.Count < 1)
			{
				this.Chapters = new List<string>();
				this.TOCs = new List<string>();
			}

			for (int index = 0; index < this.Chapters.Count; index++)
			{
				if (this.Chapters[index].Equals("") || this.Chapters[index].StartsWith("http://"))
					this._missingChapters.Add(index);
				else
				{
					int pos = this.Chapters[index].IndexOf("</h1>");
					string body = pos > 0 ? this.Chapters[index].Substring(pos + 5).Trim() : this.Chapters[index];
					if (body.Equals("<!-- chapter navigator -->"))
					{
						this.Chapters[index] = "";
						this._missingChapters.Add(index);
					}
				}

				this.ChapterUrls.Add(this.Chapters[index].StartsWith("http://") ? this.Chapters[index] : "");
			}
		}

		public void Normalize()
		{
			if (this.Chapters.Count.Equals(2) && this.Chapters[0].Equals(this.Chapters[1]))
			{
				this.TOCs.Clear();
				this.Chapters.RemoveAt(0);
			}

			if (this.Category.Equals("Truyện Ngắn") && this.Chapters.Count > 9 && this.Title.IndexOf("Truyện Ngắn", StringComparison.OrdinalIgnoreCase) < 0)
				this.Category = "Tiểu Thuyết";
			else if (this.Category.Equals("Phát Triển Cá Nhân") && this.Author.Equals("Aziz Nesin"))
				this.Category = "Truyện Ngắn";

			this.NormalizeChapters();
			this.NormalizeTOCs();
		}

		public void NormalizeChapters()
		{
			if (this.Chapters == null || this.Chapters.Count < 1)
				return;

			else if (this.Chapters.Count < 2)
			{
				if (this.Source.Equals("isach.info"))
					this.Chapters[0] = ISach.NormalizeBody(this.Chapters[0], 1);
				else if (this.Source.Equals("vnthuquan.net"))
					this.Chapters[0] = VNThuQuan.NormalizeBody(this.Chapters[0]);
			}

			else
				for (int index = 0; index < this.Chapters.Count; index++)
				{
					if (this.Chapters[index].StartsWith("http") || !this.Chapters[index].StartsWith("<h1>"))
						continue;

					int pos = this.Chapters[index].IndexOf("</h1>");

					string body = pos > 0 ? this.Chapters[index].Substring(pos + 5).Trim() : this.Chapters[index];

					if (body.Equals("<!-- chapter navigator -->"))
						this.Chapters[index] = "";

					else
					{
						if (this.Source.Equals("isach.info"))
							body = ISach.NormalizeBody(body, this.Chapters.Count);
						else if (this.Source.Equals("vnthuquan.net"))
							body = VNThuQuan.NormalizeBody(body);

						string tocTitle = this.TOCs != null && this.TOCs.Count > index ? this.GetTOCItem(index) : "";
						string title = pos > 0 ? this.Chapters[index].Substring(4, pos - 4).Trim() : "";
						title = string.IsNullOrWhiteSpace(tocTitle) ? title : tocTitle;

						if (title.Equals("Chương 1 -", StringComparison.OrdinalIgnoreCase) || title.Equals("Chương 1:", StringComparison.OrdinalIgnoreCase))
							title += " Giới thiệu";

						else if ((title.StartsWith("Chương", StringComparison.OrdinalIgnoreCase) && title.IndexOf(":") > 0)
							|| (title.StartsWith("Hồi", StringComparison.OrdinalIgnoreCase) && title.IndexOf(":") > 0))
						{
							pos = title.IndexOf(":");
							string left = title.Left(pos + 1).GetNormalized();
							if (left[left.Length - 2].Equals(' '))
								left = left.Remove(left.Length - 2, 1);
							string right = title.Substring(pos + 1).GetNormalized();
							title = left + " " + right;
						}
						else
							title = title.GetNormalized();

						this.Chapters[index] = (!string.IsNullOrWhiteSpace(title) ? "<h1>" + title + "</h1>" : "") + body.Replace("\r", "").Replace("\n", "");
					}
				}
		}

		public void NormalizeTOCs()
		{
			if (this.Chapters == null || this.Chapters.Count < 2)
				return;

			List<string> tocs = new List<string>();
			for (int index = 0; index < this.Chapters.Count; index++)
			{
				string title = null;
				if (this.Chapters[index].StartsWith("<h1>"))
				{
					int pos = this.Chapters[index].IndexOf("</h1>");
					title = (pos > 0 ? this.Chapters[index].Substring(4, pos - 4) : "").Trim();
				}

				if (title == null && this.TOCs != null && this.TOCs.Count > index)
					tocs.Add(!string.IsNullOrWhiteSpace(this.TOCs[index]) ? this.TOCs[index] : (index + 1).ToString() + ".");
				else
					tocs.Add(!string.IsNullOrWhiteSpace(title) ? title : (index + 1).ToString() + ".");
			}

			this.TOCs = tocs;
		}

		public string GetTOCItem(int index)
		{
			string toc = this.TOCs != null && index < this.TOCs.Count ? this.TOCs[index] : "";
			if (!string.IsNullOrWhiteSpace(toc))
			{
				toc = Utility.RemoveTag(toc, "a");
				toc = Utility.RemoveTag(toc, "p");
			}
			return string.IsNullOrWhiteSpace(toc) ? (index + 1).ToString() : toc.GetNormalized().Replace("{0}", (index + 1).ToString());
		}

		public void GetMediaFiles()
		{
			if (!this.Cover.Equals(""))
				this.MediaFiles.Add(this.Cover.Replace(Utils.MediaUri, ""));

			for (int index = 0; index < this.Chapters.Count; index++)
			{
				object[] data = Utils.NormalizeMediaFiles(this.Chapters[index]);
				if (data == null || data.Length < 2)
					continue;

				List<string> images = data[1] as List<string>;
				if (images != null && images.Count > 0)
					foreach (string image in images)
						this.MediaFiles.Append(image.Replace(Utils.MediaUri, ""));
			}
		}

		public override int GetHashCode()
		{
			return this.PermanentID.GetHashCode();
		}
		#endregion

	}

	public class BookSelf
	{

		#region Constructors
		public BookSelf()
		{
			this.Url = "";
			this.UrlPattern = "";
			this.UrlParameters = new List<string>();
			this.TotalPages = 0;
			this.CurrentPage = 1;
			this.CategoryIndex = 0;
		}
		#endregion

		#region Properties
		public string Url { get; set; }
		public string UrlPattern { get; set; }
		public List<string> UrlParameters { get; set; }
		public int TotalPages { get; set; }
		public int CurrentPage { get; set; }
		public List<Book> Books { get; set; }
		public int CategoryIndex { get; set; }
		#endregion

	}

}