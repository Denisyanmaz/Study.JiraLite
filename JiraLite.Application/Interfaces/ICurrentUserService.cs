namespace JiraLite.Application.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
}
