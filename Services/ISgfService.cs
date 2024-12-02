public interface ISgfService
{
    Task<byte[]> GenerateRegionImage(string sgfContent, string regionName);
    Task<Dictionary<string, byte[]>> GenerateAllRegionImages(string sgfContent);
}