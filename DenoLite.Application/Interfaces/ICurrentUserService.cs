namespace DenoLite.Application.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
}
