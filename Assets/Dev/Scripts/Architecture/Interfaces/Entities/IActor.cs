using Sarabande.Core;
using UnityEngine;
namespace Sarabande.Actors
{
    public interface IActor
    {
        public ActorType type { get; }
        public void Register() { }
    }
}


