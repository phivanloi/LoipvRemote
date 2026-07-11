namespace LoipvRemote.Security.KeyDerivation
{
    public interface IKeyDerivationFunction
    {
        byte[] DeriveKey(string password, byte[] salt);
    }
}