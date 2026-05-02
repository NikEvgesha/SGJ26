namespace LittlePlanet.UI
{
    public interface IManagedWindow
    {
        bool IsWindowOpen { get; }
        void SetWindowOpen(bool isOpen);
    }
}
