namespace LoipvRemote.Config
{
    public interface ILoader<out T>
    {
        T Load();
    }
}