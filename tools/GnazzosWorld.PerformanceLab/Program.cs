using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using GnazzosWorld.PerformanceLab;

#if DEBUG
throw new InvalidOperationException("Run the measurement lab as Release without a debugger.");
#else
if (Debugger.IsAttached || args.Length != 5 || args[3].Length != 40 || args[3].Any(c => !char.IsAsciiHexDigit(c)) || !ulong.TryParse(args[4], out _))
    throw new ArgumentException("Use: OUTPUT_JSON PUBLISHED_API_DLL IMAGE_MANIFEST REVISION_SHA RUN_ID");
var output = Path.GetFullPath(args[0]); if (File.Exists(output)) throw new IOException("Choose a new evidence path.");
using var manifest = JsonDocument.Parse(File.ReadAllBytes(args[2]));
var images = new List<object>();
foreach (var image in manifest.RootElement.GetProperty("images").EnumerateArray())
{
    var name = image.GetProperty("name").GetString()!;
    if (name is not "perf_large.png" and not "perf_thumb.png") throw new InvalidDataException("Unexpected image name.");
    var bytes = File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[2]))!, name));
    var width = image.GetProperty("width").GetInt32(); var height = image.GetProperty("height").GetInt32();
    if (bytes.Length != image.GetProperty("encodedBytes").GetInt32() || Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() != image.GetProperty("sha256").GetString()) throw new InvalidDataException("Image fixture changed.");
    images.Add(new { Name = name, Width = width, Height = height, EncodedBytes = bytes.Length, RgbaPlaneBytesEstimate = checked(width * height * 4) });
}
var search = SearchMeasurements.Run(); var retention = RetentionMeasurements.Run(); var api = await ApiMeasurements.Run(args[1]);
var report = new { Schema = 1, Revision = args[3].ToLowerInvariant(), RunId = args[4], Configuration = "Release", HostClass = "desktop-lab", Runtime = RuntimeInformation.FrameworkDescription,
    OS = RuntimeInformation.OSDescription, Architecture = RuntimeInformation.ProcessArchitecture.ToString(), Processors = Environment.ProcessorCount,
    Search = search, Retention = retention, Images = images, Api = api,
    NativeStartup = "not_run", NativeScrolling = "not_run", NativeDecodedMemory = "not_run", BatteryAndWireCost = "not_run", Accessibility = "not_run", NativeAot = "not_run" };
await using var result = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None);
await JsonSerializer.SerializeAsync(result, report, new JsonSerializerOptions { WriteIndented = true });
Console.WriteLine("Performance evidence written: measured desktop search/API/retention; native and energy/accessibility rows remain not_run.");
#endif
