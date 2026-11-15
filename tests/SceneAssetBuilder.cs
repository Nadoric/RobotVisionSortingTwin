using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static class SceneAssetBuilder
{
    /// <summary>由项目矢量源生成 PNG 及包含七种尺寸的 Windows ICO，避免依赖外部绘图工具。</summary>
    public static void Build(string directory)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var input = File.OpenRead(Path.Combine(directory, "TwinIcon.xaml"));
                var image = (DrawingImage)XamlReader.Load(input);
                int[] sizes = [16, 24, 32, 48, 64, 128, 256];
                var images = new List<byte[]>();
                foreach (int size in sizes)
                {
                    var visual = new DrawingVisual();
                    using (var context = visual.RenderOpen())
                    {
                        context.DrawImage(image, new Rect(0, 0, size, size));
                    }

                    var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(visual);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var memory = new MemoryStream();
                    encoder.Save(memory);
                    images.Add(memory.ToArray());
                }

                File.WriteAllBytes(Path.Combine(directory, "TwinIcon.png"), images[^1]);
                using var output = new BinaryWriter(File.Create(Path.Combine(directory, "TwinIcon.ico")));
                output.Write((ushort)0);
                output.Write((ushort)1);
                output.Write((ushort)sizes.Length);
                int offset = 6 + sizes.Length * 16;
                for (int index = 0; index < sizes.Length; index++)
                {
                    output.Write((byte)(sizes[index] == 256 ? 0 : sizes[index]));
                    output.Write((byte)(sizes[index] == 256 ? 0 : sizes[index]));
                    output.Write((byte)0);
                    output.Write((byte)0);
                    output.Write((ushort)1);
                    output.Write((ushort)32);
                    output.Write(images[index].Length);
                    output.Write(offset);
                    offset += images[index].Length;
                }

                foreach (byte[] bytes in images)
                {
                    output.Write(bytes);
                }
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            throw failure;
        }

        Console.WriteLine("Generated application icon: " + Path.GetFullPath(directory));
    }
}
