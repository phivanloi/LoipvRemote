namespace LoipvRemote.Desktop.Navigation;

/// <summary>Desktop navigation boundary used by command handlers and tab hosts.</summary>
public interface IConnectionNavigator
{
    void Activate(Guid connectionId);
    void Close(Guid connectionId);
}
