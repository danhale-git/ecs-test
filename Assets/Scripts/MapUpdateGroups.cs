using Unity.Entities;

namespace MapUpdateGroups
{
    [UpdateAfter(typeof(DiscoveryBarrier))]
    public class InitialiseSquaresGroup : ComponentSystemGroup { }
}