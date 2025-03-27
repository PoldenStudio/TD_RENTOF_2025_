public interface IPanelVisualizer
{
    void VisualizePanelPressed(int panelIndex);
    void VisualizePanelReleased(int panelIndex);
    bool IsEnabled { get; set; }
}