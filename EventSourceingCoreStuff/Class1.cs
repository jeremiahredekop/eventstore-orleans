using System;
using System.Threading.Tasks;

namespace EventSourceingCoreStuff
{
    public interface IRawStorageProvider
    {
        Task<T> GetMementoAsync<T>(string id);
        Task StashMementoAsync(IMemento memento, string id);
    }


    public interface IEvent { }

    public interface IMemento
    {
        ulong Position { get; set; }
    }
}
