using OpenCvSharp;
using Windows.Data.Pdf;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public sealed class DrawingLoadService
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg" };

    public async Task<Mat> LoadAsync(DrawingInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!File.Exists(input.Path)) throw new FileNotFoundException("Drawing file was not found.", input.Path);
        cancellationToken.ThrowIfCancellationRequested();
        if (ImageExtensions.Contains(Path.GetExtension(input.Path)))
        {
            var mat = await Task.Run(() => Cv2.ImRead(input.Path, ImreadModes.Color), cancellationToken);
            if (mat.Empty()) { mat.Dispose(); throw new InvalidDataException("The image could not be decoded."); }
            return mat;
        }
        if (!string.Equals(Path.GetExtension(input.Path), ".pdf", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("Supported formats are PNG, JPG, JPEG, and PDF.");
        return await LoadPdfPageAsync(input.Path, input.PdfPage, cancellationToken);
    }

    private static async Task<Mat> LoadPdfPageAsync(string path, int pageNumber, CancellationToken cancellationToken)
    {
        if (pageNumber < 1) throw new ArgumentOutOfRangeException(nameof(pageNumber));
        var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(path);
        using var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read);
        var document = await PdfDocument.LoadFromStreamAsync(stream);
        if (pageNumber > document.PageCount) throw new ArgumentOutOfRangeException(nameof(pageNumber));
        using var rendered = new InMemoryRandomAccessStream();
        using (var page = document.GetPage((uint)(pageNumber - 1)))
            await page.RenderToStreamAsync(rendered);
        cancellationToken.ThrowIfCancellationRequested();
        var decoder = await BitmapDecoder.CreateAsync(rendered);
        var pixels = await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, new BitmapTransform(), ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);
        var mat = Mat.FromPixelData((int)decoder.OrientedPixelHeight, (int)decoder.OrientedPixelWidth, MatType.CV_8UC4, pixels.DetachPixelData());
        var bgr = new Mat(); Cv2.CvtColor(mat, bgr, ColorConversionCodes.BGRA2BGR); mat.Dispose(); return bgr;
    }

    public static byte[] EncodePng(Mat image)
    {
        Cv2.ImEncode(".png", image, out var bytes);
        return bytes;
    }
}
