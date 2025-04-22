namespace Application.Services.Interfaces;

public interface IStateService
{
    string? GetState(long chatId);
    void SetState(long chatId, string state);
    void ClearState(long chatId);
    
    void SetTempData<T>(long chatId, string key, T value);
    T GetTempData<T>(long chatId, string key, T defaultValue = default);
    void ClearTempData(long chatId);
    
}