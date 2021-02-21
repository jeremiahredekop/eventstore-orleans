using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using AzureStorage;
using EventSourceingCoreStuff;
using EventStore.Client;
using FluentAssertions;
using Newtonsoft.Json;
using Orleans;
using Orleans.EventSourcing;
using Orleans.EventSourcing.CustomStorage;

namespace OrleansEventStore
{
    /// <summary>
    /// The storage interface exposed by grains that want to use the CustomStorage log-consistency provider
    /// <typeparam name="TState">The type for the state of the grain.</typeparam>
    /// <typeparam name="TDelta">The type for delta objects that represent updates to the state.</typeparam>
    /// </summary>
    public interface ICustomStorageInterface2<TState, in TDelta, in TId> where TState: class, IMemento, new()
    {
        /// <summary>
        /// Reads the current state and version from storage
        /// (note that the state object may be mutated by the provider, so it must not be shared).
        /// </summary>
        /// <returns>the version number and a  state object.</returns>
        Task<KeyValuePair<int, TState>> ReadStateFromStorage(TId id);

        /// <summary>
        /// Applies the given array of deltas to storage, and returns true, if the version in storage matches the expected version. 
        /// Otherwise, does nothing and returns false. If successful, the version of storage must be increased by the number of deltas.
        /// </summary>
        /// <returns>true if the deltas were applied, false otherwise</returns>
        Task<bool> ApplyUpdatesToStorage(IReadOnlyList<TDelta> updates, int expectedversion, TId id, TState memento);
    }

    public class EventStoreCustomStorageInterface<TState> : ICustomStorageInterface2 <TState, IEvent, string> where TState : class, IMemento, new()
    {
        private readonly EventStoreClient _esClient;
        private readonly ulong _snapshotThreshold;
        private readonly IRawStorageProvider _raw;
        private readonly Assembly _eventTypeAssembly;

        public EventStoreCustomStorageInterface(EventStoreClient esClient, int snapshotThreshold, IRawStorageProvider raw, Assembly eventTypeAssembly)
        {
            _esClient = esClient;
            _snapshotThreshold = (ulong) snapshotThreshold;
            _raw = raw;
            _eventTypeAssembly = eventTypeAssembly;
        }

        public async Task<KeyValuePair<int, TState>> ReadStateFromStorage(string id)
        {
            if (id == null) return new KeyValuePair<int, TState>(0, new TState());

            var md = await _esClient.GetStreamMetadataAsync(id);
            var serverPosition = md.MetastreamRevision ?? 0;

            var positionToUse = (ulong) 0;

            var memento = new TState();
            if (serverPosition > _snapshotThreshold)
            {
                memento = await _raw.GetMementoAsync<TState>(id);
                positionToUse = memento.Position;
            }

            var events = _esClient.ReadStreamAsync(Direction.Forwards, id, positionToUse);

            if (await events.ReadState == ReadState.StreamNotFound)
                return new KeyValuePair<int, TState>(-1, new TState());

            await foreach (var @event in events)
            {
                var json = Encoding.UTF8.GetString(@event.Event.Data.ToArray());
                var type = _eventTypeAssembly.GetType(@event.Event.EventType);

                if (type == null) throw new Exception("Event type not found...");

                var e = JsonConvert.DeserializeObject(json, type);
                
                TransitionState(memento, e);
                memento.Position = @event.OriginalEventNumber;
            }

            return new KeyValuePair<int, TState>((int) memento.Position, memento);
        }

        public async Task<bool> ApplyUpdatesToStorage(IReadOnlyList<IEvent> updates, int expectedversion, string id, TState memento)
        {
            if (id == null) return true;

            var toPersist = from u in updates
                let type = u.GetType().FullName
                let bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(u))
                select new EventData(Uuid.NewUuid(), type, bytes);

            var toPersistArray = toPersist.ToArray();

            if (expectedversion < 0)
            {
                await _esClient.AppendToStreamAsync(id, StreamState.NoStream, toPersistArray, options => options.ThrowOnAppendFailure = true);
            }
            else
            {
                await _esClient.AppendToStreamAsync(id, new StreamRevision((ulong)expectedversion), toPersistArray, options => options.ThrowOnAppendFailure = true);
            }


            var diff = expectedversion + toPersistArray.Length;

            if (diff > (long) _snapshotThreshold)
            {
                try
                {
                    await _raw.StashMementoAsync(memento, id);
                }
                catch (Exception e)
                {
                    //TODO: LOG
                }
            }

            return true;
        }

        /// <summary>
        /// Defines how to apply events to the state. Unless it is overridden in the subclass, it calls
        /// a dynamic "Apply" function on the state, with the event as a parameter.
        /// All exceptions thrown by this method are caught and logged by the log view provider.
        /// <para>Override this to customize how to transition the state for a given event.</para>
        /// </summary>
        /// <param name="state"></param>
        /// <param name="event"></param>
        private static void TransitionState(object state, object @event)
        {
            dynamic s = state;
            dynamic e = @event;
            s.Apply(e);
        }
    }
    
    public abstract class CustomStorage2JournaledGrain<TState> : JournaledGrain<TState, IEvent>, 
        IGrainWithStringKey,
        ICustomStorageInterface<TState, IEvent> //dumb, but that's the way it is
        where TState : class, IMemento, new()
    {
        private ICustomStorageInterface2<TState, IEvent, string> _storage;

        public CustomStorage2JournaledGrain()
        {
            //TODO: Switch to dependency injection
            _storage = EventStoreClientBehaviors.GetStorage<TState>();
        }

        async Task<KeyValuePair<int, TState>> ICustomStorageInterface<TState, IEvent>.ReadStateFromStorage()
        {
            var result = await _storage.ReadStateFromStorage(GrainPrimaryKey);
            return new KeyValuePair<int, TState>(result.Key, result.Value);
        }

        public override Task OnActivateAsync()
        {
            GrainPrimaryKey = this.GetPrimaryKeyString();
            return base.OnActivateAsync();
        }

        public string GrainPrimaryKey { get; private set; }

        async Task<bool> ICustomStorageInterface<TState, IEvent>.ApplyUpdatesToStorage(IReadOnlyList<IEvent> updates, int expectedversion)
        {
            return await _storage.ApplyUpdatesToStorage(updates, expectedversion, GrainPrimaryKey, State);
        }

    }

    public static class EventStoreClientBehaviors
    {
        public static ICustomStorageInterface2<TState, IEvent, string> GetStorage<TState>() where TState : class, IMemento, new()
        {
            return new EventStoreCustomStorageInterface<TState>(Instance, 1000, RawStorage, EventAssembly);
        }

        public static void InitializeConnection(string eventStoreUri, Assembly eventTypeAssembly, string blobConnstring)
        {
            var settings = new EventStoreClientSettings
            {
                ConnectivitySettings =
                {
                    Address = new Uri(eventStoreUri)
                }
            };

            Instance = new EventStoreClient(settings);
            EventAssembly = eventTypeAssembly;
            RawStorage = new RawStorageProvider(blobConnstring);
        }

        public static IRawStorageProvider RawStorage { get; set; }

        public static Assembly EventAssembly { get; private set; }

        public static EventStoreClient Instance { get; private set; }

    }
}
