namespace Application.Services.Interfaces;

public interface IStateService
{
    void SetState(long chatId, string state);
    string? GetState(long chatId);
    void ClearState(long chatId); 
}