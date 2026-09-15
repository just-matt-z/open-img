using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OpenImage.Engine;

public static class DrawingEngine
{
    /// <summary>
    /// <summary>
    /// Draws a solid, soft, or airbrush circular dab on a WriteableBitmap in Bgra32 format with customizable hardness
    /// </summary>
    public static unsafe void DrawBrushStamp(WriteableBitmap bmp, int centerX, int centerY, int radius, Color color, double opacity, bool isEraser = false, double hardness = 0.5)
    {
        if (bmp == null || radius <= 0) return;

        bmp.Lock();
        try
        {
            int width = bmp.PixelWidth;
            int height = bmp.PixelHeight;
            int stride = bmp.BackBufferStride;
            byte* buffer = (byte*)bmp.BackBuffer.ToPointer();

            int minX = Math.Max(0, centerX - radius);
            int maxX = Math.Min(width - 1, centerX + radius);
            int minY = Math.Max(0, centerY - radius);
            int maxY = Math.Min(height - 1, centerY + radius);

            float rSquared = radius * radius;
            byte drawB = color.B;
            byte drawG = color.G;
            byte drawR = color.R;
            float alphaFactor = (float)(opacity * (color.A / 255.0));
            float hard = (float)Math.Clamp(hardness, 0.0, 1.0);

            for (int y = minY; y <= maxY; y++)
            {
                byte* row = buffer + (y * stride);
                int dy = y - centerY;
                int dySquared = dy * dy;

                for (int x = minX; x <= maxX; x++)
                {
                    int dx = x - centerX;
                    int distSquared = dx * dx + dySquared;

                    if (distSquared <= rSquared)
                    {
                        float distance = MathF.Sqrt(distSquared);
                        float normDist = distance / radius; // 0.0 at center, 1.0 at outer edge
                        if (normDist > 1.0f) continue;

                        float falloff;
                        if (hard >= 0.98f)
                        {
                            // Crisp / Ink Pen with 1-px antialiased boundary
                            falloff = Math.Clamp((float)radius - distance, 0.0f, 1.0f);
                        }
                        else if (normDist <= hard)
                        {
                            falloff = 1.0f;
                        }
                        else
                        {
                            // Smooth cubic hermite falloff from hardness threshold to 1.0
                            float t = (normDist - hard) / (1.0f - hard);
                            falloff = 1.0f - (t * t * (3.0f - 2.0f * t));
                        }

                        float pixelAlpha = alphaFactor * falloff;
                        if (pixelAlpha <= 0.001f) continue;

                        byte* pixel = row + (x * 4);

                        if (isEraser)
                        {
                            // Subtract alpha
                            pixel[3] = (byte)Math.Max(0, pixel[3] - (int)(pixelAlpha * 255));
                        }
                        else
                        {
                            // Alpha blend over existing pixel
                            float srcA = pixelAlpha;
                            float dstA = (pixel[3] / 255.0f) * (1.0f - srcA);
                            float outA = srcA + dstA;

                            if (outA > 0.001f)
                            {
                                pixel[0] = (byte)Math.Clamp((drawB * srcA + pixel[0] * dstA) / outA, 0, 255);
                                pixel[1] = (byte)Math.Clamp((drawG * srcA + pixel[1] * dstA) / outA, 0, 255);
                                pixel[2] = (byte)Math.Clamp((drawR * srcA + pixel[2] * dstA) / outA, 0, 255);
                                pixel[3] = (byte)Math.Clamp(outA * 255, 0, 255);
                            }
                        }
                    }
                }
            }

            bmp.AddDirtyRect(new Int32Rect(minX, minY, maxX - minX + 1, maxY - minY + 1));
        }
        finally
        {
            bmp.Unlock();
        }
    }

    /// <summary>
    /// Interpolates a continuous stroke between two points with hardness falloff
    /// </summary>
    public static void DrawBrushLine(WriteableBitmap bmp, Point p1, Point p2, int radius, Color color, double opacity, bool isEraser = false, double hardness = 0.5)
    {
        double dx = p2.X - p1.X;
        double dy = p2.Y - p1.Y;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        double step = hardness < 0.3 ? Math.Max(1.0, radius * 0.15) : Math.Max(1.0, radius * 0.30); // denser step for airbrush
        int steps = Math.Max(1, (int)Math.Ceiling(dist / step));

        for (int i = 0; i <= steps; i++)
        {
            double t = (double)i / steps;
            int x = (int)Math.Round(p1.X + dx * t);
            int y = (int)Math.Round(p1.Y + dy * t);
            DrawBrushStamp(bmp, x, y, radius, color, opacity, isEraser, hardness);
        }
    }

