using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

internal static class GenerateAssets
{
    private static readonly int[] Sizes = { 16, 20, 24, 32, 40, 48, 64, 256 };

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            return 1;
        }

        List<byte[]> images = new List<byte[]>();
        foreach (int size in Sizes)
        {
            images.Add(RenderPng(size));
        }

        string directory = Path.GetDirectoryName(Path.GetFullPath(args[0]));
        Directory.CreateDirectory(directory);
        using (FileStream stream = File.Create(args[0]))
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)Sizes.Length);

            int offset = 6 + (16 * Sizes.Length);
            for (int index = 0; index < Sizes.Length; index++)
            {
                int size = Sizes[index];
                writer.Write((byte)(size >= 256 ? 0 : size));
                writer.Write((byte)(size >= 256 ? 0 : size));
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((ushort)1);
                writer.Write((ushort)32);
                writer.Write(images[index].Length);
                writer.Write(offset);
                offset += images[index].Length;
            }

            foreach (byte[] image in images)
            {
                writer.Write(image);
            }
        }

        return 0;
    }

    private static byte[] RenderPng(int size)
    {
        using (Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(bitmap))
        using (GraphicsPath background = RoundedRectangle(
            new RectangleF(0, 0, size - 1, size - 1),
            size * 0.26F))
        using (SolidBrush fill = new SolidBrush(Color.FromArgb(37, 99, 235)))
        using (GraphicsPath mark = CreateMark(size))
        using (Pen stroke = new Pen(Color.White, Math.Max(2F, size * 0.113F)))
        using (MemoryStream stream = new MemoryStream())
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.Clear(Color.Transparent);
            graphics.FillPath(fill, background);
            stroke.StartCap = LineCap.Round;
            stroke.EndCap = LineCap.Round;
            stroke.LineJoin = LineJoin.Round;
            graphics.DrawPath(stroke, mark);
            bitmap.Save(stream, ImageFormat.Png);
            return stream.ToArray();
        }
    }

    private static GraphicsPath CreateMark(int size)
    {
        float scale = size / 256F;
        GraphicsPath path = new GraphicsPath();
        path.StartFigure();
        path.AddLine(85F * scale, 205F * scale, 85F * scale, 65F * scale);
        path.AddLine(85F * scale, 65F * scale, 133F * scale, 65F * scale);
        path.AddBezier(
            133F * scale, 65F * scale,
            169F * scale, 65F * scale,
            189F * scale, 84F * scale,
            189F * scale, 113F * scale);
        path.AddBezier(
            189F * scale, 113F * scale,
            189F * scale, 142F * scale,
            169F * scale, 161F * scale,
            133F * scale, 161F * scale);
        path.AddLine(133F * scale, 161F * scale, 85F * scale, 161F * scale);
        path.StartFigure();
        path.AddLine(133F * scale, 161F * scale, 198F * scale, 209F * scale);
        return path;
    }

    private static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
    {
        float diameter = radius * 2F;
        GraphicsPath path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180F, 90F);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270F, 90F);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0F, 90F);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90F, 90F);
        path.CloseFigure();
        return path;
    }
}
