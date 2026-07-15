namespace LoipvRemote.Security
{
    public interface ICryptoProviderFactory
    {
        ICryptographyProvider Build();
    }
}