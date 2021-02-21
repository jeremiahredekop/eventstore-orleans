using System;
using EventSourceingCoreStuff;

namespace OrleansEventStore
{
    public class Delivered : IEvent
    {
        public Delivered(DateTime dateTime)
        {
            DateTime = dateTime;
        }
        
        public DateTime DateTime { get; }
    }
}