    /// <summary>
    /// Clones pixels from source coordinates to target coordinates with hardness falloff
    /// </summary>
    public static unsafe void DrawCloneStamp(WriteableBitmap targetBmp, WriteableBitmap sourceBmp, int targetCenterX, int targetCenterY, int sourceCenterX, int sourceCenterY, int radius, double opacity, double hardness = 0.5)
    {
        if (targetBmp == null || sourceBmp == null || radius <= 0) return;

        int tWidth = targetBmp.PixelWidth;
        int tHeight = targetBmp.PixelHeight;
        int sWidth = sourceBmp.PixelWidth;
        int sHeight = sourceBmp.PixelHeight;

        int minTX = Math.Max(0, targetCenterX - radius);
        int maxTX = Math.Min(tWidth - 1, targetCenterX + radius);
        int minTY = Math.Max(0, targetCenterY - radius);
        int maxTY = Math.Min(tHeight - 1, targetCenterY + radius);

        if (minTX > maxTX || minTY > maxTY) return;

        int rSquared = radius * radius;
        float alphaFactor = (float)Math.Clamp(opacity, 0.0, 1.0);
        float hard = (float)Math.Clamp(hardness, 0.0, 1.0);

        int dabW = maxTX - minTX + 1;
        int dabH = maxTY - minTY + 1;
        uint[] sampleBuffer = new uint[dabW * dabH];

        sourceBmp.Lock();
        try
        {
            byte* sScan0 = (byte*)sourceBmp.BackBuffer;
            int sStride = sourceBmp.BackBufferStride;

            for (int y = 0; y < dabH; y++)
            {
                int curTY = minTY + y;
                int curSY = sourceCenterY + (curTY - targetCenterY);

                if (curSY >= 0 && curSY < sHeight)
                {
                    byte* sRow = sScan0 + (curSY * sStride);
                    for (int x = 0; x < dabW; x++)
                    {
                        int curTX = minTX + x;
                        int curSX = sourceCenterX + (curTX - targetCenterX);

                        if (curSX >= 0 && curSX < sWidth)
                        {
                            sampleBuffer[y * dabW + x] = *(uint*)(sRow + (curSX * 4));
                        }
                    }
                }
            }
        }
        finally
        {
            sourceBmp.Unlock();
        }

        targetBmp.Lock();
        try
        {
            byte* tScan0 = (byte*)targetBmp.BackBuffer;
            int tStride = targetBmp.BackBufferStride;

            for (int y = 0; y < dabH; y++)
            {
                int curTY = minTY + y;
                int dy = curTY - targetCenterY;
                byte* tRow = tScan0 + (curTY * tStride);

                for (int x = 0; x < dabW; x++)
                {
                    int curTX = minTX + x;
                    int dx = curTX - targetCenterX;
                    int distSquared = dx * dx + dy * dy;

                    if (distSquared <= rSquared)
                    {
                        uint sampledPixel = sampleBuffer[y * dabW + x];
                        byte srcA_orig = (byte)((sampledPixel >> 24) & 0xFF);
                        if (srcA_orig == 0) continue;

                        byte srcR = (byte)((sampledPixel >> 16) & 0xFF);
                        byte srcG = (byte)((sampledPixel >> 8) & 0xFF);
                        byte srcB = (byte)(sampledPixel & 0xFF);

                        float distance = MathF.Sqrt(distSquared);
                        float normDist = distance / radius;
                        if (normDist > 1.0f) continue;

                        float falloff;
                        if (hard >= 0.98f)
                        {
                            falloff = Math.Clamp((float)radius - distance, 0.0f, 1.0f);
                        }
                        else if (normDist <= hard)
                        {
                            falloff = 1.0f;
                        }
                        else
                        {
                            float t = (normDist - hard) / (1.0f - hard);
                            falloff = 1.0f - (t * t * (3.0f - 2.0f * t));
                        }

                        float pixelAlpha = alphaFactor * (srcA_orig / 255.0f) * falloff;
                        if (pixelAlpha <= 0.001f) continue;

                        byte* pixel = tRow + (curTX * 4);
                        float dstA = (pixel[3] / 255.0f) * (1.0f - pixelAlpha);
                        float outA = pixelAlpha + dstA;

                        if (outA > 0.0001f)
                        {
                            pixel[0] = (byte)Math.Clamp((srcB * pixelAlpha + pixel[0] * dstA) / outA, 0, 255);
                            pixel[1] = (byte)Math.Clamp((srcG * pixelAlpha + pixel[1] * dstA) / outA, 0, 255);
                            pixel[2] = (byte)Math.Clamp((srcR * pixelAlpha + pixel[2] * dstA) / outA, 0, 255);
                            pixel[3] = (byte)Math.Clamp(outA * 255, 0, 255);
                        }
                    }
                }
            }

            targetBmp.AddDirtyRect(new Int32Rect(minTX, minTY, dabW, dabH));
        }
        finally
        {
            targetBmp.Unlock();
        }
    }

