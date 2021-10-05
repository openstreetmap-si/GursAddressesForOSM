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

        public async Task<string> GetOriginalPbfFileAsync(ProcessingArea area)
        {
            var pbfFile = Path.Combine(cacheFolder, area.Name + ".original.pbf");
            var semaphor = semaphors.GetOrAdd(pbfFile, (k) => new SemaphoreSlim(1, 1));
            await semaphor.WaitAsync();
            try
            {
                if (!FileUpToDate(pbfFile))
                {
                    // TEMPORARY UNTIL WE GET KEY FOR https://protomaps.com/extracts
                    var tempFile = Path.GetTempFileName() + ".osm.xml";
                    using (var stream = await httpClient.GetStreamAsync($"https://osmstorage.blob.core.windows.net/gurs-import/{area.Name}.original.osm.bz2"))
                    using (var decompress = new BZip2InputStream(stream))
                    using (var fileStream = new FileStream(tempFile, FileMode.Create))
                        await decompress.CopyToAsync(fileStream);
                    var unclipped = Path.Combine(cacheFolder, "unclipped-" + area.Name + ".original.pbf");
                    await Process.Start("osmconvert", new[] { tempFile, "-o=" + unclipped }).WaitForExitAsync();
                    // END OF TEMPORARY

                    await Process.Start("osmconvert", new[] { unclipped, "--complete-ways", "--complete-multipolygons", "-B=" + area.pathToPoly, "-o=" + pbfFile }).WaitForExitAsync();

                    File.Delete(unclipped);
                    File.Delete(tempFile);
                }
                return pbfFile;
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

