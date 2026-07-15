using System;

namespace LoipvRemote.Config.Serializers
{
    public interface ISerializer<in TIn, out TOut>
    {
        TOut Serialize(TIn model);
        Version Version { get; }
    }
}