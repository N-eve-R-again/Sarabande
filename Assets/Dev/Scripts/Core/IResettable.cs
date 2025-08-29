namespace Sarabande.Core
{
    /// <summary>Tout objet qui sait se remettre à son état initial.</summary>
    public interface IResettable
    {
        void ResetToInitial();
    }
}
