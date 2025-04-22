using System.Collections.Concurrent;
using Application.Services.Interfaces;
using Domain.IRipository;

namespace Application.Services.Implementations;

public class StateService : IStateService
{
    private readonly ConcurrentDictionary<long, string> _userStates = new();
    private readonly ConcurrentDictionary<long, Dictionary<string, object>> _tempData = new();

    public string? GetState(long chatId)
        => _userStates.TryGetValue(chatId, out var state) ? state : null;

    public void SetState(long chatId, string state)
        => _userStates[chatId] = state;

    public void ClearState(long chatId)
        => _userStates.TryRemove(chatId, out _);

    public void SetTempData<T>(long chatId, string key, T value)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value), "Value cannot be null");

        _tempData.AddOrUpdate(chatId, new Dictionary<string, object> { { key, value } }, (k, existingData) =>
        {
            existingData[key] = value;
            return existingData;
        });
    }

    public T GetTempData<T>(long chatId, string key, T defaultValue = default)
    {
        if (_tempData.TryGetValue(chatId, out var data) && data.TryGetValue(key, out var value))
        {
            return (T)value!;
        }
        return defaultValue;
    }

    public void ClearTempData(long chatId)
        => _tempData.TryRemove(chatId, out _);
}