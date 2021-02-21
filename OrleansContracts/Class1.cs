using System;
using System.Threading.Tasks;
using EventSourceingCoreStuff;
using Orleans;
using Orleans.EventSourcing.CustomStorage;

namespace OrleansContracts
{
    public interface IShipment : IGrainWithStringKey
    {
        Task Pickup();
        Task Deliver();
        Task<TransitStatus> GetStatus();
        Task ForceDeactivateForDemo();
    }

    public enum TransitStatus
    {
        AwaitingPickup,
        InTransit,
        Delivered
    }

}
