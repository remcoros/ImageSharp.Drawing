// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;
using System.Numerics;
using System.Text;
using SixLabors.Fonts;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Drawing.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Drawing.Tests.Drawing.Text
{
    [GroupOutput("Drawing/Text")]
    [ValidateDisposedMemoryAllocations]
    public class DrawTextOnImageTests
    {
        private const string AB = "AB\nAB";

        private const string TestText = "Sphinx of black quartz, judge my vow\n0123456789";

        private static readonly ImageComparer TextDrawingComparer = TestEnvironment.IsFramework || TestEnvironment.NetCoreVersion.StartsWith("2")
            ? ImageComparer.TolerantPercentage(1e-2f) // Relax comparison on .NET Framework and .NET Core 2.x
            : ImageComparer.TolerantPercentage(1e-5f);

        private static readonly ImageComparer OutlinedTextDrawingComparer = ImageComparer.TolerantPercentage(6e-4f);

        public DrawTextOnImageTests(ITestOutputHelper output)
            => this.Output = output;

        private ITestOutputHelper Output { get; }

        [Theory]
        [WithSolidFilledImages(1276, 336, "White", PixelTypes.Rgba32, ColorFontSupport.MicrosoftColrFormat)]
        [WithSolidFilledImages(1276, 336, "White", PixelTypes.Rgba32, ColorFontSupport.None)]
        public void EmojiFontRendering<TPixel>(TestImageProvider<TPixel> provider, ColorFontSupport colorFontSupport)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Font font = CreateFont(TestFonts.OpenSans, 70);
            FontFamily emojiFontFamily = CreateFont(TestFonts.TwemojiMozilla, 36).Family;

            Color color = Color.Black;
            string text = "A short piece of text 😀 with an emoji";

            provider.VerifyOperation(
              TextDrawingComparer,
              img =>
              {
                  TextOptions textOptions = new(font)
                  {
                      HorizontalAlignment = HorizontalAlignment.Center,
                      VerticalAlignment = VerticalAlignment.Center,
                      TextAlignment = TextAlignment.Center,
                      FallbackFontFamilies = new[] { emojiFontFamily },
                      ColorFontSupport = colorFontSupport,
                      Origin = new PointF(img.Width / 2, img.Height / 2)
                  };

                  img.Mutate(i => i.DrawText(textOptions, text, color));
              },
              $"ColorFontsEnabled-{colorFontSupport == ColorFontSupport.MicrosoftColrFormat}");
        }

        [Theory]
        [WithSolidFilledImages(400, 200, "White", PixelTypes.Rgba32)]
        public void FallbackFontRendering<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // https://github.com/SixLabors/Fonts/issues/171
            var collection = new FontCollection();
            Font whitney = CreateFont(TestFonts.WhitneyBook, 25);
            FontFamily malgun = CreateFont(TestFonts.Malgun, 25).Family;

            Color color = Color.Black;
            const string text = "亞DARKSOUL亞";

            provider.VerifyOperation(
              TextDrawingComparer,
              img =>
              {
                  TextOptions textOptions = new(whitney)
                  {
                      HorizontalAlignment = HorizontalAlignment.Center,
                      VerticalAlignment = VerticalAlignment.Center,
                      TextAlignment = TextAlignment.Center,
                      FallbackFontFamilies = new[] { malgun },
                      KerningMode = KerningMode.Normal,
                      Origin = new PointF(img.Width / 2, img.Height / 2)
                  };

                  img.Mutate(i => i.DrawText(textOptions, text, color));
              });
        }

        [Theory]
        [WithSolidFilledImages(276, 336, "White", PixelTypes.Rgba32)]
        public void DoesntThrowExceptionWhenOverlappingRightEdge_Issue688<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Font font = CreateFont(TestFonts.OpenSans, 36);
            Color color = Color.Black;
            const string text = "A short piece of text";

            using Image<TPixel> img = provider.GetImage();

            // Measure the text size
            FontRectangle size = TextMeasurer.Measure(text, new TextOptions(font));

            // Find out how much we need to scale the text to fill the space (up or down)
            float scalingFactor = Math.Min(img.Width / size.Width, img.Height / size.Height);

            // Create a new font
            var scaledFont = new Font(font, scalingFactor * font.Size);
            TextOptions textOptions = new(scaledFont)
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Origin = new PointF(img.Width / 2, img.Height / 2)
            };

            img.Mutate(i => i.DrawText(textOptions, text, color));
        }

        [Theory]
        [WithSolidFilledImages(1500, 500, "White", PixelTypes.Rgba32)]
        public void DoesntThrowExceptionWhenOverlappingRightEdge_Issue688_2<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Font font = CreateFont(TestFonts.OpenSans, 39);
            string text = new('a', 10000);
            Rgba32 color = Color.Black;
            var point = new PointF(100, 100);

            using Image<TPixel> img = provider.GetImage();
            img.Mutate(ctx => ctx.DrawText(text, font, color, point));
        }

        [Theory]
        [WithSolidFilledImages(200, 200, "White", PixelTypes.Rgba32)]
        public void OpenSansJWithNoneZeroShouldntExtendPastGlyphe<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Font font = CreateFont(TestFonts.OpenSans, 50);
            Color color = Color.Black;

            using Image<TPixel> img = provider.GetImage();
            img.Mutate(ctx => ctx.DrawText(TestText, font, Color.Black, new PointF(-50, 2)));

            Assert.Equal(Color.White.ToPixel<TPixel>(), img[173, 2]);
        }

        [Theory]
        [WithSolidFilledImages(20, 50, "White", PixelTypes.Rgba32, 50, 0, 0, TestFonts.OpenSans, "i")]
        [WithSolidFilledImages(200, 150, "White", PixelTypes.Rgba32, 50, 0, 0, TestFonts.SixLaborsSampleAB, AB)]
        [WithSolidFilledImages(900, 150, "White", PixelTypes.Rgba32, 50, 0, 0, TestFonts.OpenSans, TestText)]
        [WithSolidFilledImages(400, 45, "White", PixelTypes.Rgba32, 20, 0, 0, TestFonts.OpenSans, TestText)]
        [WithSolidFilledImages(1100, 200, "White", PixelTypes.Rgba32, 50, 150, 50, TestFonts.OpenSans, TestText)]
        public void FontShapesAreRenderedCorrectly<TPixel>(
            TestImageProvider<TPixel> provider,
            int fontSize,
            int x,
            int y,
            string fontName,
            string text)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Font font = CreateFont(fontName, fontSize);

            provider.RunValidatingProcessorTest(
                c => c.DrawText(text, font, Color.Black, new PointF(x, y)),
                $"{fontName}-{fontSize}-{ToTestOutputDisplayText(text)}-({x},{y})",
                TextDrawingComparer,
                appendPixelTypeToFileName: false,
                appendSourceFileOrDescription: true);
        }

        [Theory]
        [WithSolidFilledImages(50, 50, "White", PixelTypes.Rgba32, 50, 25, 25, TestFonts.OpenSans, "i", 45, 25, 25)]
        [WithSolidFilledImages(200, 200, "White", PixelTypes.Rgba32, 50, 100, 100, TestFonts.SixLaborsSampleAB, AB, 45, 100, 100)]
        [WithSolidFilledImages(1100, 1100, "White", PixelTypes.Rgba32, 50, 550, 550, TestFonts.OpenSans, TestText, 45, 550, 550)]
        [WithSolidFilledImages(400, 400, "White", PixelTypes.Rgba32, 20, 200, 200, TestFonts.OpenSans, TestText, 45, 200, 200)]
        public void FontShapesAreRenderedCorrectly_WithRotationApplied<TPixel>(
            TestImageProvider<TPixel> provider,
            int fontSize,
            int x,
            int y,
            string fontName,
            string text,
            float angle,
            float rotationOriginX,
            float rotationOriginY)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Font font = CreateFont(fontName, fontSize);
            float radians = GeometryUtilities.DegreeToRadian(angle);

            TextOptions textOptions = new(font)
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Origin = new PointF(x, y)
            };

            provider.RunValidatingProcessorTest(
                x => x
                .SetDrawingTransform(Matrix3x2.CreateRotation(radians, new Vector2(rotationOriginX, rotationOriginY)))
                .DrawText(textOptions, text, Color.Black),
                $"F({fontName})-S({fontSize})-A({angle})-{ToTestOutputDisplayText(text)}-({x},{y})",
                TextDrawingComparer,
                appendPixelTypeToFileName: false,
                appendSourceFileOrDescription: true);
        }

        [Theory]
        [WithSolidFilledImages(50, 50, "White", PixelTypes.Rgba32, 50, 25, 25, TestFonts.OpenSans, "i", -12, 0, 25, 25)]
        [WithSolidFilledImages(200, 200, "White", PixelTypes.Rgba32, 50, 100, 100, TestFonts.SixLaborsSampleAB, AB, 10, 0, 100, 100)]
        [WithSolidFilledImages(1100, 1100, "White", PixelTypes.Rgba32, 50, 550, 550, TestFonts.OpenSans, TestText, 0, 10, 550, 550)]
        [WithSolidFilledImages(400, 400, "White", PixelTypes.Rgba32, 20, 200, 200, TestFonts.OpenSans, TestText, 0, -10, 200, 200)]
        public void FontShapesAreRenderedCorrectly_WithSkewApplied<TPixel>(
            TestImageProvider<TPixel> provider,
            int fontSize,
            int x,
            int y,
            string fontName,
            string text,
            float angleX,
            float angleY,
            float rotationOriginX,
            float rotationOriginY)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Font font = CreateFont(fontName, fontSize);
            float radianX = GeometryUtilities.DegreeToRadian(angleX);
            float radianY = GeometryUtilities.DegreeToRadian(angleY);

            TextOptions textOptions = new(font)
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Origin = new PointF(x, y)
            };

            provider.RunValidatingProcessorTest(
                x => x
                .SetDrawingTransform(Matrix3x2.CreateSkew(radianX, radianY, new Vector2(rotationOriginX, rotationOriginY)))
                .DrawText(textOptions, text, Color.Black),
                $"F({fontName})-S({fontSize})-A({angleX},{angleY})-{ToTestOutputDisplayText(text)}-({x},{y})",
                TextDrawingComparer,
                appendPixelTypeToFileName: false,
                appendSourceFileOrDescription: true);
        }

        /// <summary>
        /// Based on:
        /// https://github.com/SixLabors/ImageSharp/issues/572
        /// </summary>
        [Theory]
        [WithSolidFilledImages(2480, 3508, "White", PixelTypes.Rgba32)]
        public void FontShapesAreRenderedCorrectly_LargeText<TPixel>(
            TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Font font = CreateFont(TestFonts.OpenSans, 36);

            var sb = new StringBuilder();
            string str = Repeat(" ", 78) + "THISISTESTWORDSTHISISTESTWORDSTHISISTESTWORDSTHISISTESTWORDSTHISISTESTWORDS";
            sb.Append(str);

            string newLines = Repeat(Environment.NewLine, 61);
            sb.Append(newLines);

            for (int i = 0; i < 10; i++)
            {
                sb.AppendLine(str);
            }

            // Strict comparer, because the image is sparse:
            var comparer = ImageComparer.TolerantPercentage(1e-6f);

            provider.VerifyOperation(
                comparer,
                img => img.Mutate(c => c.DrawText(sb.ToString(), font, Color.Black, new PointF(10, 1))),
                false,
                false);
        }

        [Theory]
        [WithSolidFilledImages(400, 550, "White", PixelTypes.Rgba32, 1, 5, true)]
        [WithSolidFilledImages(400, 550, "White", PixelTypes.Rgba32, 1.5, 3, true)]
        [WithSolidFilledImages(400, 550, "White", PixelTypes.Rgba32, 2, 2, true)]
        [WithSolidFilledImages(400, 100, "White", PixelTypes.Rgba32, 1, 5, false)]
        [WithSolidFilledImages(400, 100, "White", PixelTypes.Rgba32, 1.5, 3, false)]
        [WithSolidFilledImages(400, 100, "White", PixelTypes.Rgba32, 2, 2, false)]
        public void FontShapesAreRenderedCorrectly_WithLineSpacing<TPixel>(
            TestImageProvider<TPixel> provider,
            float lineSpacing,
            int lineCount,
            bool wrap)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Font font = CreateFont(TestFonts.OpenSans, 16);

            var sb = new StringBuilder();
            string str = "Lorem ipsum dolor sit amet, consectetuer adipiscing elit. Maecenas porttitor congue massa. Fusce posuere, magna sed pulvinar ultricies, purus lectus malesuada libero, sit amet commodo magna eros quis urna.";

            for (int i = 0; i < lineCount; i++)
            {
                sb.AppendLine(str);
            }

            TextOptions textOptions = new(font)
            {
                KerningMode = KerningMode.Normal,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left,
                LineSpacing = lineSpacing,
                Origin = new PointF(10, 1)
            };

            if (wrap)
            {
                textOptions.WrappingLength = 300;
            }

            Color color = Color.Black;

            // NET472 is 0.0045 different.
            var comparer = ImageComparer.TolerantPercentage(0.0046F);

            provider.VerifyOperation(
                comparer,
                img => img.Mutate(c => c.DrawText(textOptions, sb.ToString(), color)),
                $"linespacing_{lineSpacing}_linecount_{lineCount}_wrap_{wrap}",
                false,
                false);
        }

        [Theory]
        [WithSolidFilledImages(200, 150, "White", PixelTypes.Rgba32, 50, 0, 0, TestFonts.SixLaborsSampleAB, AB)]
        [WithSolidFilledImages(900, 150, "White", PixelTypes.Rgba32, 50, 0, 0, TestFonts.OpenSans, TestText)]
        [WithSolidFilledImages(1100, 200, "White", PixelTypes.Rgba32, 50, 150, 50, TestFonts.OpenSans, TestText)]
        public void FontShapesAreRenderedCorrectlyWithAPen<TPixel>(
            TestImageProvider<TPixel> provider,
            int fontSize,
            int x,
            int y,
            string fontName,
            string text)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Font font = CreateFont(fontName, fontSize);
            Color color = Color.Black;

            provider.VerifyOperation(
                OutlinedTextDrawingComparer,
                img => img.Mutate(c => c.DrawText(text, new Font(font, fontSize), Pens.Solid(color, 1), new PointF(x, y))),
                $"pen_{fontName}-{fontSize}-{ToTestOutputDisplayText(text)}-({x},{y})",
                appendPixelTypeToFileName: false,
                appendSourceFileOrDescription: true);
        }

        [Theory]
        [WithSolidFilledImages(200, 150, "White", PixelTypes.Rgba32, 50, 0, 0, TestFonts.SixLaborsSampleAB, AB)]
        [WithSolidFilledImages(900, 150, "White", PixelTypes.Rgba32, 50, 0, 0, TestFonts.OpenSans, TestText)]
        [WithSolidFilledImages(1100, 200, "White", PixelTypes.Rgba32, 50, 150, 50, TestFonts.OpenSans, TestText)]
        public void FontShapesAreRenderedCorrectlyWithAPenPatterned<TPixel>(
            TestImageProvider<TPixel> provider,
            int fontSize,
            int x,
            int y,
            string fontName,
            string text)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Font font = CreateFont(fontName, fontSize);
            Color color = Color.Black;

            provider.VerifyOperation(
                OutlinedTextDrawingComparer,
                img => img.Mutate(c => c.DrawText(text, new Font(font, fontSize), Pens.DashDot(color, 3), new PointF(x, y))),
                $"pen_{fontName}-{fontSize}-{ToTestOutputDisplayText(text)}-({x},{y})",
                appendPixelTypeToFileName: false,
                appendSourceFileOrDescription: true);
        }

        [Theory]
        [WithSolidFilledImages(1000, 1500, "White", PixelTypes.Rgba32, TestFonts.OpenSans)]
        public void TextPositioningIsRobust<TPixel>(TestImageProvider<TPixel> provider, string fontName)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Font font = CreateFont(fontName, 30);

            string text = Repeat(
                "Beware the Jabberwock, my son!  The jaws that bite, the claws that catch!  Beware the Jubjub bird, and shun The frumious Bandersnatch!\n",
                20);

            TextOptions textOptions = new(font)
            {
                WrappingLength = 1000,
                Origin = new PointF(10, 50)
            };

            string details = fontName.Replace(" ", string.Empty);

            // Based on the reported 0.1755% difference with AccuracyMultiple = 8
            // We should avoid quality regressions leading to higher difference!
            var comparer = ImageComparer.TolerantPercentage(0.2f);

            provider.RunValidatingProcessorTest(
                x => x.DrawText(textOptions, text, Color.Black),
                details,
                comparer,
                appendPixelTypeToFileName: false,
                appendSourceFileOrDescription: false);
        }

        [Fact]
        public void CanDrawTextWithEmptyPath()
        {
            // The following font/text combination generates an empty path.
            Font font = CreateFont(TestFonts.WendyOne, 72);
            const string text = "Hello\0World";
            TextOptions textOptions = new(font);
            FontRectangle textSize = TextMeasurer.Measure(text, textOptions);

            Assert.NotEqual(FontRectangle.Empty, textSize);

            using var image = new Image<Rgba32>(Configuration.Default, (int)textSize.Width + 20, (int)textSize.Height + 20);
            image.Mutate(x => x.DrawText(
                text,
                font,
                Color.Black,
                Vector2.Zero));
        }

        [Theory]
        [WithSolidFilledImages(300, 200, nameof(Color.White), PixelTypes.Rgba32, TestFonts.OpenSans, 32, 75F)]
        [WithSolidFilledImages(300, 200, nameof(Color.White), PixelTypes.Rgba32, TestFonts.OpenSans, 40, 90F)]
        public void CanRotateFilledFont_Issue175<TPixel>(
            TestImageProvider<TPixel> provider,
            string fontName,
            int fontSize,
            float angle)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Font font = CreateFont(fontName, fontSize);
            const string text = "QuickTYZ";
            AffineTransformBuilder builder = new AffineTransformBuilder().AppendRotationDegrees(angle);

            TextOptions textOptions = new(font);
            FontRectangle bounds = TextMeasurer.Measure(text, textOptions);
            Matrix3x2 transform = builder.BuildMatrix(Rectangle.Round(new RectangleF(bounds.X, bounds.Y, bounds.Width, bounds.Height)));

            provider.RunValidatingProcessorTest(
                x => x.SetDrawingTransform(transform).DrawText(textOptions, text, Color.Black),
                $"F({fontName})-S({fontSize})-A({angle})-{ToTestOutputDisplayText(text)})",
                TextDrawingComparer,
                appendPixelTypeToFileName: false,
                appendSourceFileOrDescription: true);
        }

        [Theory]
        [WithSolidFilledImages(300, 200, nameof(Color.White), PixelTypes.Rgba32, TestFonts.OpenSans, 32, 75F, 1)]
        [WithSolidFilledImages(300, 200, nameof(Color.White), PixelTypes.Rgba32, TestFonts.OpenSans, 40, 90F, 2)]
        public void CanRotateOutlineFont_Issue175<TPixel>(
            TestImageProvider<TPixel> provider,
            string fontName,
            int fontSize,
            float angle,
            int strokeWidth)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Font font = CreateFont(fontName, fontSize);
            const string text = "QuickTYZ";
            AffineTransformBuilder builder = new AffineTransformBuilder().AppendRotationDegrees(angle);

            TextOptions textOptions = new(font);
            FontRectangle bounds = TextMeasurer.Measure(text, textOptions);
            Matrix3x2 transform = builder.BuildMatrix(Rectangle.Round(new RectangleF(bounds.X, bounds.Y, bounds.Width, bounds.Height)));

            provider.RunValidatingProcessorTest(
                x => x.SetDrawingTransform(transform)
                .DrawText(textOptions, text, Pens.Solid(Color.Black, strokeWidth)),
                $"F({fontName})-S({fontSize})-A({angle})-STR({strokeWidth})-{ToTestOutputDisplayText(text)})",
                TextDrawingComparer,
                appendPixelTypeToFileName: false,
                appendSourceFileOrDescription: true);
        }

        [Theory]
        [WithBlankImage(100, 100, PixelTypes.Rgba32, "M10,90 Q90,90 90,45 Q90,10 50,10 Q10,10 10,40 Q10,70 45,70 Q70,70 75,50", "spiral")]
        [WithBlankImage(350, 350, PixelTypes.Rgba32, "M275 175 A100 100 0 1 1 275 174", "circle")]
        [WithBlankImage(120, 120, PixelTypes.Rgba32, "M50,10 L 90 90 L 10 90 L50 10", "triangle")]
        public void CanDrawTextAlongPathHorizontal<TPixel>(TestImageProvider<TPixel> provider, string svgPath, string exampleImageKey)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            bool parsed = Path.TryParseSvgPath(svgPath, out IPath path);
            Assert.True(parsed);

            Font font = CreateFont(TestFonts.OpenSans, 13);
            TextOptions textOptions = new(font)
            {
                WrappingLength = path.ComputeLength(),
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Left,
            };

            const string text = "Quick brown fox jumps over the lazy dog.";
            IPathCollection glyphs = TextBuilder.GenerateGlyphs(text, path, textOptions);

            provider.RunValidatingProcessorTest(
                c => c.Fill(Color.White).Draw(Color.Red, 1, path).Fill(Color.Black, glyphs),
                new { type = exampleImageKey },
                comparer: ImageComparer.TolerantPercentage(0.002f));
        }

        [Theory]
        [WithBlankImage(350, 350, PixelTypes.Rgba32, "M225 175 A50 50 0 1 1 225 174", "circle")]
        [WithBlankImage(250, 250, PixelTypes.Rgba32, "M100,60 L 140 140 L 60 140 L100 60", "triangle")]
        public void CanDrawTextAlongPathVertical<TPixel>(TestImageProvider<TPixel> provider, string svgPath, string exampleImageKey)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            bool parsed = Path.TryParseSvgPath(svgPath, out IPath path);
            Assert.True(parsed);

            Font font = CreateFont(TestFonts.OpenSans, 13);
            TextOptions textOptions = new(font)
            {
                WrappingLength = path.ComputeLength() / 4,
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Left,
                LayoutMode = LayoutMode.VerticalLeftRight
            };

            const string text = "Quick brown fox jumps over the lazy dog.";
            IPathCollection glyphs = TextBuilder.GenerateGlyphs(text, path, textOptions);

            provider.RunValidatingProcessorTest(
                c => c.Fill(Color.White).Draw(Color.Red, 1, path).Fill(Color.Black, glyphs),
                new { type = exampleImageKey },
                comparer: ImageComparer.TolerantPercentage(0.002f));
        }

        private static string Repeat(string str, int times) => string.Concat(Enumerable.Repeat(str, times));

        private static string ToTestOutputDisplayText(string text)
        {
            string fnDisplayText = text.Replace("\n", string.Empty);
            return fnDisplayText.Substring(0, Math.Min(fnDisplayText.Length, 4));
        }

        private static Font CreateFont(string fontName, int size)
            => TestFontUtilities.GetFont(fontName, size);
    }
}
