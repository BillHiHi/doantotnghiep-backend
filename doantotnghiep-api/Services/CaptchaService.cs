using System.Drawing;
using SkiaSharp;

namespace doantotnghiep_api.Services
{
    public class CaptchaService : ICaptchaService
    {
        private static readonly Dictionary<string, (string Code, DateTime Expiry)> _captchaStore = new();
        private readonly object _lock = new();

        public string GenerateCaptcha()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 6).Select(s => s[random.Next(s.Length)]).ToArray());
        }

        public bool ValidateCaptcha(string sessionId, string inputCode)
        {
            lock (_lock)
            {
                if (!_captchaStore.TryGetValue(sessionId, out var entry))
                    return false;

                if (DateTime.UtcNow > entry.Expiry)
                {
                    _captchaStore.Remove(sessionId);
                    return false;
                }

                bool isValid = string.Equals(entry.Code, inputCode, StringComparison.OrdinalIgnoreCase);
                _captchaStore.Remove(sessionId); // Dùng 1 lần
                return isValid;
            }
        }

        public string StoreAndGetSessionId(string code)
        {
            var sessionId = Guid.NewGuid().ToString();
            lock (_lock)
            {
                _captchaStore[sessionId] = (code, DateTime.UtcNow.AddMinutes(5));
            }
            return sessionId;
        }

        public string GenerateCaptchaImage(string code)
        {
            using var surface = SKSurface.Create(new SKImageInfo(160, 50));
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.White);

            var rng = new Random();

            // Vẽ đường nhiễu nền
            for (int i = 0; i < 8; i++)
            {
                using var noisePaint = new SKPaint
                {
                    Color = new SKColor(
                        (byte)rng.Next(180, 230),
                        (byte)rng.Next(180, 230),
                        (byte)rng.Next(180, 230)
                    ),
                    StrokeWidth = 1,
                    IsStroke = true
                };
                canvas.DrawLine(
                    rng.Next(0, 160), rng.Next(0, 50),
                    rng.Next(0, 160), rng.Next(0, 50),
                    noisePaint
                );
            }

            // Vẽ chấm nhiễu
            for (int i = 0; i < 30; i++)
            {
                using var dotPaint = new SKPaint
                {
                    Color = new SKColor(
                        (byte)rng.Next(150, 220),
                        (byte)rng.Next(150, 220),
                        (byte)rng.Next(150, 220)
                    )
                };
                canvas.DrawCircle(rng.Next(0, 160), rng.Next(0, 50), 1, dotPaint);
            }

            // Vẽ từng ký tự với màu và góc xoay ngẫu nhiên
            for (int i = 0; i < code.Length; i++)
            {
                using var textPaint = new SKPaint
                {
                    Color = new SKColor(
                        (byte)rng.Next(0, 100),
                        (byte)rng.Next(0, 100),
                        (byte)rng.Next(100, 200)
                    ),
                    TextSize = rng.Next(24, 32),
                    IsAntialias = true,
                    Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
                };

                float x = 10 + i * 24;
                float y = 35;

                canvas.Save();
                canvas.RotateDegrees(rng.Next(-15, 15), x, y);
                canvas.DrawText(code[i].ToString(), x, y, textPaint);
                canvas.Restore();
            }

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return Convert.ToBase64String(data.ToArray());
        }
    }
}