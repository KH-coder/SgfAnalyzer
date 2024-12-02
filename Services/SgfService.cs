using System.Text.RegularExpressions;
using SkiaSharp;

public class SgfService : ISgfService
{
    private class Stone
    {
        public int X { get; set; }
        public int Y { get; set; }
        public bool IsBlack { get; set; }

        public Stone(int x, int y, bool isBlack)
        {
            X = x;
            Y = y;
            IsBlack = isBlack;
        }
    }

    private class BoardRegion
    {
        public int StartX { get; set; }
        public int EndX { get; set; }
        public int StartY { get; set; }
        public int EndY { get; set; }
        public string Name { get; set; }

        public BoardRegion(int startX, int endX, int startY, int endY, string name)
        {
            StartX = startX;
            EndX = endX;
            StartY = startY;
            EndY = endY;
            Name = name;
        }
    }

    private const int BOARD_SIZE = 19;
    private const int CELL_SIZE = 35;
    private const int MARGIN = 20;
    private const int STONE_SIZE = 30;
    private readonly List<Stone> stones = new List<Stone>();
    private readonly List<BoardRegion> regions;

    public SgfService()
    {
        regions = new List<BoardRegion>
        {
            new BoardRegion(9, 18, 0, 9, "region1"),
            new BoardRegion(9, 18, 9, 18, "region2"),
            new BoardRegion(0, 9, 0, 9, "region3"),
            new BoardRegion(0, 9, 9, 18, "region4")
        };
    }

    private void ParseSgf(string sgfContent)
    {
        stones.Clear();
        
        Match blackSection = Regex.Match(sgfContent, @"AB\[(.*?)\](?=\w+\[|$)");
        Match whiteSection = Regex.Match(sgfContent, @"AW\[(.*?)\](?=\w+\[|$)");

        if (blackSection.Success)
        {
            string blackStones = blackSection.Groups[1].Value;
            MatchCollection blackMatches = Regex.Matches(blackStones, @"([a-s][a-s])(?:\]|$)");
            foreach (Match pos in blackMatches)
            {
                string position = pos.Groups[1].Value;
                int x = position[0] - 'a';
                int y = position[1] - 'a';
                stones.Add(new Stone(x, y, true));
            }
        }

        if (whiteSection.Success)
        {
            string whiteStones = whiteSection.Groups[1].Value;
            MatchCollection whiteMatches = Regex.Matches(whiteStones, @"([a-s][a-s])(?:\]|$)");
            foreach (Match pos in whiteMatches)
            {
                string position = pos.Groups[1].Value;
                int x = position[0] - 'a';
                int y = position[1] - 'a';
                stones.Add(new Stone(x, y, false));
            }
        }
    }

    public async Task<byte[]> GenerateRegionImage(string sgfContent, string regionName)
    {
        ParseSgf(sgfContent);
        var region = regions.FirstOrDefault(r => r.Name.Equals(regionName, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"Invalid region name: {regionName}");

        return await Task.Run(() => GenerateRegionImageInternal(region));
    }

    public async Task<Dictionary<string, byte[]>> GenerateAllRegionImages(string sgfContent)
    {
        ParseSgf(sgfContent);
        var result = new Dictionary<string, byte[]>();

        foreach (var region in regions)
        {
            result[region.Name] = await Task.Run(() => GenerateRegionImageInternal(region));
        }

        return result;
    }

    private byte[] GenerateRegionImageInternal(BoardRegion region)
    {
        int regionWidth = (region.EndX - region.StartX + 1) * CELL_SIZE + 2 * MARGIN;
        int regionHeight = (region.EndY - region.StartY + 1) * CELL_SIZE + 2 * MARGIN;

        using (var surface = SKSurface.Create(new SKImageInfo(regionWidth, regionHeight)))
        {
            var canvas = surface.Canvas;
            // 使用純白底色
            canvas.Clear(SKColors.White);
            
            using (var borderPaint = new SKPaint
            {
                Color = SKColors.Black,
                StrokeWidth = 2, // 更細的線條
                Style = SKPaintStyle.Stroke,
                IsAntialias = true
            })
            {
                // 繪製網格
                for (int i = 0; i <= (region.EndX - region.StartX); i++)
                {
                    int linePos = MARGIN + i * CELL_SIZE;
                    canvas.DrawLine(linePos, MARGIN, 
                        linePos, MARGIN + (region.EndY - region.StartY) * CELL_SIZE, borderPaint);
                }

                for (int i = 0; i <= (region.EndY - region.StartY); i++)
                {
                    int linePos = MARGIN + i * CELL_SIZE;
                    canvas.DrawLine(MARGIN, linePos,
                        MARGIN + (region.EndX - region.StartX) * CELL_SIZE, linePos, borderPaint);
                }
            }

            // 繪製棋子
            foreach (var stone in stones)
            {
                if (stone.X >= region.StartX && stone.X <= region.EndX &&
                    stone.Y >= region.StartY && stone.Y <= region.EndY)
                {
                    int centerX = MARGIN + (stone.X - region.StartX) * CELL_SIZE;
                    int centerY = MARGIN + (stone.Y - region.StartY) * CELL_SIZE;

                    if (stone.IsBlack)
                    {
                        using (var stonePaint = new SKPaint
                        {
                            Color = SKColors.Black,
                            Style = SKPaintStyle.Fill,
                            IsAntialias = true
                        })
                        {
                            canvas.DrawCircle(centerX, centerY, STONE_SIZE / 2 - 1, stonePaint);
                        }
                    }
                    else
                    {
                        using (var stonePaint = new SKPaint
                        {
                            Color = SKColors.White,
                            Style = SKPaintStyle.Fill,
                            IsAntialias = true
                        })
                        using (var stoneOutlinePaint = new SKPaint
                        {
                            Color = SKColors.Black,
                            StrokeWidth = 1,
                            Style = SKPaintStyle.Stroke,
                            IsAntialias = true
                        })
                        {
                            canvas.DrawCircle(centerX, centerY, STONE_SIZE / 2 - 1, stonePaint);
                            canvas.DrawCircle(centerX, centerY, STONE_SIZE / 2 - 1, stoneOutlinePaint);
                        }
                    }
                }
            }

            // 將圖片轉換為 byte array
            using (var image = surface.Snapshot())
            using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
            {
                // 保存到檔案
                string outputPath = Path.Combine(Directory.GetCurrentDirectory(), "output");
                Directory.CreateDirectory(outputPath);
                string filePath = Path.Combine(outputPath, $"goboard_{region.Name}.png");
                using (var stream = File.OpenWrite(filePath))
                {
                    data.SaveTo(stream);
                }
                
                return data.ToArray();
            }
        }
    }
}