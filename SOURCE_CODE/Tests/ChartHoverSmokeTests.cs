using System;
using System.Collections.Generic;
using System.Drawing;
using HVAC_Pro_Desktop.UI;

namespace HVAC_Pro_Desktop.Tests
{
    public static class ChartHoverSmokeTests
    {
        private sealed class TestSurface : HoverChartControl
        {
            public void BuildRegions()
            {
                BeginHoverRegions();
                AddHoverRectangle(new RectangleF(10, 10, 40, 30), "bar", "April revenue", ChartHoverFormat.Currency(123456.78m), "sum of taxable invoices for April");
                AddHoverPoint(new PointF(80, 30), 10f, "point", "Net cash flow", ChartHoverFormat.Currency(-1500m), "receipts minus payments");
                AddHoverDonutSlice(new RectangleF(100, 10, 80, 80), -90f, 180f, .5f, "slice", "Paid invoices", "8 (80%)", "8 paid ÷ 10 total invoices");
                AddHoverPolygon(new[] { new PointF(10, 70), new PointF(60, 70), new PointF(50, 100), new PointF(20, 100) }, "funnel", "Converted", "4 (40%)", "4 converted ÷ 10 total quotations");
            }
        }

        public static IEnumerable<string> RunAll()
        {
            using (var surface = new TestSurface())
            {
                surface.BuildRegions();
                AssertContains(surface.HoverTextAtForTest(new Point(20, 20)), "₹1,23,456.78", "bar exact INR value");
                AssertContains(surface.HoverTextAtForTest(new Point(80, 30)), "receipts minus payments", "point calculation");
                AssertContains(surface.HoverTextAtForTest(new Point(140, 15)), "Paid invoices", "donut slice");
                AssertContains(surface.HoverTextAtForTest(new Point(30, 80)), "4 converted ÷ 10 total quotations", "funnel calculation");
                if (surface.HoverTextAtForTest(new Point(240, 140)) != null)
                    throw new InvalidOperationException("Chart hover returned a value outside every plotted region.");
            }

            return new[]
            {
                "Chart hover returns exact en-IN values",
                "Chart hover exposes calculation context",
                "Chart hover hit-testing covers bars, points, donut slices, and funnels"
            };
        }

        private static void AssertContains(string actual, string expected, string scenario)
        {
            if (string.IsNullOrWhiteSpace(actual) || actual.IndexOf(expected, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("Chart hover " + scenario + " failed. Expected: " + expected + "; Actual: " + (actual ?? "<null>"));
        }
    }
}
