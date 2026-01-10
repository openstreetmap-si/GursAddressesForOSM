using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.BZip2;

namespace OsmGursBuildingImport
{
    class OsmDataDownloader
    {
        private readonly string cacheFolder;
        private readonly GursData gursData;
        private HttpClient httpClient = new HttpClient();


        public OsmDataDownloader(string originalsCacheFolder, GursData gursData)
        {
            this.cacheFolder = originalsCacheFolder;
            this.gursData = gursData;
        }

        ConcurrentDictionary<string, SemaphoreSlim> semaphors = new();

        class SlicePizzaApiResponse
        {
            public bool Complete { get; set; }
        }

        public async Task<string> GetOriginalXmlFileAsync(ProcessingArea area)
        {
            var xmlOutputFile = Path.Combine(cacheFolder, area.Name + ".original.xml");
            var semaphor = semaphors.GetOrAdd(xmlOutputFile, (k) => new SemaphoreSlim(1, 1));
            await semaphor.WaitAsync();
            try
            {
                if (!FileUpToDate(xmlOutputFile))
                {
                    var unclipped = Path.Combine(cacheFolder, "unclipped-" + area.Name + ".original.pbf");
                    var triggerExtractResponse = await httpClient.PostAsync($"https://slice.openstreetmap.us/api/",
                        new StringContent($"{{\"Name\":\"none\",\"RegionType\":\"geojson\",\"RegionData\":{File.ReadAllText(area.pathToGeojson)}}}"));
                    var requestId = await triggerExtractResponse.Content.ReadAsStringAsync();
                    for (int i = 0; i < 20; i++)
                    {
                        await Task.Delay(1000);
                        var response = await httpClient.GetFromJsonAsync<SlicePizzaApiResponse>($"https://slice.openstreetmap.us/api/{requestId}");
                        if (response.Complete)
                        {
                            break;
                        }
                        if (i == 19)
                        {
                            throw new TimeoutException("SlicePizza API didn't complete after 20 seconds.");
                        }
                    }
                    var stream = await httpClient.GetStreamAsync($"https://slice.openstreetmap.us/api/{requestId}.osm.pbf");
                    using (var fileStream = new FileStream(unclipped, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await stream.CopyToAsync(fileStream);
                    }
                    await Process.Start("osmconvert", [unclipped, "--complete-ways", "--complete-multipolygons", "-B=" + area.pathToPoly, "-o=" + xmlOutputFile]).WaitForExitAsync();
                    File.Delete(unclipped);
                }
                return xmlOutputFile;
            }
            finally
            {
                semaphor.Release();
            }
        }

        private static bool FileUpToDate(string file)
        {
            return File.Exists(file) &&
                    File.GetLastWriteTimeUtc(file).AddMinutes(3) > DateTime.UtcNow;
        }
    }
}

