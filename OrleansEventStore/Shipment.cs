using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrleansContracts;

namespace OrleansEventStore
{
    public class Shipment : CustomStorage2JournaledGrain<ShipmentState>, IShipment
    {
        private readonly ILogger<Shipment> _logger;

        public Shipment(ILogger<Shipment> logger)
        {
            _logger = logger;
        }

        public async Task Pickup()
        {
            if (State.Status == TransitStatus.InTransit)
            {
                throw new InvalidOperationException("Shipment has already been picked up.");
            }

            if (State.Status == TransitStatus.Delivered)
            {
                throw new InvalidOperationException("Shipment has already been delivered.");
            }

            RaiseEvent(new PickedUp(DateTime.UtcNow));
            await ConfirmEvents();
        }

        public async Task Deliver()
        {
            if (State.Status == TransitStatus.AwaitingPickup)
            {
                throw new InvalidOperationException("Shipment has not yet been picked up.");
            }

            if (State.Status == TransitStatus.Delivered)
            {
                throw new InvalidOperationException("Shipment has already been delivered.");
            }

            RaiseEvent(new Delivered(DateTime.UtcNow));
            await ConfirmEvents();
        }

        public Task ForceDeactivateForDemo()
        {
            DeactivateOnIdle();
            return Task.CompletedTask;
        }

        public Task<TransitStatus> GetStatus()
        {
            return Task.FromResult(State.Status);
        }
    }
}
