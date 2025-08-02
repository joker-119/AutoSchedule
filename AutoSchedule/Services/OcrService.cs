namespace AutoSchedule.Services;

using Android.Gms.Extensions;
using Android.Graphics;

using Xamarin.Google.MLKit.Vision.Common;
using Xamarin.Google.MLKit.Vision.Text;
using Xamarin.Google.MLKit.Vision.Text.Latin;

public class OcrService
{
    public async Task<string> ExtractTextAsync(Stream imageStream)
    {
        // Decode ↦ Bitmap (async overload is nice for large images)
        Bitmap bitmap = await BitmapFactory.DecodeStreamAsync(imageStream) ??
                        throw new InvalidOperationException("Invalid image");

        InputImage inputImage = InputImage.FromBitmap(bitmap, 0);
        ITextRecognizer recognizer = TextRecognition.GetClient(TextRecognizerOptions.DefaultOptions);

        // ML Kit returns Android.Gms.Tasks.Task ⇒ cast result to Vision.Text.Text
        Text visionText = (Text)await recognizer.Process(inputImage);
        return visionText.GetText();
    }
}