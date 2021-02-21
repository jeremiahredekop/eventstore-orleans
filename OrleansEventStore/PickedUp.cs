using System;
using EventSourceingCoreStuff;

namespace OrleansEventStore
{
    public class PickedUp : IEvent
    {
        public PickedUp(DateTime dateTime)
        {
            DateTime = dateTime;
        }
        
        public DateTime DateTime { get; }
    }
}