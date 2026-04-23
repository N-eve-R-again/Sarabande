using Sarabande.Listeners;
using System;

public static class UIEvents
{
    public static event Func<MessageConfig, int> OnRegisterMsgCollectible;
    public static int NotifyRegisterMsgCollectible(MessageConfig _messageConfig)
        => OnRegisterMsgCollectible?.Invoke(_messageConfig) ?? -1;

    public static event Action<int> OnCollectMsgCollectible;
    public static void NotifyCollectMsgCollectible(int _messageConfig)
        => OnCollectMsgCollectible?.Invoke(_messageConfig);
}