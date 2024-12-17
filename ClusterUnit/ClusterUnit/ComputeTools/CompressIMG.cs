using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ClusterUnit.ComputeTools
{
    public class CompressIMG
    {
        static readonly int[,] QuantizationMatrix = {
{ 32, 24, 20, 32, 48, 80, 102, 122 },
{ 24, 24, 28, 38, 52, 116, 120, 110 },
{ 28, 26, 32, 48, 80, 114, 138, 112 },
{ 28, 34, 44, 58, 102, 174, 160, 124 },
{ 36, 44, 74, 112, 136, 218, 206, 154 },
{ 48, 70, 110, 128, 162, 208, 226, 184 },
{ 98, 128, 156, 174, 206, 242, 240, 202 },
{ 144, 184, 190, 196, 224, 200, 206, 198 }
};

        private int _countOfThreads = 0;
        private int _numberOfThreads = 8;
        private int blockSize = 8;
        private List<double[,]> _dataBlocks = new List<double[,]>();
        public async Task<byte[]> CompressImageAsync(byte[] buffer)
        {
            byte[] bytes = new byte[buffer.Length];
            using (MemoryStream memoryStream = new MemoryStream(buffer))
            {
                using (Bitmap original = new Bitmap(memoryStream))
                {
                    int originalWidth = original.Width;
                    int originalHeight = original.Height;
                    // Добавляем отступы, если нужно
                    Bitmap paddedImage = AddPadding(original);

                    int width = paddedImage.Width;
                    int height = paddedImage.Height;

                    // Получаем данные изображения в массив байт для обработки
                    BitmapData bmpData = paddedImage.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, paddedImage.PixelFormat);
                    int bytesPerPixel = Image.GetPixelFormatSize(paddedImage.PixelFormat) / 8;
                    int stride = bmpData.Stride;
                    IntPtr ptr = bmpData.Scan0;

                    byte[] pixelData = new byte[stride * height];
                    Marshal.Copy(ptr, pixelData, 0, pixelData.Length);

                    // Параллельная обработка каждого блока 8x8
                    Parallel.For(0, width / blockSize, i =>
                    {
                        for (int j = 0; j < height / blockSize; j++)
                        {
                            ProcessBlock(pixelData, i * blockSize, j * blockSize, stride, bytesPerPixel);
                        }
                    });

                    // Копируем обработанные данные обратно
                    Marshal.Copy(pixelData, 0, ptr, pixelData.Length);
                    paddedImage.UnlockBits(bmpData);

                    Rectangle cropArea = new Rectangle(0, 0, originalWidth, originalHeight);
                    Bitmap cropped = new Bitmap(cropArea.Width, cropArea.Height);

                    using (Graphics g = Graphics.FromImage(cropped))
                    {
                        g.DrawImage(paddedImage, 0, 0, cropArea, GraphicsUnit.Pixel);
                    }

                    // Сохраняем изображение в поток памяти и возвращаем его как байтовый массив
                    using (MemoryStream memory = new MemoryStream())
                    {
                        cropped.Save(memory, ImageFormat.Jpeg);

                        paddedImage.Dispose();
                        cropped.Dispose();
                        _dataBlocks.Clear();
                        return memory.ToArray();
                    }
                }
            }
        }
        private void ProcessBlock(byte[] pixelData, int startX, int startY, int stride, int bytesPerPixel)
        {
            double[,] pixelsR = new double[blockSize, blockSize];
            double[,] pixelsG = new double[blockSize, blockSize];
            double[,] pixelsB = new double[blockSize, blockSize];

            // Извлекаем блок пикселей для каждого канала
            for (int x = 0; x < blockSize; x++)
            {
                for (int y = 0; y < blockSize; y++)
                {
                    int pixelIndex = ((startY + y) * stride) + ((startX + x) * bytesPerPixel);
                    pixelsR[x, y] = pixelData[pixelIndex + 2]; // Red
                    pixelsG[x, y] = pixelData[pixelIndex + 1]; // Green
                    pixelsB[x, y] = pixelData[pixelIndex]; // Blue
                }
            }

            // Обрабатываем каждый канал отдельно
            // Красный канал
            double[,] dctBlockR = DCT(pixelsR);
            int[,] quantizedBlockR = QuantizeBlock(dctBlockR);
            double[,] dequantizedBlockR = DequantizeBlock(quantizedBlockR);
            double[,] idctBlockR = IDCT(dequantizedBlockR);

            // Зеленый канал
            double[,] dctBlockG = DCT(pixelsG);
            int[,] quantizedBlockG = QuantizeBlock(dctBlockG);
            double[,] dequantizedBlockG = DequantizeBlock(quantizedBlockG);
            double[,] idctBlockG = IDCT(dequantizedBlockG);

            // Синий канал
            double[,] dctBlockB = DCT(pixelsB);
            int[,] quantizedBlockB = QuantizeBlock(dctBlockB);
            double[,] dequantizedBlockB = DequantizeBlock(quantizedBlockB);
            double[,] idctBlockB = IDCT(dequantizedBlockB);

            // Копируем обработанные данные обратно в массив пикселей
            for (int x = 0; x < blockSize; x++)
            {
                for (int y = 0; y < blockSize; y++)
                {
                    int
                    pixelIndex = ((startY + y) * stride) + ((startX + x) * bytesPerPixel);
                    pixelData[pixelIndex] = (byte)Math.Clamp(idctBlockB[x, y], 0, 255); // Blue
                    pixelData[pixelIndex + 1] = (byte)Math.Clamp(idctBlockG[x, y], 0, 255); // Green
                    pixelData[pixelIndex + 2] = (byte)Math.Clamp(idctBlockR[x, y], 0, 255); // Red
                }
            }
        }

        public Bitmap AddPadding(Bitmap original)
        {
            int newWidth = ((original.Width + 7) / 8) * 8; // округление до ближайшего числа, кратного 8
            int newHeight = ((original.Height + 7) / 8) * 8;

            // Создаем новое изображение с новыми размерами и заполняем его белым цветом
            Bitmap paddedImage = new Bitmap(newWidth, newHeight);
            using (Graphics g = Graphics.FromImage(paddedImage))
            {
                g.Clear(Color.White); // Заполняем пустые пиксели белым (или другим цветом)
                g.DrawImage(original, 0, 0); // Копируем оригинальное изображение
            }

            return paddedImage;
        }

        public int[,] QuantizeBlock(double[,] block)
        {
            int[,] quantizedBlock = new int[8, 8];
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    // Делим коэффициенты блока на значения матрицы квантования и округляем
                    quantizedBlock[i, j] = (int)Math.Round(block[i, j] / QuantizationMatrix[i, j]);
                }
            }
            return quantizedBlock;
        }

        public double[,] DequantizeBlock(int[,] quantizedBlock)
        {
            double[,] dequantizedBlock = new double[8, 8];
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    // Умножаем на значения матрицы квантования, чтобы восстановить блок
                    dequantizedBlock[i, j] = quantizedBlock[i, j] * QuantizationMatrix[i, j];
                }
            }
            return dequantizedBlock;
        }

        public double[,] DCT(double[,] block)
        {
            double[,] result = new double[8, 8];
            double c1 = Math.PI / 16;

            for (int u = 0; u < 8; u++)
            {
                for (int v = 0; v < 8; v++)
                {
                    double sum = 0.0;
                    for (int x = 0; x < 8; x++)
                    {
                        for (int y = 0; y < 8; y++)
                        {
                            sum += block[x, y] *
                            Math.Cos((2 * x + 1) * u * c1) *
                            Math.Cos((2 * y + 1) * v * c1);
                        }
                    }

                    // Учитываем коэффициенты для нормализации
                    double cu = (u == 0) ? 1.0 / Math.Sqrt(2) : 1.0;
                    double cv = (v == 0) ? 1.0 / Math.Sqrt(2) : 1.0;

                    result[u, v] = 0.25 * cu * cv * sum;
                }
            }
            return result;
        }

        public double[,] IDCT(double[,] block)
        {
            double[,] result = new double[8, 8];
            double c1 = Math.PI / 16;

            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 8; y++)
                {
                    double sum = 0.0;
                    for (int u = 0; u < 8; u++)
                    {
                        for (int v = 0; v < 8; v++)
                        {
                            double cu = (u == 0) ? 1.0 / Math.Sqrt(2) : 1.0;
                            double cv = (v == 0) ? 1.0 / Math.Sqrt(2) : 1.0;

                            sum += cu * cv * block[u, v] *
                            Math.Cos((2 * x + 1) * u * c1) *
                            Math.Cos((2 * y + 1) * v * c1);
                        }
                    }
                    result[x, y] = 0.25 * sum;
                }
            }
            return result;
        }
    }
}