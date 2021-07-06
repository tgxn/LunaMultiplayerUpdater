﻿using CommonUpdater.Structures;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace CommonUpdater
{
    public class Downloader
    {
        public const string ApiUrl = "https://ci.appveyor.com/api";
        public static string ProjectUrl = $"{ApiUrl}/projects/gavazquez/lunamultiplayer";

        public static string FolderToDecompress = Path.Combine(Path.GetTempPath(), "LMP");

        public static void DownloadAndReplaceFiles(ProductToDownload product)
        {
            var downloadFileName = GetDownloadFileName(product);

            string downloadUrl;
            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
            {
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                downloadUrl = GetDownloadUrl(client, downloadFileName).Result;
            }

            if (!string.IsNullOrEmpty(downloadUrl))
            {
                Console.WriteLine($"Downloading LMP from: {downloadUrl} Please wait...");
                try
                {
                    CleanTempFiles(downloadFileName);
                    using (var client = new WebClient())
                    {
                        client.DownloadFile(downloadUrl, Path.Combine(Path.GetTempPath(), downloadFileName));
                        Console.WriteLine($"Downloading succeeded! Path: {Path.Combine(Path.GetTempPath(), downloadFileName)}");
                    }

                    Console.WriteLine($"Decompressing file to {FolderToDecompress}");
                    ZipFile.ExtractToDirectory(Path.Combine(Path.GetTempPath(), downloadFileName), FolderToDecompress);

                    CopyFilesFromTempToDestination(product);

                    Console.WriteLine("-----------------===========FINISHED===========-----------------");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
                finally
                {
                    CleanTempFiles(downloadFileName);
                }
            }
        }

        private static void CopyFilesFromTempToDestination(ProductToDownload product)
        {

            foreach (var dirPath in Directory.GetDirectories(FolderToDecompress, "*", SearchOption.AllDirectories))
            {
                var destFolder = dirPath.Replace(FolderToDecompress, Directory.GetCurrentDirectory());
                Console.WriteLine($"Creating destination folder: {destFolder}");
                Directory.CreateDirectory(destFolder);
            }

            foreach (var newPath in Directory.GetFiles(FolderToDecompress, "*.*", SearchOption.AllDirectories))
            {
                var destPath = newPath.Replace(FolderToDecompress, Directory.GetCurrentDirectory());
                Console.WriteLine($"Copying {Path.GetFileName(newPath)} to {destPath}");
                File.Copy(newPath, destPath, true);
            }
        }

        private static string GetDownloadFileName(ProductToDownload product)
        {
            switch (product)
            {
                case ProductToDownload.Client:
                    return "LunaMultiplayer-Client-Debug.zip";
                case ProductToDownload.Server:
                    return "LunaMultiplayer-Server-Debug.zip";
                default:
                    throw new ArgumentOutOfRangeException(nameof(product), product, null);
            }
        }

        private static void CleanTempFiles(string downloadFileName)
        {
            try
            {
                if (Directory.Exists(FolderToDecompress))
                    Directory.Delete(FolderToDecompress, true);
            }
            catch (Exception)
            {
                // ignored
            }

            File.Delete(Path.Combine(Path.GetTempPath(), downloadFileName));
        }

        private static async Task<string> GetDownloadUrl(HttpClient client, string downloadFileName)
        {
            using (var response = await client.GetAsync(ProjectUrl))
            {
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var obj = new JavaScriptSerializer().Deserialize<RootObject>(content);
                if (obj.build.status == "success")
                {
                    var job = obj.build.jobs.FirstOrDefault(j => j.name.Contains("Debug"));
                    if (job != null)
                    {
                        Console.WriteLine($"Downloading DEBUG version: {obj.build.version}");
                        return $"{ApiUrl}/buildjobs/{job.jobId}/artifacts/{downloadFileName}";
                    }
                }
                else
                {
                    Console.WriteLine($"Latest build status ({obj.build.status}) is not \"success\". Cannot download at this moment");
                }
            }

            return null;
        }
    }
}