    /// <summary>
    /// Interpolates continuous clone stamp stroke between points
    /// </summary>
    public static void DrawCloneLine(WriteableBitmap targetBmp, WriteableBitmap sourceBmp, Point targetP1, Point targetP2, Point sourceP1, Point sourceP2, int radius, double opacity, double hardness = 0.5)
    {
        double dx = targetP2.X - targetP1.X;
        double dy = targetP2.Y - targetP1.Y;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        double step = hardness < 0.3 ? Math.Max(1.0, radius * 0.15) : Math.Max(1.0, radius * 0.30);
        int steps = Math.Max(1, (int)Math.Ceiling(dist / step));

        for (int i = 0; i <= steps; i++)
        {
            double t = (double)i / steps;
            int tx = (int)Math.Round(targetP1.X + dx * t);
            int ty = (int)Math.Round(targetP1.Y + dy * t);
            int sx = (int)Math.Round(sourceP1.X + (sourceP2.X - sourceP1.X) * t);
            int sy = (int)Math.Round(sourceP1.Y + (sourceP2.Y - sourceP1.Y) * t);
            DrawCloneStamp(targetBmp, sourceBmp, tx, ty, sx, sy, radius, opacity, hardness);
        }
    }

    /// <summary>
    /// High-speed BFS Queue Flood Fill algorithm on WriteableBitmap
    /// </summary>
    public static unsafe void FloodFill(WriteableBitmap bmp, int startX, int startY, Color fillColor, int tolerance = 32)
    {
        if (bmp == null) return;
        int width = bmp.PixelWidth;
        int height = bmp.PixelHeight;

        if (startX < 0 || startX >= width || startY < 0 || startY >= height) return;

        bmp.Lock();
        try
        {
            byte* buffer = (byte*)bmp.BackBuffer.ToPointer();
            int stride = bmp.BackBufferStride;

            byte* startPixel = buffer + (startY * stride) + (startX * 4);
            byte targetB = startPixel[0];
            byte targetG = startPixel[1];
            byte targetR = startPixel[2];
            byte targetA = startPixel[3];

            byte fillB = fillColor.B;
            byte fillG = fillColor.G;
            byte fillR = fillColor.R;
            byte fillA = fillColor.A;

            if (Math.Abs(targetB - fillB) <= 2 && Math.Abs(targetG - fillG) <= 2 && Math.Abs(targetR - fillR) <= 2 && targetA == fillA)
                return;

            bool[] visited = new bool[width * height];
            var queue = new Queue<Point>(1024);
            queue.Enqueue(new Point(startX, startY));
            visited[startY * width + startX] = true;

            int minX = startX, maxX = startX, minY = startY, maxY = startY;

            while (queue.Count > 0)
            {
                var pt = queue.Dequeue();
                int x = (int)pt.X;
                int y = (int)pt.Y;

                byte* current = buffer + (y * stride) + (x * 4);
                current[0] = fillB;
                current[1] = fillG;
                current[2] = fillR;
                current[3] = fillA;

                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;

                // Check 4 neighbors
                int[] dx = { -1, 1, 0, 0 };
                int[] dy = { 0, 0, -1, 1 };

                for (int i = 0; i < 4; i++)
                {
                    int nx = x + dx[i];
                    int ny = y + dy[i];

                    if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                    {
                        int index = ny * width + nx;
                        if (!visited[index])
                        {
                            byte* neighbor = buffer + (ny * stride) + (nx * 4);
                            if (Math.Abs(neighbor[0] - targetB) <= tolerance &&
                                Math.Abs(neighbor[1] - targetG) <= tolerance &&
                                Math.Abs(neighbor[2] - targetR) <= tolerance &&
                                Math.Abs(neighbor[3] - targetA) <= tolerance)
                            {
                                visited[index] = true;
                                queue.Enqueue(new Point(nx, ny));
                            }
                        }
                    }
                }
            }

            bmp.AddDirtyRect(new Int32Rect(minX, minY, maxX - minX + 1, maxY - minY + 1));
        }
        finally
        {
            bmp.Unlock();
        }
    }

    /// <summary>
    /// Samples pixel color at specified coordinates
    /// </summary>
    public static unsafe Color SampleColor(WriteableBitmap bmp, int x, int y)
    {
        if (bmp == null || x < 0 || x >= bmp.PixelWidth || y < 0 || y >= bmp.PixelHeight)
            return Colors.Black;

        bmp.Lock();
        try
        {
            byte* pixel = (byte*)bmp.BackBuffer.ToPointer() + (y * bmp.BackBufferStride) + (x * 4);
            return Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
        }
        finally
        {
            bmp.Unlock();
        }
    }
}
