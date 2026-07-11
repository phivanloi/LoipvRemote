namespace LoipvRemote.Config.DataProviders
{
    public interface IDataProvider<TFormat>
    {
        TFormat Load();

        void Save(TFormat contents);
    }
}