// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Drawing.Tests.Drawing
{
    [GroupOutput("Drawing")]
    public class DrawPolygonTests
    {
        [Theory]
        [WithBasicTestPatternImages(250, 350, PixelTypes.Rgba32, "White", 1f, 2.5, true)]
        [WithBasicTestPatternImages(250, 350, PixelTypes.Rgba32, "White", 0.6f, 10, true)]
        [WithBasicTestPatternImages(250, 350, PixelTypes.Rgba32, "White", 1f, 5, false)]
        [WithBasicTestPatternImages(250, 350, PixelTypes.Bgr24, "Yellow", 1f, 10, true)]
        public void DrawPolygon<TPixel>(TestImageProvider<TPixel> provider, string colorName, float alpha, float thickness, bool antialias)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            PointF[] simplePath =
                {
                    new Vector2(10, 10), new Vector2(200, 150), new Vector2(50, 300)
                };
            Color color = TestUtils.GetColorByName(colorName).WithAlpha(alpha);

            var options = new GraphicsOptions { Antialias = antialias };

            string aa = antialias ? string.Empty : "_NoAntialias";
            FormattableString outputDetails = $"{colorName}_A({alpha})_T({thickness}){aa}";

            provider.RunValidatingProcessorTest(
                c => c.SetGraphicsOptions(options).DrawPolygon(color, thickness, simplePath),
                outputDetails,
                appendSourceFileOrDescription: false);
        }

        [Theory]
        [WithBasicTestPatternImages(250, 350, PixelTypes.Rgba32)]
        public void DrawPolygon_Transformed<TPixel>(TestImageProvider<TPixel> provider)
           where TPixel : unmanaged, IPixel<TPixel>
        {
            PointF[] simplePath =
                {
                    new Vector2(10, 10), new Vector2(200, 150), new Vector2(50, 300)
                };

            provider.RunValidatingProcessorTest(
                c => c.SetShapeOptions(x => x.Transform = Matrix3x2.CreateSkew(GeometryUtilities.DegreeToRadian(-15), 0, new Vector2(200, 200)))
                .DrawPolygon(Color.White, 2.5f, simplePath));
        }
    }
}
