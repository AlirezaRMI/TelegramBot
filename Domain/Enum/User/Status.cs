namespace Domain.Enum.User;

[Flags]
public enum Status : byte
{
    Active,
    Inactive,
    Blocked,
}