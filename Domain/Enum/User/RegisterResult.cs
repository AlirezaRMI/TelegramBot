namespace Domain.Enum.User;

public enum RegisterResult
{
    Success,
    UserNotFound,
    UserAlreadyExists,
    UserNotRegistered,
    UserNotActive,
    Error
}