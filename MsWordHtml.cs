#region Related components
using System;
using System.IO;

using net.vieapps.Components.Utility;
#endregion

namespace net.vieapps.books.Components
{
	public static class MsWordHtml
	{
		public static Book ToBook(string html, string mediaSourceFolder, string mediaDestinationFolder, string chapterTag, Action<Book> onCompleted)
		{
			Book book = new Book();
			book.PermanentID = Utility.GetUUID();

			// title & author
			int start = html.IndexOf("<title>", StringComparison.OrdinalIgnoreCase), end = -1;
			if (start > 0)
			{
				end = html.IndexOf("</title>", start + 1, StringComparison.OrdinalIgnoreCase);
				if (end > 0)
				{
					string info = html.Substring(start + 7, end - start - 7).Replace("\r", " ").Replace("\n", "").Trim();
					start = info.IndexOf("-");
					if (start > 0)
					{
						book.Title = info.Substring(0, start).Trim();
						book.Author = info.Substring(start + 1).Trim();
					}
					else
						book.Title = info;
				}
			}

			// cover image
			start = html.IndexOf("<p>", StringComparison.OrdinalIgnoreCase);
			end = html.IndexOf("<img", start);
			if (start > 0 && end > 0 && end < html.IndexOf("</p>", start))
			{
				end = html.IndexOf("</p>", start);
				string info = html.Substring(start, end - start ).Trim();
				start = info.IndexOf("src=\"");
				end = info.IndexOf("\"", start + 5);
				book.Cover = info.Substring(start + 5, end - start - 5);
			}

			// TOC
			string tag = string.IsNullOrWhiteSpace(chapterTag) ? "h1" : chapterTag.Trim().ToLower(), toc = "";
			start = html.IndexOf("<p>", StringComparison.OrdinalIgnoreCase);
			end = html.IndexOf("MỤC LỤC", start, StringComparison.OrdinalIgnoreCase);
			if (start > 0 && end > 0)
			{
				start = html.IndexOf("<p>", end, StringComparison.OrdinalIgnoreCase);
				end = html.IndexOf("<div", start, StringComparison.OrdinalIgnoreCase);
				if (end < 0)
					end = html.IndexOf("<" + tag, start);

				if (start > 0 && end > 0)
				{
					toc = html.Substring(start, end - start);
					start = end;
					end = html.IndexOf("</body>", StringComparison.OrdinalIgnoreCase);
				}
				else
				{
					start = html.IndexOf("<body>", StringComparison.OrdinalIgnoreCase) + 6;
					end = html.IndexOf("</body>", StringComparison.OrdinalIgnoreCase);
				}
			}
			else
			{
				start = html.IndexOf("<body>", StringComparison.OrdinalIgnoreCase) + 6;
				end = html.IndexOf("</body>", StringComparison.OrdinalIgnoreCase);
			}

			// body
			string body = html.Substring(start, end - start).Replace("<p><b> </b></p>", "").Replace("<p></p>", "");

			// normalize
			if (!string.IsNullOrWhiteSpace(book.Cover))
			{
				string sourceFilePath = (string.IsNullOrWhiteSpace(mediaSourceFolder) ? "" : mediaSourceFolder.Trim() + "\\") + book.Cover;
				if (File.Exists(sourceFilePath))
				{
					string filename = Utils.GetFilename(book.Cover);
					book.MediaFiles.Add(filename);
					book.Cover = Utils.MediaUri + filename;

					string destinationFilePath = (string.IsNullOrWhiteSpace(mediaDestinationFolder) ? "" : mediaDestinationFolder.Trim() + "\\") + Utils.MediaFolder + "\\" + book.PermanentID + "-" + filename;
					File.Copy(sourceFilePath, destinationFilePath, true);
				}
				else
					book.Cover = "";
			}

			start = body.IndexOf("<" + tag);
			while (start > -1)
			{
				end = body.IndexOf("<" + tag, start + 1);
				if (end < 0)
					end = body.Length;

				string chapter = body.Substring(start, end - start);

				end = chapter.IndexOf("</" + tag + ">") + tag.Length + 3;
				string title = Utility.RemoveTag(chapter.Substring(0, end).Trim(), "a");
				chapter = chapter.Remove(0, end).Trim();

				book.Chapters.Add(title + chapter.Trim().Replace("\r", "").Replace("\n", ""));
				start = body.IndexOf("<" + tag, start + 1);
			}

			for (int index = 0; index < book.Chapters.Count; index++)
			{
				string chapter = book.Chapters[index];
				start = chapter.IndexOf("<img");
				while (start > -1)
				{
					start = chapter.IndexOf("src=") + 5;
					end = chapter.IndexOf(chapter[start - 1].ToString(), start);
					string imageFile = chapter.Substring(start, end - start);
					string sourceFilePath = (string.IsNullOrWhiteSpace(mediaSourceFolder) ? "" : mediaSourceFolder.Trim() + "\\") + imageFile;
					imageFile = Utils.GetFilename(imageFile);
					if (!book.MediaFiles.Contains(imageFile) && File.Exists(sourceFilePath))
					{
						book.MediaFiles.Add(imageFile);
						string destinationFilePath = (string.IsNullOrWhiteSpace(mediaDestinationFolder) ? "" : mediaDestinationFolder.Trim() + "\\") + Utils.MediaFolder + "\\" + book.PermanentID + "-" + imageFile;
						File.Copy(sourceFilePath, destinationFilePath, true);
					}
					chapter = chapter.Remove(start, end - start);
					chapter = chapter.Insert(start, Utils.MediaUri + imageFile);
					start = chapter.IndexOf("<img", start + 1);
				}
				book.Chapters[index] = chapter;
			}

			book.NormalizeTOCs();
			book.NormalizeChapters();

			if (onCompleted != null)
				onCompleted(book);

			return book;
		}

