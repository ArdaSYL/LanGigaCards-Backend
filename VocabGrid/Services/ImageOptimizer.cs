using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace VocabGrid.Services;

/// <summary>
/// Shrinks uploaded images before they hit disk: deck covers and flashcard
/// images arrive at whatever resolution/format the user's phone camera or
/// screenshot tool produced, often far larger than anything the app ever
/// displays them at (a few hundred px in a deck grid or card).
///
/// Two independent savings, both applied: capping the longest side at
/// <see cref="MaxDimension"/> (nothing in the UI shows an image larger than
/// that, so extra pixels are pure waste), and re-encoding to WebP (smaller
/// than JPEG or PNG at equivalent visual quality for photographic and
/// mixed content alike -- the reason it's already in <c>MediaController</c>'s
/// own accepted-extensions list).
///
/// Animated GIFs are deliberately left untouched: ImageSharp's WebP encoder
/// only handles a single frame, so re-encoding one at all would silently
/// destroy its animation.
/// </summary>
internal static class ImageOptimizer
{
    private const int MaxDimension = 1600;
    private const int WebpQuality = 82;

    private static readonly HashSet<string> Optimizable = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    internal static bool CanOptimize(string extension) => Optimizable.Contains(extension);

    /// <summary>
    /// Resizes and re-encodes [input] to WebP. Always produces a ".webp"
    /// file regardless of the original extension -- output format is a
    /// storage decision, not something the original upload's extension
    /// should dictate.
    /// </summary>
    internal static async Task<MemoryStream> OptimizeAsync(Stream input, CancellationToken cancellationToken = default)
    {
        using var image = await Image.LoadAsync(input, cancellationToken);

        if (image.Width > MaxDimension || image.Height > MaxDimension)
        {
            image.Mutate(context => context.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(MaxDimension, MaxDimension),
            }));
        }

        var output = new MemoryStream();
        await image.SaveAsWebpAsync(output, new WebpEncoder { Quality = WebpQuality }, cancellationToken);
        output.Position = 0;
        return output;
    }
}
