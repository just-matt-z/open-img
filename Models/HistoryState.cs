using System.Windows.Media;

namespace OpenImage.Models;

public class HistoryState
{
    public string Description { get; set; } = "Action";
    public string Timestamp { get; set; } = DateTime.Now.ToString("HH:mm:ss");
    public double CanvasWidth { get; set; }
    public double CanvasHeight { get; set; }
    public List<Layer> Layers { get; set; } = new();
    public Adjustments Adjustments { get; set; } = new();
    public ImageSource? Thumbnail { get; set; }
    public int StepNumber { get; set; } = 1;
    public bool IsCurrentState { get; set; } = false;

    public static HistoryState Create(string description, double width, double height, IEnumerable<Layer> layers, Adjustments adj, ImageSource? thumb = null, int stepNumber = 1)
    {
        return new HistoryState
        {
            Description = description,
            Timestamp = DateTime.Now.ToString("HH:mm:ss"),
            CanvasWidth = width,
            CanvasHeight = height,
            Layers = layers.Select(l => l.Clone()).ToList(),
            Adjustments = adj.Clone(),
            Thumbnail = thumb,
            StepNumber = stepNumber,
            IsCurrentState = true
        };
    }
}