		public static string Normalize(string html, bool decodeHTML, bool toPrecomposedUnicode, bool removeStyles, bool normalizeTags, bool normalizeParagraphs, Action<string> onProcessing, Action<string> onCompleted)
		{
			// check
			if (string.IsNullOrWhiteSpace(html))
				return "";

			// decode HTML
			string normalizedHtml = decodeHTML ? html.Trim().HtmlDecode() : html.Trim();

			// convert to pre-composed unicode
			if (toPrecomposedUnicode)
				normalizedHtml = normalizedHtml.ConvertCompositeUnicodeToUnicode();

			// remove styles
			string[] tags = "link|style".Split('|');
			foreach (string tag in tags)
			{
				if (onProcessing != null)
					onProcessing("Đang xoá thẻ " + tag.ToUpper());
				normalizedHtml = Utility.ClearTag(normalizedHtml, tag);
			}

			// remove comments
			if (onProcessing != null)
				onProcessing("Đang xoá các thẻ COMMENT");
			normalizedHtml = Utility.ClearComments(normalizedHtml);

			// normalize tags
			if (normalizeTags)
			{
				if (onProcessing != null)
					onProcessing("Đang chuẩn hoá (xoá bớt) thẻ DIV và các thẻ của Microsoft Office Word");
				normalizedHtml = Utility.RemoveTag(normalizedHtml, "div");
				normalizedHtml = Utility.RemoveMsOfficeTags(normalizedHtml);
				normalizedHtml = Utility.ClearTag(normalizedHtml, "meta");
				normalizedHtml = Utility.RemoveTagAttributes(normalizedHtml, "body");

				if (onProcessing != null)
					onProcessing("Đang chuẩn hoá thẻ H1, H2, H3, ...");

				normalizedHtml = MsWordHtml.NormalizeHeadings(normalizedHtml);
			}

			// normalize paragraphs
			if (normalizeParagraphs)
			{
				if (onProcessing != null)
					onProcessing("Đang chuẩn hoá các paragraphs");
				normalizedHtml = MsWordHtml.NormalizeParagraphs(normalizedHtml, true);
			}

			// normalize
			normalizedHtml = normalizedHtml.Replace("\r", "").Replace("\n", "");
			while (normalizedHtml.IndexOf("p> ") > 0)
				normalizedHtml = normalizedHtml.Replace("p> ", "p>");
			while (normalizedHtml.IndexOf(" </p") > 0)
				normalizedHtml = normalizedHtml.Replace(" </p", "</p");

			if (onCompleted != null)
				onCompleted(normalizedHtml);

			return normalizedHtml;
		}

		internal static string NormalizeHeadings(string html)
		{
			string normalizedHtml = html.Trim();
			string[] tags = "h1|h2|h3|h4|h5|h6".Split('|');
			foreach (string tag in tags)
			{
				normalizedHtml = Utility.RemoveTagAttributes(normalizedHtml, tag);
				normalizedHtml = normalizedHtml.Replace(StringComparison.OrdinalIgnoreCase, "<p><" + tag + ">", "<" + tag + ">");
				normalizedHtml = normalizedHtml.Replace(StringComparison.OrdinalIgnoreCase, "</" + tag + "></p>", "</" + tag + ">");
				normalizedHtml = normalizedHtml.Replace(StringComparison.OrdinalIgnoreCase, "<" + tag + "></p>", "");

				int start = normalizedHtml.IndexOf("<" + tag + ">", StringComparison.OrdinalIgnoreCase);
				while (start > -1)
				{
					int end = normalizedHtml.IndexOf("</" + tag + ">", start, StringComparison.OrdinalIgnoreCase);
					if (end > 0)
					{
						string paragraph = Utility.RemoveTag(normalizedHtml.Substring(start, end - start + 3 + tag.Length).Replace("\r", "").Replace("\n", " "), "span").Trim();
						normalizedHtml = normalizedHtml.Remove(start, end - start + 3 + tag.Length);
						normalizedHtml = normalizedHtml.Insert(start, paragraph);
					}
					start = normalizedHtml.IndexOf("<" + tag + ">", start + 2 + tag.Length, StringComparison.OrdinalIgnoreCase);
				}
			}
			return normalizedHtml;
		}

		internal static string NormalizeParagraphs(string input, bool removeSPAN)
		{
			if (string.IsNullOrWhiteSpace(input))
				return "";

			string output = Utility.RemoveTagAttributes(input, "p");
			string space = "&nbsp;".HtmlDecode();
			int start = output.IndexOf("<p>", StringComparison.OrdinalIgnoreCase);
			while (start > -1)
			{
				int end = output.IndexOf("</p>", start + 1, StringComparison.OrdinalIgnoreCase);
				if (end > 0)
				{
					string paragraph = output.Substring(start + 3, end - start - 3).Replace("\r", " ").Replace("\n", "").Trim();
					paragraph = Utility.RemoveTag(paragraph, "br");
					if (removeSPAN)
						paragraph = Utility.RemoveTag(paragraph, "span", "display:none");
					while (paragraph.IndexOf(space + space) > -1)
						paragraph = paragraph.Replace(space + space, space);
					output = output.Remove(start, end - start + 4);
					output = output.Insert(start, "<p>" + paragraph + "</p>");
				}
				start = output.IndexOf("<p>", start + 1, StringComparison.OrdinalIgnoreCase);
			}

			output = output.Replace("<p></p>", "").Replace("<p> </p>", "");
			return output;
		}

	}
}