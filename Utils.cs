#region Related components
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

using net.vieapps.Components.Utility;
#endregion

namespace net.vieapps.books.Components
{
	public static class Utils
	{

		#region Normalize title/name/chars
		public static string GetNormalized(this string @string)
		{
			int counter = -1;
			string result = @string.Trim();

			counter = 0;
			while (counter < 100 && (result.StartsWith("-") || result.StartsWith(".") || result.StartsWith(":")))
			{
				result = result.Right(result.Length - 1);
				counter++;
			}

			counter = 0;
			while (counter < 100 && (result.EndsWith("-") || result.EndsWith(".") || result.EndsWith(":")))
			{
				result = result.Left(result.Length - 1);
				counter++;
			}

			counter = 0;
			while (counter < 100 && (result.EndsWith("-") || result.EndsWith(".")))
			{
				result = result.Left(result.Length - 1);
				counter++;
			}

			counter = 0;
			while (counter < 100 && (result.IndexOf("( ") > -1))
			{
				result = result.Replace("( ", "(");
				counter++;
			}

			counter = 0;
			while (counter < 100 && (result.IndexOf(" )") > -1))
			{
				result = result.Replace(" )", ")");
				counter++;
			}

			counter = 0;
			while (counter < 100 && (result.IndexOf("( ") > -1))
			{
				result = result.Replace("( ", "(");
				counter++;
			}

			counter = 0;
			while (counter < 100 && (result.IndexOf(" )") > -1))
			{
				result = result.Replace(" )", ")");
				counter++;
			}

			counter = 0;
			while (counter < 100 && (result.IndexOf("  ") > -1))
			{
				result = result.Replace("  ", " ");
				counter++;
			}

			return result.Trim().GetCapitalizedWords();
		}

		static List<string> _Chars = null;

		public static List<string> Chars
		{
			get
			{
				if (Utils._Chars == null)
				{
					Utils._Chars = new List<string>() { "0" };
					for (char @char = 'A'; @char <= 'Z'; @char++)
						Utils._Chars.Add(@char.ToString());
				}
				return Utils._Chars;
			}
		}

		public static string GetFirstChar(this string @string)
		{
			string result = Utility.GetNormalizedFilename(@string).ConvertUnicodeToANSI().Trim();
			if (string.IsNullOrWhiteSpace(result))
				return "0";

			string[] specials = new string[] { "-", ".", "'", "+", "&", "“", "”" };
			foreach (string special in specials)
			{
				while (result.StartsWith(special))
					result = result.Right(result.Length - 1).Trim();
				while (result.EndsWith(special))
					result = result.Left(result.Length - 1).Trim();
			}
			if (string.IsNullOrWhiteSpace(result))
				return "0";

			int index = 0;
			bool isCorrect = false;
			while (!isCorrect && index < result.Length)
			{
				char @char = result.ToUpper()[index];
				isCorrect = (@char >= '0' && @char <= '9') || (@char >= 'A' && @char <= 'Z');
				if (!isCorrect)
					index++;
			}

			char firstChar = index < result.Length ? result[index] : '0';
			return (firstChar >= '0' && firstChar <= '9') ? "0" : firstChar.ToString();
		}

		public static string GetXmlString(this string @string, bool replaceQuotes)
		{
			string xml = @string.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
			return replaceQuotes ? xml.Replace("\"", "&quot;") : xml;
		}

		public static string GetXmlString(this string @string)
		{
			return @string.GetXmlString(true);
		}
		#endregion

		#region Normalizing media files
		public static string MediaUri = "book://media/";

		public static string MediaFolder = "media-files";

		public static object[] NormalizeMediaFiles(string input)
		{
			if (string.IsNullOrWhiteSpace(input))
				return null;

			List<string> images = new List<string>();
			string output = input.Trim();
			int start = output.IndexOf("<img", StringComparison.OrdinalIgnoreCase), end = -1;
			while (start > -1)
			{
				start = output.IndexOf("src=", start + 1, StringComparison.OrdinalIgnoreCase) + 5;
				char @char = output[start - 1];
				end = output.IndexOf(@char.ToString(), start + 1, StringComparison.OrdinalIgnoreCase);
				string image = output.Substring(start, end - start);
				if (!image.StartsWith(Utils.MediaUri))
				{
					output = output.Remove(start, end - start);
					output = output.Insert(start, Utils.MediaUri + Utils.GetFilename(image));
				}
				images.Add(image);
				start = output.IndexOf("<img", start + 1, StringComparison.OrdinalIgnoreCase);
			}
			return new object[] { output, images };
		}

		public static string NormalizeMediaFiles(string input, string identifier)
		{
			return input.Replace(Utils.MediaUri, Utils.MediaFolder + "/" + identifier + "-");
		}
		#endregion

		#region Working with files & folders
		public static string GetFilename(string filePath)
		{
			if (string.IsNullOrWhiteSpace(filePath))
				return "";
			else
			{
				string filename = filePath.Trim();
				int start = filename.IndexOf("\\");
				while (start > -1)
				{
					filename = filename.Remove(0, start + 1);
					start = filename.IndexOf("\\");
				}

				start = filename.IndexOf("/");
				while (start > -1)
				{
					filename = filename.Remove(0, start + 1);
					start = filename.IndexOf("/");
				}

				start = filename.IndexOf("?");
				if (start > 0)
					filename = filename.Substring(0, start);

				try
				{
					FileInfo info = new FileInfo(filename);
					return filePath.Trim().ToLower().GetMD5() + info.Extension;
				}
				catch
				{
					int pos = -1;
					start = filename.IndexOf(".");
					while (start > -1)
					{
						pos = start;
						start = filename.IndexOf(".", start + 1);
					}
					filename = filename.Substring(pos);
					start = filename.IndexOf("?");
					if (start > 0)
						filename = filename.Substring(0, start);
					return filePath.Trim().ToLower().GetMD5() + filename;
				}
			}
		}

		public static string NormalizeFilename(string input)
		{
			return Utility.GetNormalizedFilename(input);
		}

		public static async Task DownloadFileAsync(string url, string referUri, string folder, string identifier, CancellationToken cancellationToken, Action<string, string> onCompleted, Action<string, Exception> onError)
		{
			string filePath = (string.IsNullOrWhiteSpace(folder) ? "" : folder.Trim() + "\\") + (string.IsNullOrWhiteSpace(identifier) ? "" : identifier.Trim() + "-") + Utils.GetFilename(url);
			await Utility.DownloadFileAsync(url, filePath, referUri, cancellationToken, onCompleted, onError);
		}
		#endregion

	}

}