using System.Drawing;
using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;

namespace ClusterUnit.ComputeTools
{
    public class FastCompressIMG
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

        private double[,,,] cosTable;
        private double[,] dctMatrix;
        private double[,] idctMatrix;

        private int _countOfThreads = 0;
        private int _numberOfThreads = 8;
        private int blockSize = 8;
        private List<double[,]> _dataBlocks = new List<double[,]>();

        public FastCompressIMG()
        {
            double c1 = Math.PI / 16;

            cosTable = new double[8, 8, 8, 8];
            dctMatrix = new double[8, 8];
            idctMatrix = new double[8, 8];
            InitializeCosTable();
        }

        // Предварительные вычисления для косинусов
        private void InitializeCosTable()
        {
            cu_0 = 1.0 / Math.Sqrt(2);
            cv_0 = 1.0 / Math.Sqrt(2);

            double c1 = Math.PI / 16;

            for (int i = 0; i < QuantizationMatrix.GetLength(0); i++)
            {
                for (int j = 0; j < QuantizationMatrix.GetLength(1); j++)
                {
                    QuantizationMatrix[i, j] = QuantizationMatrix[i, j] * 2;
                }
            }

            // Заполняем таблицу значений косинусов для всех индексов
            for (int u = 0; u < 8; u++)
            {
                for (int v = 0; v < 8; v++)
                {
                    for (int x = 0; x < 8; x++)
                    {
                        for (int y = 0; y < 8; y++)
                        {
                            cosTable[u, v, x, y] = Math.Cos((2 * x + 1) * u * c1) * Math.Cos((2 * y + 1) * v * c1);
                        }
                    }
                }
            }
        }

        public async Task<byte[]> CompressImageAsync(byte[] buffer)
        {
            using (var memoryStream = new MemoryStream(buffer))
            using (var original = new Bitmap(memoryStream))
            {
                Bitmap paddedImage = AddPadding(original);

                int width = paddedImage.Width, height = paddedImage.Height;
                BitmapData bmpData = paddedImage.LockBits(
                    new Rectangle(0, 0, width, height),
                    ImageLockMode.ReadWrite,
                    paddedImage.PixelFormat);

                try
                {
                    int bytesPerPixel = Image.GetPixelFormatSize(paddedImage.PixelFormat) / 8;
                    byte[] pixelData = new byte[bmpData.Stride * height];
                    Marshal.Copy(bmpData.Scan0, pixelData, 0, pixelData.Length);

                    Parallel.For(0, width / blockSize, i =>
                    {
                        for (int j = 0; j < height / blockSize; j++)
                        {
                            ProcessBlock(pixelData, i * blockSize, j * blockSize, bmpData.Stride, bytesPerPixel);
                        }
                    });

                    Marshal.Copy(pixelData, 0, bmpData.Scan0, pixelData.Length);
                }
                finally
                {
                    paddedImage.UnlockBits(bmpData);
                }

                using (var cropped = CropImage(paddedImage, original.Width, original.Height))
                using (var memory = new MemoryStream())
                {
                    cropped.Save(memory, ImageFormat.Jpeg);
                    return memory.ToArray();
                }
            }
        }

        private Bitmap CropImage(Bitmap padded, int originalWidth, int originalHeight)
        {
            var cropArea = new Rectangle(0, 0, originalWidth, originalHeight);
            var cropped = new Bitmap(cropArea.Width, cropArea.Height);
            using (Graphics g = Graphics.FromImage(cropped))
            {
                g.DrawImage(padded, 0, 0, cropArea, GraphicsUnit.Pixel);
            }
            return cropped;
        }

        void ProcessChannel(double[,] channel, out double[,] result)
        {
            double[,] dctBlock = DCT(channel);
            int[,] quantizedBlock = QuantizeBlock(dctBlock);
            double[,] dequantizedBlock = DequantizeBlock(quantizedBlock);
            result = IDCT(dequantizedBlock);
        }

        private async void ProcessBlock(byte[] pixelData, int startX, int startY, int stride, int bytesPerPixel)
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
                    pixelsB[x, y] = pixelData[pixelIndex];     // Blue
                }
            }

            // Обрабатываем каждый канал отдельно
            double[,] idctBlockR = null;
            double[,] idctBlockG = null;
            double[,] idctBlockB = null;

            Parallel.Invoke(
            () => ProcessChannel(pixelsR, out idctBlockR),
            () => ProcessChannel(pixelsG, out idctBlockG),
            () => ProcessChannel(pixelsB, out idctBlockB));

            unsafe
            {
                fixed (byte* pixelPtr = pixelData)
                {
                    for (int x = 0; x < blockSize; x++)
                    {
                        for (int y = 0; y < blockSize; y++)
                        {
                            byte* pixel = pixelPtr + ((startY + y) * stride) + ((startX + x) * bytesPerPixel);
                            pixel[2] = (byte)Math.Clamp(idctBlockR[x, y], 0, 255); // Red
                            pixel[1] = (byte)Math.Clamp(idctBlockG[x, y], 0, 255); // Green
                            pixel[0] = (byte)Math.Clamp(idctBlockB[x, y], 0, 255); // Blue
                        }
                    }
                }
            }
        }

        public Bitmap AddPadding(Bitmap original)
        {
            int newWidth = ((original.Width + 7) / 8) * 8;  // округление до ближайшего числа, кратного 8
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

        double cu_0;
        double cv_0;

        // DCT (Дискретное косинусное преобразование)
        public double[,] DCT(double[,] block)
        {
            double[,] result = new double[8, 8];
            for (int u = 0; u < 8; u++)
            {
                for (int v = 0; v < 8; v++)
                {
                    double sum = 0.0;
                    for (int x = 0; x < 8; x++)
                    {
                        for (int y = 0; y < 8; y++)
                        {
                            sum += block[x, y] * cosTable[u, v, x, y];
                        }
                    }

                    double cu = (u == 0) ? cu_0 : 1.0;
                    double cv = (v == 0) ? cv_0 : 1.0;

                    result[u, v] = 0.25 * cu * cv * sum;
                }
            }
            return result;
        }

        // IDCT (Обратное дискретное косинусное преобразование)
        public double[,] IDCT(double[,] block)
        {
            double[,] result = new double[8, 8];
            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 8; y++)
                {
                    double sum = 0.0;
                    for (int u = 0; u < 8; u++)
                    {
                        for (int v = 0; v < 8; v++)
                        {
                            double cu = (u == 0) ? cu_0 : 1.0;
                            double cv = (v == 0) ? cv_0 : 1.0;

                            sum += cu * cv * block[u, v] * cosTable[u, v, x, y];
                        }
                    }
                    result[x, y] = 0.25 * sum;
                }
            }
            return result;
        }
    }
}
