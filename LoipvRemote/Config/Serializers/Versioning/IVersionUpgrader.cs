using System;

namespace LoipvRemote.Config.Serializers.Versioning
{
    public interface IVersionUpgrader
    {
        bool CanUpgrade(Version currentVersion);
        Version Upgrade();
    }
}