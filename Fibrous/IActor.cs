namespace Fibrous
{
    /// <summary>   Interface for actor. </summary>
    ///
    /// <typeparam name="TMsg">  Type of the message. </typeparam>
    public interface IActor<in TMsg> : IDisposableRegistry, IStartable
    {
        void Send(TMsg msg);
    }
}