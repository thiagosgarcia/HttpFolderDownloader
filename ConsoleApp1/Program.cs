using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HttpFolderDownloader
{
    public class Program
    {
        public static Data Data = new Data();

        public static async Task Main(string[] args)
        {
            if (args == null || args.Length < 2 || Regex.IsMatch(args[0], "--?((help)|[?h])", RegexOptions.IgnoreCase))
                await PrintHelp();

            Data.StartTime = DateTime.Now;

            var model = await ExtractArgs(args);
            Directory.CreateDirectory(model.Path);
            await DoExtraction(model, depth: model.Depth);

            Data.EndTime = DateTime.Now;
            Console.WriteLine(Data.ToString());
            Exit();
        }

        private static async Task DoExtraction(Model model, string currentUrl = null, int depth = -1)
        {
            Console.WriteLine();
            if (string.IsNullOrWhiteSpace(currentUrl))
                currentUrl = model.Url;
            var client = new HttpClient();

            Console.WriteLine($"Url: {currentUrl}");

            var filepath = model.Path.TrimEnd('\\') + "\\" + currentUrl.Replace(model.Url, "")
                               .Replace("/", "\\").Replace("http:\\\\", "")
                               .Replace("https:\\\\", "");
            if (File.Exists(filepath) && !model.Overwrite)
                return;

            HttpResponseMessage response;
            try
            {
                response = await client.GetAsync(currentUrl);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                return;
            }

            var contentType = response.Content.Headers.ContentType.MediaType;
            Console.WriteLine($"MediaType: {contentType}");
            if (IsNavigationMime(model, contentType))
            {
                if ((depth != -1 && depth <= 0))
                    return;
                //navigate
                Console.WriteLine($"Will Navigate...");
                var content = await response.Content.ReadAsStringAsync();
                Data.BytesTransferred += content.Length;
                Data.LinksAccessed++;
                var links = ExtractHref(content);

                foreach (var link in links)
                    await DoExtraction(model, Regex.IsMatch(link, "^(http)", RegexOptions.IgnoreCase) ? link : currentUrl + link, depth: depth > 0 ? depth - 1 : depth == 0 ? 0 : -1);
            }
            else if (IsDownloadMime(model, contentType))
            {
                //download
                //var filepath = model.Path.TrimEnd('\\') + "\\" + currentUrl.Replace(model.Url, "").Replace("/", "\\");
                //if (!File.Exists(filepath) || model.Overwrite)
                //{
                var filename = Path.GetFileName(filepath);
                var path = Path.GetFullPath(filepath).Substring(0, filepath.Length - filename.Length);
                Console.WriteLine($"Will save to {filepath} ...");
                Directory.CreateDirectory(path);
                var file = File.Create(filepath);

                var content = await response.Content.ReadAsStreamAsync();
                using (var stream = new StreamContent(content))
                    await stream.CopyToAsync(file);

                Data.TotalDownloadSize += file.Length;
                Data.BytesTransferred += file.Length;
                Data.FilesDownloaded++;

                file.Close();
                //}
            }
        }

        private static bool IsDownloadMime(Model model, string contentType)
        {
            return model.downloadContent.Count < 1 || model.downloadContent.Contains(contentType);
        }

        private static bool IsNavigationMime(Model model, string contentType)
        {
            return model.navigateContent.Contains(contentType);
        }

        private static IEnumerable<string> ExtractHref(string content)
        {
            foreach (var match in Regex.Matches(content, "(?<=href=\").*?(?=\")", RegexOptions.IgnoreCase | RegexOptions.Multiline))
                if (!match.ToString().EqualsIgnoreCase("../") && !match.ToString().EqualsIgnoreCase("./"))
                    yield return match.ToString();
        }

        private static async Task<Model> ExtractArgs(string[] args)
        {
            var model = new Model();
            if (args[0].IsUrl())
                model.Url = args[0];
            else
            {
                Console.WriteLine(@"Must include URL parameter.");
                await PrintHelp();
                Exit();
            }

            if (args[1].IsPath())
                model.Path = args[1];
            else
            {
                Console.WriteLine(@"Must include PATH parameter.");
                await PrintHelp();
                Exit();
            }

            if (args.Length > 2 && int.TryParse(args[2], out var intVal))
                model.Depth = intVal == 0 ? -1 : intVal;


            var defNavigate = new List<string>() { "text/plain", "text/html" };
            //var defDownload = new List<string>() { "application/pdf", "text/html" };
            model.downloadContent = ExtractParams(args, "--downloadContent");
            model.navigateContent = ExtractParams(args, "--navigateContent", defNavigate);
            model.Overwrite = ExtractParams(args, "--overwrite", "true");

            Console.WriteLine(
                $@"Using: 
    URL:                    {model.Url}
    PATH:                   {model.Path}
    Depth:                  {model.Depth}
    Overwrite:              {model.Overwrite}
    download MIME:          {string.Join(',', model.downloadContent.Select(x => x))}
    navigate MIME:          {string.Join(',', model.navigateContent.Select(x => x))}"
                );

            return model;
        }

        private static IList<string> ExtractParams(string[] args, string param, List<string> def = null)
        {
            var r = new List<string>();
            for (var i = 0; i < args.Length; i++)
            {
                if (args.Length > i + 1 && args[i].EqualsIgnoreCase(param))
                    r.AddRange(args[i + 1].TrimStart('"').TrimEnd('\'').Trim().Split(","));
            }

            if (def != null && !r.Any())
                return def;
            return r;
        }
        private static bool ExtractParams(string[] args, string param, string def)
        {
            var val = string.Empty;
            for (var i = 0; i < args.Length; i++)
            {
                if (args.Length > i + 1 && args[i].EqualsIgnoreCase(param))
                    val = args[i + 1].TrimStart('"').TrimEnd('\'').Trim();
            }

            if (!string.IsNullOrWhiteSpace(def) && string.IsNullOrWhiteSpace(val))
                val = def;

            if (bool.TryParse(val, out bool r))
                return r;

            return true;
        }

        private static async Task PrintHelp()
        {
            await Task.Run(() => Console.WriteLine(
                @"HttpFolderDownloader

This app will look for any ""href"" reference in the feed and will try to navigate over them and download when it's downloadable. 

Usage:
    HttpFolderDownloader <URL> <PATH_TO_SAVE_FILES> [Depth] [--downloadContent <contentType>] [--navigateContent <contentType>] [--overwrite <true|false>]

* For multiple values, separate them by comma

    --downloadType              MIME types for downloading
    --navigateContent           MIME types for navigation

    When defined those variables, the app will try to either download or navigate depending on the configuration by Content-Types.

    Depth not informed or less than or equal 0, for infinite depth of links. If it is informed, 1 is the current level, 2 for the current level and 1
           more amd so on.

    Overwrite variable define if files on download should be updated or not.
"
            ));
            Exit();
        }

        private static void Exit(int code = 0) => Environment.Exit(code);
    }

    public class Model
    {
        public string Url { get; set; }
        public string Path { get; set; }
        public bool Overwrite { get; set; }
        public int Depth { get; set; }
        public IList<string> downloadContent { get; set; }
        public IList<string> navigateContent { get; set; }

    }

    public class Data
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string TimeTaken => EndTime.Subtract(StartTime).ToString("g");
        public long LinksAccessed { get; set; }
        public long FilesDownloaded { get; set; }
        public long BytesTransferred { get; set; }
        public long TotalDownloadSize { get; set; }

        public override string ToString()
        {
            return $@"
            Start Time:                 {StartTime:g}
            End Time:                   {EndTime:g}
            Elapsed Time:               {TimeTaken}

            #Links:                     {LinksAccessed}
            #Downloads:                 {FilesDownloaded}

            Total bytes transferred:    {ToHighestScale(BytesTransferred)}
            Total file size:            {ToHighestScale(TotalDownloadSize)}
";
        }

        private string ToHighestScale(double bytes, string unit = null)
        {
            var units = new[] { "B", "KB", "MB", "GB", "TB", "PB" };

            var unitIndex = units.ToList().IndexOf(unit ?? "B");

            if (unitIndex < 0)
                unitIndex = 0;

            while (bytes > 1024 && unitIndex < units.Length - 1)
            {
                bytes /= 1024;
                unitIndex++;
            }

            return $"{bytes:##.###} {units[unitIndex]}";
        }
    }

    public static class Extensions
    {
        public static bool IsUrl(this string url) => Regex.IsMatch(url,
            "^(?:http(s)?:\\/\\/)?[\\w.-]+(?:\\.[\\w\\.-]+)+[\\w\\-\\._~:/?#[\\]@!\\$&'\\(\\)\\*\\+,;=.]+$",
            RegexOptions.IgnoreCase);
        public static bool IsPath(this string url) => Regex.IsMatch(url,
            "^(?:[\\w]\\:|\\\\)(\\\\[a-z_\\-\\s0-9\\.]+)+$",
            RegexOptions.IgnoreCase);

        public static bool EqualsIgnoreCase(this string value, string aValue) =>
            value.Equals(aValue, StringComparison.InvariantCultureIgnoreCase);
    }
}
