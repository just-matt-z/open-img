namespace OpenImage.Models;

public class HistoryState
{
    public string Description { get; set; } = "Action";
    public string Timestamp { get; set; } = DateTime.Now.ToString("HH:mm:ss");
    public double CanvasWidth { get; set; }
    public double CanvasHeight { get; set; }
    public List<Layer> Layers { get; set; } = new();
    public Adjustments Adjustments { get; set; } = new();

    public static HistoryState Create(string description, double width, double height, IEnumerable<Layer> layers, Adjustments adj)
    {
        return new HistoryState
        {
            Description = description,
            Timestamp = DateTime.Now.ToString("HH:mm:ss"),
            CanvasWidth = width,
            CanvasHeight = height,
            Layers = layers.Select(l => l.Clone()).ToList(),
            Adjustments = adj.Clone()
        };
    }
}
