using Application.Services.Interfaces;
using Domain.IRipository;

namespace Application.Services.Implementations;

public class StateService : IStateService
{
    private readonly Dictionary<long, string> _userStates = new();
    private readonly Dictionary<long, Dictionary<string, object>> _tempData = new();

    public string? GetState(long chatId)
        => _userStates.TryGetValue(chatId, out var state) ? state : null;

    public void SetState(long chatId, string state)
        => _userStates[chatId] = state;

    public void ClearState(long chatId)
        => _userStates.Remove(chatId);

    public void SetTempData<T>(long chatId, string key, T value)
    {
        if (!_tempData.ContainsKey(chatId))
            _tempData[chatId] = new Dictionary<string, object>();

        _tempData[chatId][key] = value!;
    }

    public T GetTempData<T>(long chatId, string key)
    {
        if (_tempData.TryGetValue(chatId, out var data) && data.TryGetValue(key, out var value))
        {
            return (T)value!;
        }

        throw new KeyNotFoundException($"TempData with key '{key}' not found for chat {chatId}");
    }

    public void ClearTempData(long chatId)
        => _tempData.Remove(chatId);
}
