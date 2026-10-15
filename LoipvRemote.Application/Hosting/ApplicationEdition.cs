namespace LoipvRemote.Application.Hosting;

public static class ApplicationEdition
{
    public static bool IsPortable
    {
        get
        {
#if PORTABLE
            return true;
#else
            return false;
#endif
        }
    }
}
