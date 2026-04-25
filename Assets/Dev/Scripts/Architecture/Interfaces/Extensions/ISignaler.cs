using UnityEngine;

namespace Sarabande.EntityExtensions
{
    public interface ISignaler
    {

    }

#pragma warning disable CS0618
    public static class SignalerEvents
    {
        public static void SendSignal(this ISignaler signaler, string[] keys) => LevelEntityEvents.Raise_SendSignal(keys);

    }

#pragma warning restore CS0618
}

