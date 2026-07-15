namespace LoipvRemote.Config
{
    public interface ISaver<in T>
    {
        void Save(T model, string propertyNameTrigger = "");
    }
}