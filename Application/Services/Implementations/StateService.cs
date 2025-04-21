using Application.Services.Interfaces;
using Domain.IRipository;

namespace Application.Services.Implementations;

public class StateService(Dictionary<long,string> userstate): IStateService
{
    public void SetState(long chatId, string state)
    {
        userstate[chatId] = state;
    }

    public string? GetState(long chatId)
    {
      return userstate.TryGetValue(chatId, out var  state) ? state: null;
    }

    public void ClearState(long chatId)
    {
        userstate.Remove(chatId);
    }
}